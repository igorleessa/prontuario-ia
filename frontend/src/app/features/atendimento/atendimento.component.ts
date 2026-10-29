import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AtendimentoDetalhe, CLASSE_STATUS, ROTULO_STATUS } from '../../core/models/atendimento.model';
import { NotaExportavel, ResultadoExportacao } from '../../core/models/exportacao.model';
import { RascunhoClinico } from '../../core/models/rascunho-clinico.model';
import { ModoTranscricao } from '../../core/models/transcricao.model';
import { AppConfigService } from '../../core/services/app-config.service';
import { AtendimentoService } from '../../core/services/atendimento.service';
import { AuthService } from '../../core/services/auth.service';
import { DestinoEscuta, EscutaConsultaService } from '../../core/services/escuta-consulta.service';
import { TranscricaoService } from '../../core/services/transcricao.service';
import { AvatarComponent } from '../../shared/avatar.component';
import { IconeComponent, NomeIcone } from '../../shared/icone.component';
import { cpfMascarado, idade } from '../../shared/pessoa';
import { TextoProtegidoComponent } from '../../shared/texto-protegido.component';
import { DocumentosComponent } from './documentos/documentos.component';

type Etapa = 'consentimento' | 'consulta' | 'revisao' | 'encerrado';

/**
 * Os dois formatos de registro previstos na especificacao: o prontuario nativo
 * estruturado (Modalidade A) e a nota de texto corrido enviada ao EMR externo
 * (Modalidade B). O medico pode ver o atendimento nos dois, mesmo que a clinica
 * so exporte um deles.
 */
type Visao = 'estruturado' | 'nota';

type Campo = keyof RascunhoClinico;

/** De onde veio o texto atual do campo: e o que a etiqueta ao lado do rotulo mostra. */
type Origem = 'vazio' | 'ia' | 'medico';

interface DefinicaoCampo {
  chave: Campo;
  rotulo: string;
  icone: NomeIcone;
  linhas: number;
  espera: string;
}

/** Trecho da transcricao na tela: o texto ja sanitizado que o servidor devolveu. */
interface TrechoNaTela {
  sequencia: number;
  texto: string;
  estado: 'transcrevendo' | 'pronto' | 'falhou';
}

const CAMPOS: DefinicaoCampo[] = [
  { chave: 'queixaPrincipal', rotulo: 'Queixa principal', icone: 'queixa', linhas: 2, espera: 'Motivo da consulta, com a duração quando dita.' },
  { chave: 'hda', rotulo: 'História da doença atual', icone: 'historico', linhas: 5, espera: 'Início, características, sintomas associados e negativas pertinentes.' },
  { chave: 'antecedentes', rotulo: 'Antecedentes', icone: 'antecedentes', linhas: 3, espera: 'Comorbidades, medicamentos em uso, alergias, cirurgias e hábitos.' },
  { chave: 'exameFisico', rotulo: 'Exame físico', icone: 'estetoscopio', linhas: 3, espera: 'Sinais vitais e achados verbalizados durante o exame.' },
  { chave: 'hipoteseDiagnostica', rotulo: 'Hipótese diagnóstica', icone: 'raciocinio', linhas: 2, espera: 'Hipóteses formuladas pelo médico.' },
  { chave: 'cid10Sugerido', rotulo: 'CID-10', icone: 'identificacao', linhas: 1, espera: 'Ex.: J06.9' },
  { chave: 'conduta', rotulo: 'Conduta', icone: 'conduta', linhas: 4, espera: 'Prescrição, exames, orientações e retorno.' },
];

/** Rascunho refeito quando chegam estes trechos novos... */
const TRECHOS_PARA_ATUALIZAR = 3;
/** ...ou quando ha qualquer trecho novo e este tempo passou desde o ultimo. */
const INTERVALO_ATUALIZACAO_MS = 20_000;
/** Ritmo das falas da consulta de demonstracao. */
const INTERVALO_DEMONSTRACAO_MS = 2_600;

@Component({
  selector: 'app-atendimento',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DatePipe,
    DecimalPipe,
    NgTemplateOutlet,
    DocumentosComponent,
    IconeComponent,
    AvatarComponent,
    TextoProtegidoComponent,
  ],
  templateUrl: './atendimento.component.html',
  styleUrl: './atendimento.component.scss',
})
export class AtendimentoComponent {
  private readonly atendimentos = inject(AtendimentoService);
  private readonly transcricoes = inject(TranscricaoService);
  private readonly escuta = inject(EscutaConsultaService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly configuracaoApp = inject(AppConfigService);

  /** Vem do parametro de rota via withComponentInputBinding(). */
  readonly id = input.required<string>();

  readonly campos = CAMPOS;
  readonly rotuloStatus = ROTULO_STATUS;
  readonly classeStatus = CLASSE_STATUS;

  readonly carregando = signal(true);
  readonly atendimento = signal<AtendimentoDetalhe | null>(null);
  readonly erro = signal<string | null>(null);
  readonly aviso = signal<string | null>(null);
  readonly salvando = signal(false);

  // ---------- Escuta ----------

  readonly estadoEscuta = this.escuta.estado;
  readonly modoEscuta = this.escuta.modo;
  readonly nivelAudio = this.escuta.nivel;
  readonly falaEmAndamento = this.escuta.falaEmAndamento;
  readonly iniciandoEscuta = signal(false);
  readonly encerrando = signal(false);
  readonly segundosEscuta = signal(0);
  readonly segundosEmSilencio = signal(0);
  readonly trechos = signal<TrechoNaTela[]>([]);
  readonly dadosProtegidos = signal(0);
  readonly mostrarTranscricao = signal(true);
  private readonly listaTranscricao = viewChild<ElementRef<HTMLElement>>('listaTranscricao');

  /** Simulacao do modo demonstracao: falas ficticias entrando como se viessem do provedor. */
  readonly simulando = signal(false);
  readonly demonstracao = this.configuracaoApp.demonstracao;

  readonly ouvindo = computed(() => this.estadoEscuta() === 'ouvindo' || this.simulando());
  readonly escutaAtiva = computed(() => this.estadoEscuta() !== 'parada' || this.simulando());
  readonly microfoneMudo = computed(() => !this.simulando() && this.segundosEmSilencio() >= 10);

  /**
   * Barras do medidor de voz: o nivel atual espalhado em alturas diferentes.
   * Na demonstracao nao ha microfone; a animacao fica por conta do CSS.
   */
  readonly barrasNivel = computed(() => {
    const nivel = this.simulando() ? 0.6 : this.nivelAudio();
    return [0.45, 0.75, 1, 0.8, 0.55, 0.9, 0.65].map((peso) => Math.max(0.12, Math.min(1, nivel * peso * 1.6)));
  });

  readonly tempoEscuta = computed(() => {
    const total = this.segundosEscuta();
    const minutos = Math.floor(total / 60).toString().padStart(2, '0');
    const segundos = (total % 60).toString().padStart(2, '0');
    return `${minutos}:${segundos}`;
  });

  readonly rotuloModo = computed(() => {
    if (this.simulando()) {
      return 'Demonstração';
    }
    return this.modoEscuta() === 'TempoReal' ? 'Tempo real' : this.modoEscuta() === 'Trechos' ? 'Por trechos' : null;
  });

  // ---------- Rascunho ao vivo ----------

  readonly gerandoRascunho = signal(false);
  readonly rascunhoAtualizadoEm = signal<Date | null>(null);
  readonly erroIA = signal<string | null>(null);
  readonly origem = signal<Record<Campo, Origem>>(this.origensVazias());
  readonly sugestoes = signal<Partial<Record<Campo, string>>>({});
  readonly destacados = signal<ReadonlySet<Campo>>(new Set());
  readonly sugestaoIa = signal<RascunhoClinico | null>(null);
  readonly mostrarSugestao = signal(false);
  readonly agora = signal(Date.now());

  readonly camposPreenchidos = computed(() => {
    const origem = this.origem();
    return this.campos.filter((campo) => origem[campo.chave] !== 'vazio').length;
  });

  readonly atualizadoHa = computed(() => {
    const em = this.rascunhoAtualizadoEm();
    if (!em) {
      return null;
    }
    const segundos = Math.max(0, Math.round((this.agora() - em.getTime()) / 1000));
    return segundos < 60 ? `há ${segundos} s` : `há ${Math.floor(segundos / 60)} min`;
  });

  // ---------- Revisao e encerrado ----------

  readonly visao = signal<Visao>('estruturado');
  readonly nota = signal<string | null>(null);
  readonly carregandoNota = signal(false);
  readonly notaCopiada = signal(false);
  readonly notaGravada = signal<NotaExportavel | null>(null);
  readonly exportando = signal(false);
  readonly resultadoExportacao = signal<ResultadoExportacao | null>(null);
  readonly baixandoPdf = signal(false);

  /** A etapa exibida deriva do status persistido, para que o atendimento possa ser retomado. */
  readonly etapa = computed<Etapa>(() => {
    switch (this.atendimento()?.status) {
      case 'EmConsulta':
        return 'consulta';
      case 'EmRevisao':
        return 'revisao';
      case 'Finalizado':
      case 'Cancelado':
        return 'encerrado';
      default:
        return 'consentimento';
    }
  });

  readonly passos = computed(() => {
    const ordem: Etapa[] = ['consentimento', 'consulta', 'revisao', 'encerrado'];
    const atual = ordem.indexOf(this.etapa());
    return [
      { rotulo: 'Consentimento', icone: 'privacidade' as NomeIcone },
      { rotulo: 'Consulta', icone: 'escuta' as NomeIcone },
      { rotulo: 'Revisão', icone: 'caneta' as NomeIcone },
      { rotulo: 'Registro assinado', icone: 'assinatura' as NomeIcone },
    ].map((passo, indice) => ({
      ...passo,
      feito: indice < atual || (indice === 3 && this.finalizado()),
      ativo: indice === atual && !(indice === 3 && this.finalizado()),
    }));
  });

  readonly finalizado = computed(() => this.atendimento()?.status === 'Finalizado');
  readonly idadePaciente = computed(() => idade(this.atendimento()?.pacienteDataNascimento));
  readonly cpfPaciente = computed(() => cpfMascarado(this.atendimento()?.pacienteCpf));

  /** Formato que a clinica de fato exporta, usado apenas para orientar o medico. */
  readonly modoOperacao = computed(() => this.auth.usuario()?.modoOperacao ?? null);
  readonly visaoOficial = computed<Visao>(() => (this.modoOperacao() === 'Conector' ? 'nota' : 'estruturado'));

  /** Campos do prontuario: preenchidos pela IA durante a consulta e editaveis pelo medico (RF09/RF10). */
  readonly form = inject(FormBuilder).nonNullable.group({
    queixaPrincipal: [''],
    hda: [''],
    antecedentes: [''],
    exameFisico: [''],
    hipoteseDiagnostica: [''],
    cid10Sugerido: [''],
    conduta: [''],
  });

  /** Ultimo texto que a IA colocou em cada campo; diferente do atual = o medico editou. */
  private ultimoDaIa: Record<Campo, string> = this.textosVazios();
  private campoEmFoco: Campo | null = null;
  private trechosDesdeRascunho = 0;
  private ultimoRascunhoEm = 0;
  private proximaSequenciaDemonstracao = 0;
  private readonly enviosPendentes = new Set<Promise<unknown>>();
  private rascunhoEmAndamento: Promise<void> | null = null;
  private relogio: ReturnType<typeof setInterval> | null = null;
  private simulacao: ReturnType<typeof setTimeout> | null = null;

  private readonly destino: DestinoEscuta = {
    texto: (sequencia, texto) => this.registrarTexto(sequencia, texto),
    audio: (sequencia, audio, extensao) => this.registrarAudio(sequencia, audio, extensao),
    renovarSessao: () => firstValueFrom(this.transcricoes.iniciarSessao(this.id())),
    mudouDeModo: (_modo: ModoTranscricao, motivo: string) => this.aviso.set(motivo),
  };

  constructor() {
    // O id so existe apos o binding do parametro de rota, entao a carga inicial
    // acontece no effect; allowSignalWrites porque ela alimenta os signals da tela.
    effect(() => this.carregar(this.id()), { allowSignalWrites: true });

    for (const campo of CAMPOS) {
      this.form.controls[campo.chave].valueChanges.subscribe((valor) => this.registrarEdicao(campo.chave, valor));
    }

    // Fechar a aba no meio da consulta perderia a fala em andamento.
    const avisarSaida = (evento: BeforeUnloadEvent) => {
      if (this.escutaAtiva()) {
        evento.preventDefault();
      }
    };
    window.addEventListener('beforeunload', avisarSaida);

    inject(DestroyRef).onDestroy(() => {
      window.removeEventListener('beforeunload', avisarSaida);
      this.pararRelogio();
      this.pararSimulacao();
      this.escuta.descartar();
    });
  }

  // ---------- Carga ----------

  private carregar(id: string, silencioso = false): void {
    if (!silencioso) {
      this.carregando.set(true);
      this.erro.set(null);
    }

    this.atendimentos.obter(id).subscribe({
      next: (detalhe) => {
        this.atendimento.set(detalhe);
        this.sugestaoIa.set(detalhe.sugestaoIa);
        this.erroIA.set(detalhe.erroProcessamentoIA);
        this.dadosProtegidos.set(detalhe.dadosPessoaisProtegidos);
        this.segundosEscuta.set(detalhe.duracaoTranscricaoSegundos ?? 0);

        const linhas = (detalhe.transcricao ?? '').split('\n').filter((linha) => linha.trim().length > 0);
        this.trechos.set(linhas.map((texto, sequencia) => ({ sequencia, texto, estado: 'pronto' })));
        this.proximaSequenciaDemonstracao = linhas.length;

        this.preencherFormulario(detalhe.rascunho, detalhe.sugestaoIa);

        if (detalhe.status === 'Finalizado' || detalhe.status === 'Cancelado') {
          this.form.disable({ emitEvent: false });
        }

        this.carregando.set(false);
      },
      error: () => {
        if (!silencioso) {
          this.erro.set('Atendimento não encontrado.');
          this.carregando.set(false);
        }
      },
    });
  }

  private preencherFormulario(rascunho: RascunhoClinico, sugestao: RascunhoClinico | null): void {
    const origem = this.origensVazias();

    for (const { chave } of CAMPOS) {
      const valor = rascunho[chave] ?? '';
      const daIa = sugestao?.[chave] ?? '';
      this.ultimoDaIa[chave] = daIa;
      this.form.controls[chave].setValue(valor, { emitEvent: false });
      origem[chave] = valor.trim() === '' ? 'vazio' : valor.trim() === daIa.trim() ? 'ia' : 'medico';
    }

    this.origem.set(origem);
  }

  // ---------- Consentimento ----------

  aceitarConsentimento(): void {
    this.erro.set(null);

    this.atendimentos.registrarConsentimento(this.id()).subscribe({
      next: () => {
        // O backend carimba o horario do aceite; refleti-lo aqui evita ter de
        // recarregar a tela para o medico ver o registro que acabou de fazer.
        const agora = new Date().toISOString();
        this.atendimento.update((atual) =>
          atual
            ? {
                ...atual,
                status: 'EmConsulta',
                consentimentoTranscricao: true,
                consentimentoEm: atual.consentimentoEm ?? agora,
              }
            : atual,
        );
      },
      error: () => this.erro.set('Não foi possível registrar o consentimento.'),
    });
  }

  /** Revisar sem transcrever - paciente que recusa, teleconsulta por escrito, retomada de atendimento. */
  preencherManualmente(): void {
    this.atendimentos.iniciarRevisao(this.id()).subscribe({
      next: () => this.atualizarStatus('EmRevisao'),
      error: () => this.erro.set('Não foi possível avançar para a revisão.'),
    });
  }

  // ---------- Escuta ----------

  async iniciarEscuta(): Promise<void> {
    if (this.iniciandoEscuta() || this.escutaAtiva()) {
      return;
    }

    this.iniciandoEscuta.set(true);
    this.erro.set(null);
    this.aviso.set(null);

    try {
      const sessao = await firstValueFrom(this.transcricoes.iniciarSessao(this.id()));

      if (sessao.motivoModoAlternativo) {
        this.aviso.set(sessao.motivoModoAlternativo);
      }

      await this.escuta.iniciar(sessao, this.destino);
      this.iniciarRelogio();
    } catch (erro) {
      this.erro.set(this.mensagemDeErro(
        erro,
        erro instanceof DOMException
          ? 'Não foi possível acessar o microfone. Verifique a permissão do navegador.'
          : 'Não foi possível iniciar a transcrição.',
      ));
    } finally {
      this.iniciandoEscuta.set(false);
    }
  }

  pausarEscuta(): void {
    this.escuta.pausar();
  }

  retomarEscuta(): void {
    this.escuta.retomar();
  }

  /**
   * Fim da consulta: espera os ultimos trechos chegarem, fecha a escuta e
   * refaz o rascunho com tudo o que foi dito. O medico cai direto na revisao.
   */
  async encerrarConsulta(): Promise<void> {
    if (this.encerrando()) {
      return;
    }

    this.encerrando.set(true);
    this.erro.set(null);

    try {
      this.pararSimulacao();
      await this.escuta.parar();
      this.pararRelogio();
      await Promise.allSettled([...this.enviosPendentes]);

      // Uma atualizacao em curso pode nao ter visto as ultimas falas.
      if (this.rascunhoEmAndamento) {
        await this.rascunhoEmAndamento;
      }

      await firstValueFrom(this.transcricoes.encerrar(this.id(), this.segundosEscuta()));
      this.atualizarStatus('EmRevisao');

      if (this.trechos().some((trecho) => trecho.estado === 'pronto') &&
          (this.trechosDesdeRascunho > 0 || this.rascunhoAtualizadoEm() === null)) {
        await this.gerarRascunho();
      }
    } catch (erro) {
      this.erro.set(this.mensagemDeErro(erro, 'Não foi possível encerrar a consulta.'));
    } finally {
      this.encerrando.set(false);
    }
  }

  /** Volta a ouvir depois de encerrada - o medico lembrou de algo antes de assinar. */
  voltarParaConsulta(): void {
    this.aceitarConsentimento();
  }

  alternarTranscricao(): void {
    this.mostrarTranscricao.update((valor) => !valor);
  }

  private registrarTexto(sequencia: number, texto: string): void {
    this.colocarTrecho({ sequencia, texto, estado: 'transcrevendo' });
    this.acompanhar(firstValueFrom(this.transcricoes.registrarTrecho(this.id(), sequencia, texto)), sequencia);
  }

  private registrarAudio(sequencia: number, audio: Blob, extensao: string): void {
    this.colocarTrecho({ sequencia, texto: '', estado: 'transcrevendo' });
    this.acompanhar(firstValueFrom(this.transcricoes.transcreverTrecho(this.id(), sequencia, audio, extensao)), sequencia);
  }

  /** Cada envio vira um trecho na tela; o encerramento espera todos terminarem. */
  private acompanhar(envio: Promise<{ texto: string; dadosProtegidos: number } | null>, sequencia: number): void {
    const acompanhado = envio
      .then((trecho) => {
        if (!trecho) {
          // Trecho sem fala: some da tela sem deixar rastro.
          this.trechos.update((lista) => lista.filter((item) => item.sequencia !== sequencia));
          return;
        }

        this.colocarTrecho({ sequencia, texto: trecho.texto, estado: 'pronto' });
        this.dadosProtegidos.update((total) => total + trecho.dadosProtegidos);
        this.trechosDesdeRascunho++;
        this.avaliarAtualizacao();
      })
      .catch(() => this.colocarTrecho({ sequencia, texto: '', estado: 'falhou' }))
      .finally(() => this.enviosPendentes.delete(acompanhado));

    this.enviosPendentes.add(acompanhado);
  }

  private colocarTrecho(trecho: TrechoNaTela): void {
    this.trechos.update((lista) => {
      const outros = lista.filter((item) => item.sequencia !== trecho.sequencia);
      return [...outros, trecho].sort((a, b) => a.sequencia - b.sequencia);
    });

    // A fala mais recente fica sempre a vista, como numa legenda.
    setTimeout(() => {
      const lista = this.listaTranscricao()?.nativeElement;
      lista?.scrollTo({ top: lista.scrollHeight, behavior: 'smooth' });
    });
  }

  // ---------- Demonstracao ----------

  /** Envia as falas da consulta ficticia uma a uma, como se viessem do provedor de transcricao. */
  async simularConsulta(): Promise<void> {
    if (this.simulando() || this.escutaAtiva()) {
      return;
    }

    this.erro.set(null);

    try {
      const { trechos } = await firstValueFrom(this.transcricoes.consultaDemonstracao());
      this.simulando.set(true);
      this.iniciarRelogio();

      const proxima = (indice: number) => {
        if (!this.simulando()) {
          return;
        }

        if (indice >= trechos.length) {
          void this.encerrarConsulta();
          return;
        }

        this.registrarTexto(this.proximaSequenciaDemonstracao++, trechos[indice]!);
        this.simulacao = setTimeout(() => proxima(indice + 1), INTERVALO_DEMONSTRACAO_MS);
      };

      proxima(0);
    } catch {
      this.erro.set('Não foi possível carregar a consulta de demonstração.');
    }
  }

  private pararSimulacao(): void {
    if (this.simulacao !== null) {
      clearTimeout(this.simulacao);
      this.simulacao = null;
    }
    this.simulando.set(false);
  }

  // ---------- Rascunho ao vivo ----------

  /** Refaz o rascunho quando ja ha fala nova suficiente para mudar o formulario. */
  private avaliarAtualizacao(): void {
    if (this.gerandoRascunho() || this.trechosDesdeRascunho === 0 || this.etapa() !== 'consulta') {
      return;
    }

    const passouIntervalo = Date.now() - this.ultimoRascunhoEm >= INTERVALO_ATUALIZACAO_MS;
    if (this.trechosDesdeRascunho >= TRECHOS_PARA_ATUALIZAR || passouIntervalo) {
      void this.gerarRascunho();
    }
  }

  /** Pede o rascunho sobre tudo o que foi dito ate agora. Tambem e o "tentar de novo" da revisao. */
  gerarRascunho(): Promise<void> {
    if (this.rascunhoEmAndamento) {
      return this.rascunhoEmAndamento;
    }

    this.rascunhoEmAndamento = this.pedirRascunho().finally(() => (this.rascunhoEmAndamento = null));
    return this.rascunhoEmAndamento;
  }

  private async pedirRascunho(): Promise<void> {
    this.gerandoRascunho.set(true);
    const considerados = this.trechosDesdeRascunho;

    try {
      const rascunho = await firstValueFrom(this.transcricoes.gerarRascunho(this.id()));
      this.trechosDesdeRascunho = Math.max(0, this.trechosDesdeRascunho - considerados);
      this.ultimoRascunhoEm = Date.now();
      this.rascunhoAtualizadoEm.set(new Date());
      this.erroIA.set(null);
      this.aplicarRascunho(rascunho);
    } catch (erro) {
      this.ultimoRascunhoEm = Date.now();
      this.erroIA.set(this.mensagemDeErro(erro, 'A IA não respondeu. Os campos seguem editáveis.'));
    } finally {
      this.gerandoRascunho.set(false);
    }
  }

  /**
   * Leva a sugestao da IA ao formulario sem atropelar o medico: campo que ele
   * editou nao e sobrescrito - a novidade aparece como sugestao para aceitar -
   * e o campo em que ele esta digitando espera a proxima atualizacao.
   */
  private aplicarRascunho(rascunho: RascunhoClinico): void {
    this.sugestaoIa.set(rascunho);
    const origem = { ...this.origem() };
    const sugestoes = { ...this.sugestoes() };
    const mudados = new Set<Campo>();

    for (const { chave } of CAMPOS) {
      const novo = (rascunho[chave] ?? '').trim();
      const anterior = this.ultimoDaIa[chave].trim();

      if (origem[chave] === 'medico') {
        if (novo && novo !== anterior && novo !== this.form.controls[chave].value.trim()) {
          sugestoes[chave] = novo;
        }
        this.ultimoDaIa[chave] = novo;
        continue;
      }

      if (this.campoEmFoco === chave || novo === this.form.controls[chave].value.trim()) {
        continue;
      }

      this.ultimoDaIa[chave] = novo;
      this.form.controls[chave].setValue(novo, { emitEvent: false });
      origem[chave] = novo ? 'ia' : 'vazio';
      delete sugestoes[chave];

      if (novo) {
        mudados.add(chave);
      }
    }

    this.origem.set(origem);
    this.sugestoes.set(sugestoes);
    this.destacar(mudados);
  }

  aceitarSugestao(campo: Campo): void {
    const valor = this.sugestoes()[campo];
    if (valor === undefined) {
      return;
    }

    this.ultimoDaIa[campo] = valor;
    this.form.controls[campo].setValue(valor, { emitEvent: false });
    this.origem.update((atual) => ({ ...atual, [campo]: 'ia' }));
    this.descartarSugestao(campo);
    this.destacar(new Set([campo]));
  }

  descartarSugestao(campo: Campo): void {
    this.sugestoes.update(({ [campo]: _, ...resto }) => resto);
  }

  focar(campo: Campo | null): void {
    this.campoEmFoco = campo;
  }

  private registrarEdicao(campo: Campo, valor: string): void {
    const atual = valor.trim();
    const daIa = this.ultimoDaIa[campo].trim();
    const origem: Origem = atual === '' ? 'vazio' : atual === daIa ? 'ia' : 'medico';

    if (this.origem()[campo] !== origem) {
      this.origem.update((anterior) => ({ ...anterior, [campo]: origem }));
    }
  }

  private destacar(campos: Set<Campo>): void {
    if (campos.size === 0) {
      return;
    }

    this.destacados.set(campos);
    setTimeout(() => this.destacados.set(new Set()), 1_800);
  }

  alternarSugestao(): void {
    this.mostrarSugestao.update((valor) => !valor);
  }

  // ---------- Confirmacao ----------

  confirmar(): void {
    if (this.salvando() || this.etapa() !== 'revisao') {
      return;
    }

    this.salvando.set(true);
    this.erro.set(null);

    this.atendimentos.confirmar(this.id(), this.conteudoRevisado()).subscribe({
      next: () => {
        this.salvando.set(false);
        this.atualizarStatus('Finalizado');
        this.form.disable({ emitEvent: false });
      },
      error: (erro: { status?: number; error?: { erro?: string } }) => {
        this.salvando.set(false);

        // 409 e o registro ja assinado se protegendo: recarregar mostra o
        // estado real em vez de deixar o medico tentando salvar de novo.
        if (erro.status === 409) {
          this.erro.set(erro.error?.erro ?? 'Este atendimento já foi encerrado.');
          this.carregar(this.id(), true);
          return;
        }

        this.erro.set('Não foi possível finalizar o atendimento.');
      },
    });
  }

  mostrarVisao(visao: Visao): void {
    this.visao.set(visao);

    if (visao === 'nota') {
      this.carregarNota();
    }
  }

  async copiarNota(): Promise<void> {
    const conteudo = this.nota();
    if (!conteudo) {
      return;
    }

    try {
      await navigator.clipboard.writeText(conteudo);
      this.notaCopiada.set(true);
    } catch {
      this.erro.set('Não foi possível copiar. Selecione o texto e copie manualmente.');
    }
  }

  /** Reenvia a nota ao EMR quando o envio automático falhou (RF19). */
  reenviarAoEmr(): void {
    if (this.exportando()) {
      return;
    }

    this.exportando.set(true);
    this.erro.set(null);
    this.resultadoExportacao.set(null);

    this.atendimentos.exportar(this.id()).subscribe({
      next: (resultado) => {
        this.exportando.set(false);
        this.resultadoExportacao.set(resultado);

        // O status e as tentativas mudam no servidor a cada envio.
        this.atendimentos.obterNota(this.id()).subscribe({
          next: (nota) => this.notaGravada.set(nota),
        });
      },
      error: () => {
        this.exportando.set(false);
        this.erro.set('Não foi possível reenviar a nota.');
      },
    });
  }

  baixarPdf(): void {
    if (this.baixandoPdf()) {
      return;
    }

    this.baixandoPdf.set(true);
    this.erro.set(null);

    this.atendimentos.baixarNotaPdf(this.id()).subscribe({
      next: (arquivo) => {
        this.baixandoPdf.set(false);
        const url = URL.createObjectURL(arquivo);
        const link = document.createElement('a');
        link.href = url;
        link.download = `nota-${this.atendimento()?.pacienteNome ?? 'paciente'}.pdf`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => {
        this.baixandoPdf.set(false);
        this.erro.set('Não foi possível gerar o PDF.');
      },
    });
  }

  voltarParaLista(): void {
    this.router.navigate(['/atendimentos']);
  }

  private carregarNota(): void {
    this.carregandoNota.set(true);
    this.notaCopiada.set(false);
    this.erro.set(null);

    // Depois de encerrado, a nota gravada e a fonte da verdade: e o texto que
    // foi (ou sera) enviado ao EMR. Durante a revisao a previa acompanha o que
    // o medico esta digitando.
    if (this.etapa() === 'encerrado') {
      this.atendimentos.obterNota(this.id()).subscribe({
        next: (nota) => {
          this.notaGravada.set(nota);
          this.nota.set(nota.conteudo);
          this.carregandoNota.set(false);
        },
        error: () => {
          this.carregandoNota.set(false);
          this.erro.set('Não foi possível carregar a nota clínica.');
        },
      });
      return;
    }

    this.atendimentos.preverNota(this.conteudoRevisado()).subscribe({
      next: ({ conteudo }) => {
        this.nota.set(conteudo);
        this.carregandoNota.set(false);
      },
      error: () => {
        this.carregandoNota.set(false);
        this.erro.set('Não foi possível gerar a nota clínica.');
      },
    });
  }

  /** Campos vazios viram null: o backend distingue "nao preenchido" de string vazia. */
  private conteudoRevisado(): RascunhoClinico {
    const valores = this.form.getRawValue();

    return {
      queixaPrincipal: valores.queixaPrincipal || null,
      hda: valores.hda || null,
      antecedentes: valores.antecedentes || null,
      exameFisico: valores.exameFisico || null,
      hipoteseDiagnostica: valores.hipoteseDiagnostica || null,
      cid10Sugerido: valores.cid10Sugerido || null,
      conduta: valores.conduta || null,
    };
  }

  // ---------- Utilitarios ----------

  private iniciarRelogio(): void {
    this.pararRelogio();
    this.segundosEmSilencio.set(0);

    this.relogio = setInterval(() => {
      this.agora.set(Date.now());

      if (this.ouvindo()) {
        this.segundosEscuta.update((s) => s + 1);
        this.segundosEmSilencio.update((s) => (this.nivelAudio() < 0.04 ? s + 1 : 0));
      }

      // Fala que chegou devagar tambem atualiza o formulario, sem esperar a cota de trechos.
      this.avaliarAtualizacao();
    }, 1000);
  }

  private pararRelogio(): void {
    if (this.relogio !== null) {
      clearInterval(this.relogio);
      this.relogio = null;
    }
  }

  private atualizarStatus(status: AtendimentoDetalhe['status']): void {
    this.atendimento.update((atual) => (atual ? { ...atual, status } : atual));
  }

  private mensagemDeErro(erro: unknown, padrao: string): string {
    if (erro instanceof HttpErrorResponse) {
      const mensagem = (erro.error as { erro?: string } | null)?.erro;
      if (mensagem) {
        return mensagem;
      }
    }
    return padrao;
  }

  private origensVazias(): Record<Campo, Origem> {
    return Object.fromEntries(CAMPOS.map(({ chave }) => [chave, 'vazio'])) as Record<Campo, Origem>;
  }

  private textosVazios(): Record<Campo, string> {
    return Object.fromEntries(CAMPOS.map(({ chave }) => [chave, ''])) as Record<Campo, string>;
  }
}
