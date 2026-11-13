import { NgTemplateOutlet } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AtendimentoDetalhe } from '../../core/models/atendimento.model';
import {
  AlertaOcupacional,
  CampoOcupacional,
  Esclarecimento,
  EstadoOcupacional,
  ROTULO_MODO,
  SecaoOcupacional,
} from '../../core/models/ocupacional.model';
import { Falante, ModoTranscricao, SessaoTranscricao } from '../../core/models/transcricao.model';
import { AppConfigService } from '../../core/services/app-config.service';
import { AtendimentoService } from '../../core/services/atendimento.service';
import { DestinoEscuta, EscutaConsultaService } from '../../core/services/escuta-consulta.service';
import { OcupacionalService } from '../../core/services/ocupacional.service';
import { TranscricaoService } from '../../core/services/transcricao.service';
import { AvatarComponent } from '../../shared/avatar.component';
import { IconeComponent } from '../../shared/icone.component';
import { cpfMascarado, idade } from '../../shared/pessoa';

interface FalaNaTela {
  sequencia: number;
  texto: string;
  falante: Falante;
  /** A IA já considerou esta fala. */
  enviada: boolean;
}

/** Com este tanto de falas novas o lote vai à IA na hora (pergunta e resposta)... */
const FALAS_POR_LOTE = 2;
/** ...e uma fala sozinha espera este tempo pela próxima antes de ir. */
const ESPERA_DO_LOTE_MS = 4_000;
/** Quantas falas já consideradas acompanham as novas, para a IA entender respostas curtas. */
const FALAS_DE_CONTEXTO = 6;
/** Ritmo das falas da conversa de demonstração. */
const INTERVALO_DEMONSTRACAO_MS = 1_800;
const ROTULO_FALANTE: Record<Falante, string> = {
  Indefinido: 'Voz',
  Medico: 'Médico',
  Paciente: 'Paciente',
  Acompanhante: 'Acompanhante',
};

/**
 * Exame ocupacional nas três formas. Na forma atual o médico preenche seção a
 * seção; nas duas com IA a conversa preenche os campos enquanto acontece, e o
 * médico confere e corrige. A conversa fica só na memória desta tela.
 */
@Component({
  selector: 'app-ocupacional',
  standalone: true,
  imports: [RouterLink, NgTemplateOutlet, IconeComponent, AvatarComponent],
  templateUrl: './ocupacional.component.html',
  styleUrl: './ocupacional.component.scss',
})
export class OcupacionalComponent {
  private readonly atendimentos = inject(AtendimentoService);
  private readonly ocupacional = inject(OcupacionalService);
  private readonly transcricoes = inject(TranscricaoService);
  private readonly escuta = inject(EscutaConsultaService);

  /** Modo demonstração: conversa fictícia no lugar do microfone. */
  readonly demonstracao = inject(AppConfigService).demonstracao;
  readonly simulando = signal(false);
  private simulacao: ReturnType<typeof setTimeout> | null = null;

  /** Vem do parâmetro de rota. */
  readonly id = input.required<string>();

  readonly rotuloModo = ROTULO_MODO;
  readonly rotuloFalante = ROTULO_FALANTE;
  readonly cpfMascarado = cpfMascarado;

  readonly carregando = signal(true);
  readonly atendimento = signal<AtendimentoDetalhe | null>(null);
  readonly estado = signal<EstadoOcupacional | null>(null);
  readonly erro = signal<string | null>(null);
  readonly erroIA = signal<string | null>(null);
  readonly aviso = signal<string | null>(null);

  readonly consentido = signal(false);
  readonly aceite = signal(false);
  readonly atencaoVista = signal(false);
  readonly noResumo = signal(false);
  readonly salvando = signal(false);

  /** Seção aberta na navegação passo a passo (forma atual e IA guiada). */
  readonly indice = signal(0);
  /** Seções abertas no acordeão do Modo Escriba. */
  readonly abertas = signal<ReadonlySet<string>>(new Set());
  /** O médico tentou avançar com pendências: elas ficam destacadas. */
  readonly cobrando = signal(false);
  /** Campos que a IA acabou de preencher, para piscar na tela. */
  readonly destacados = signal<ReadonlySet<string>>(new Set());

  // ---------- Conversa ----------

  readonly estadoEscuta = this.escuta.estado;
  readonly nivelAudio = this.escuta.nivel;
  readonly falaEmAndamento = this.escuta.falaEmAndamento;
  readonly iniciandoEscuta = signal(false);
  readonly conversaConcluida = signal(false);
  readonly falas = signal<FalaNaTela[]>([]);
  readonly esclarecimentos = signal<Esclarecimento[]>([]);
  readonly processando = signal(false);
  private readonly listaFalas = viewChild<ElementRef<HTMLElement>>('listaFalas');

  private proximaSequencia = 0;
  private esperaDoLote: ReturnType<typeof setTimeout> | null = null;
  private envio: Promise<void> | null = null;

  readonly comVoz = computed(() => this.estado() !== null && this.estado()!.modo !== 'Tradicional');
  readonly passoAPasso = computed(() => this.estado()?.modo !== 'Escriba');
  readonly ouvindo = computed(() => this.estadoEscuta() === 'ouvindo' || this.simulando());
  readonly escutaAtiva = computed(() => this.estadoEscuta() !== 'parada' || this.simulando());

  readonly etapa = computed<'consentimento' | 'prontuario' | 'resumo'>(() => {
    const estado = this.estado();
    if (!estado) {
      return 'prontuario';
    }
    if (estado.concluido || this.noResumo()) {
      return 'resumo';
    }
    return this.comVoz() && !this.consentido() ? 'consentimento' : 'prontuario';
  });

  readonly secaoAtual = computed(() => this.estado()?.secoes[this.indice()] ?? null);
  readonly totalRespondidas = computed(() => this.estado()?.secoes.reduce((soma, s) => soma + s.respondidas, 0) ?? 0);
  readonly totalObrigatorias = computed(() => this.estado()?.secoes.reduce((soma, s) => soma + s.obrigatorias, 0) ?? 0);
  readonly secoesComPendencia = computed(() => this.estado()?.secoes.filter((s) => s.pendentes.length > 0) ?? []);
  readonly criticos = computed(() => this.estado()?.alertas.filter((a) => a.critico) ?? []);
  readonly avisos = computed(() => this.estado()?.alertas.filter((a) => !a.critico) ?? []);

  readonly indicadorConversa = computed(() => {
    if (this.conversaConcluida()) {
      return 'Conversa concluída';
    }
    if (this.simulando()) {
      return 'Demonstração: conversa fictícia…';
    }
    switch (this.estadoEscuta()) {
      case 'ouvindo': return 'Ouvindo…';
      case 'pausada': return 'Conversa pausada — microfone desligado';
      case 'conectando': return 'Conectando…';
      case 'reconectando': return 'Reconectando…';
      default: return 'Aguardando início';
    }
  });

  readonly idadePaciente = computed(() => {
    const anos = idade(this.atendimento()?.pacienteDataNascimento ?? null);
    return anos === null ? null : `${anos} anos`;
  });

  private readonly destino: DestinoEscuta = {
    texto: (sequencia, texto) => this.receberFala(sequencia, texto),
    audio: (sequencia, audio, extensao) => void this.transcreverTrecho(sequencia, audio, extensao),
    renovarSessao: () => firstValueFrom(this.transcricoes.iniciarSessao(this.id())),
    mudouDeModo: (_modo: ModoTranscricao, motivo: string) => this.aviso.set(motivo),
  };

  constructor() {
    effect(() => this.carregar(this.id()), { allowSignalWrites: true });

    const avisarSaida = (evento: BeforeUnloadEvent) => {
      if (this.escutaAtiva()) {
        evento.preventDefault();
      }
    };
    window.addEventListener('beforeunload', avisarSaida);

    inject(DestroyRef).onDestroy(() => {
      window.removeEventListener('beforeunload', avisarSaida);
      this.cancelarEspera();
      this.pararSimulacao();
      this.escuta.descartar();
    });
  }

  private carregar(id: string): void {
    this.carregando.set(true);
    this.erro.set(null);

    Promise.all([firstValueFrom(this.atendimentos.obter(id)), firstValueFrom(this.ocupacional.obter(id))])
      .then(([atendimento, estado]) => {
        this.atendimento.set(atendimento);
        this.estado.set(estado);
        this.consentido.set(atendimento.consentimentoTranscricao);
        this.abertas.set(new Set([estado.secoes[0]?.id ?? '']));
      })
      .catch(() => this.erro.set('Não foi possível carregar o exame ocupacional.'))
      .finally(() => this.carregando.set(false));
  }

  // ---------- Consentimento ----------

  async confirmarConsentimento(): Promise<void> {
    if (!this.aceite()) {
      return;
    }

    try {
      await firstValueFrom(this.atendimentos.registrarConsentimento(this.id()));
      this.consentido.set(true);
    } catch {
      this.erro.set('Não foi possível registrar o consentimento.');
    }
  }

  // ---------- Formulário ----------

  /** Na forma atual o condicional aparece desabilitado; com IA, só depois de liberado. */
  visivel(campo: CampoOcupacional): boolean {
    return campo.liberado || !this.comVoz();
  }

  pendente(campo: CampoOcupacional): boolean {
    return this.cobrando() && campo.liberado && campo.obrigatorio && campo.valor === null;
  }

  alertasDoCampo(campo: CampoOcupacional): AlertaOcupacional[] {
    return this.estado()?.alertas.filter((alerta) => alerta.campos.includes(campo.id)) ?? [];
  }

  async responder(campo: CampoOcupacional, valor: string): Promise<void> {
    const limpo = valor.trim();
    if (limpo === (campo.valor ?? '') || this.estado()?.concluido) {
      return;
    }

    this.erro.set(null);

    try {
      this.aplicar(await firstValueFrom(this.ocupacional.responder(this.id(), campo.id, limpo || null)));
      this.esclarecimentos.update((lista) => lista.filter((item) => item.campo !== campo.id));
    } catch (erro) {
      this.erro.set(this.mensagem(erro, `Não foi possível salvar "${campo.rotulo}".`));
    }
  }

  alternarSecao(secao: SecaoOcupacional): void {
    this.abertas.update((abertas) => {
      const novas = new Set(abertas);
      if (!novas.delete(secao.id)) {
        novas.add(secao.id);
      }
      return novas;
    });
  }

  irPara(indice: number): void {
    // Na IA guiada cada seção tem a sua conversa: mudar de seção desliga o microfone.
    if (indice !== this.indice() && this.estadoEscuta() === 'ouvindo') {
      this.escuta.pausar();
      void this.enviarFalas();
    }

    this.cobrando.set(false);
    this.noResumo.set(false);
    this.indice.set(indice);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  voltar(): void {
    if (this.indice() > 0) {
      this.irPara(this.indice() - 1);
    }
  }

  /** Só avança com a seção completa; as pendências ficam destacadas e listadas. */
  avancar(): void {
    const estado = this.estado();
    const secao = this.secaoAtual();
    if (!estado || !secao) {
      return;
    }

    if (secao.pendentes.length > 0) {
      this.cobrando.set(true);
      return;
    }

    if (this.indice() < estado.secoes.length - 1) {
      this.irPara(this.indice() + 1);
    } else {
      this.abrirResumo();
    }
  }

  /** Encerrar a consulta no Modo Escriba: valida todas as seções de uma vez. */
  encerrar(): void {
    const comPendencia = this.secoesComPendencia();
    if (comPendencia.length > 0) {
      this.cobrando.set(true);
      this.abertas.set(new Set(comPendencia.map((secao) => secao.id)));
      return;
    }

    this.abrirResumo();
  }

  private abrirResumo(): void {
    if (this.escutaAtiva()) {
      void this.concluirConversa();
    }
    this.cobrando.set(false);
    this.noResumo.set(true);
    window.scrollTo({ top: 0 });
  }

  revisar(secaoId?: string): void {
    const estado = this.estado();
    if (!estado || estado.concluido) {
      return;
    }

    const indice = Math.max(0, estado.secoes.findIndex((secao) => secao.id === secaoId));
    this.noResumo.set(false);
    this.cobrando.set(true);
    this.indice.set(indice);
    this.abertas.set(new Set([estado.secoes[indice]!.id]));
  }

  secaoDoCampo(campoId: string): string | undefined {
    return this.estado()?.secoes.find((secao) => secao.campos.some((campo) => campo.id === campoId))?.id;
  }

  async justificar(alerta: AlertaOcupacional, texto: string): Promise<void> {
    if (texto.trim() === (alerta.justificativa ?? '')) {
      return;
    }

    this.erro.set(null);

    try {
      this.aplicar(await firstValueFrom(this.ocupacional.justificar(this.id(), alerta.chave, texto.trim() || null)));
    } catch (erro) {
      this.erro.set(this.mensagem(erro, 'Não foi possível salvar a justificativa.'));
    }
  }

  async salvar(): Promise<void> {
    if (this.salvando() || !this.estado()?.podeConcluir) {
      return;
    }

    this.salvando.set(true);
    this.erro.set(null);

    try {
      this.aplicar(await firstValueFrom(this.ocupacional.concluir(this.id())));
    } catch (erro) {
      this.erro.set(this.mensagem(erro, 'Não foi possível salvar o atendimento.'));
    } finally {
      this.salvando.set(false);
    }
  }

  // ---------- Conversa ----------

  async iniciarConversa(): Promise<void> {
    if (this.iniciandoEscuta() || this.escutaAtiva() || this.conversaConcluida()) {
      return;
    }

    this.iniciandoEscuta.set(true);
    this.erro.set(null);
    this.aviso.set(null);

    try {
      const sessao: SessaoTranscricao = await firstValueFrom(this.transcricoes.iniciarSessao(this.id()));
      if (sessao.motivoModoAlternativo) {
        this.aviso.set(sessao.motivoModoAlternativo);
      }

      // A conversa vive só nesta tela: a numeração das falas é daqui.
      await this.escuta.iniciar({ ...sessao, proximaSequencia: this.proximaSequencia }, this.destino);
    } catch (erro) {
      this.erro.set(this.mensagem(
        erro,
        erro instanceof DOMException
          ? 'Não foi possível acessar o microfone. Você pode continuar preenchendo manualmente.'
          : 'Não foi possível iniciar a conversa. Você pode continuar preenchendo manualmente.',
      ));
    } finally {
      this.iniciandoEscuta.set(false);
    }
  }

  /** Pausar para de enviar áudio: conversa que não deve entrar no prontuário, e custo que não corre. */
  alternarPausa(): void {
    if (this.estadoEscuta() === 'ouvindo') {
      this.escuta.pausar();
      void this.enviarFalas();
    } else {
      this.escuta.retomar();
    }
  }

  async concluirConversa(): Promise<void> {
    this.pararSimulacao();
    await this.escuta.parar();
    await this.enviarFalas();
    this.conversaConcluida.set(true);
  }

  /** Envia as falas da conversa fictícia uma a uma, como se viessem do microfone. */
  async simularConversa(): Promise<void> {
    if (this.escutaAtiva() || this.conversaConcluida()) {
      return;
    }

    this.erro.set(null);

    try {
      const { falas } = await firstValueFrom(this.ocupacional.conversaDemonstracao());
      this.simulando.set(true);

      const proxima = (indice: number) => {
        if (!this.simulando()) {
          return;
        }
        if (indice >= falas.length) {
          void this.concluirConversa();
          return;
        }

        this.receberFala(this.proximaSequencia, falas[indice]!);
        this.simulacao = setTimeout(() => proxima(indice + 1), INTERVALO_DEMONSTRACAO_MS);
      };

      proxima(0);
    } catch {
      this.erro.set('Não foi possível carregar a conversa de demonstração.');
    }
  }

  private pararSimulacao(): void {
    if (this.simulacao !== null) {
      clearTimeout(this.simulacao);
      this.simulacao = null;
    }
    this.simulando.set(false);
  }

  dispensar(esclarecimento: Esclarecimento): void {
    this.esclarecimentos.update((lista) => lista.filter((item) => item !== esclarecimento));
  }

  private receberFala(sequencia: number, texto: string): void {
    this.proximaSequencia = Math.max(this.proximaSequencia, sequencia + 1);
    this.falas.update((lista) =>
      [...lista.filter((fala) => fala.sequencia !== sequencia), { sequencia, texto, falante: 'Indefinido' as const, enviada: false }]
        .sort((a, b) => a.sequencia - b.sequencia));

    setTimeout(() => {
      const lista = this.listaFalas()?.nativeElement;
      lista?.scrollTo({ top: lista.scrollHeight, behavior: 'smooth' });
    });

    // Pergunta e resposta vão juntas; uma fala sozinha espera um pouco pela próxima.
    this.cancelarEspera();
    if (this.falas().filter((fala) => !fala.enviada).length >= FALAS_POR_LOTE) {
      void this.enviarFalas();
    } else {
      this.esperaDoLote = setTimeout(() => void this.enviarFalas(), ESPERA_DO_LOTE_MS);
    }
  }

  private async transcreverTrecho(sequencia: number, audio: Blob, extensao: string): Promise<void> {
    this.proximaSequencia = Math.max(this.proximaSequencia, sequencia + 1);

    try {
      const { texto } = await firstValueFrom(this.ocupacional.transcrever(this.id(), audio, extensao));
      if (texto.trim()) {
        this.receberFala(sequencia, texto.trim());
      }
    } catch (erro) {
      this.erroIA.set(this.mensagem(erro, 'Um trecho da conversa não pôde ser transcrito.'));
    }
  }

  /** Um envio por vez: o que chegar durante a chamada vai no lote seguinte. */
  private enviarFalas(): Promise<void> {
    this.cancelarEspera();
    this.envio = (this.envio ?? Promise.resolve()).then(() => this.enviarLote());
    return this.envio;
  }

  private async enviarLote(): Promise<void> {
    const novas = this.falas().filter((fala) => !fala.enviada);
    if (novas.length === 0) {
      return;
    }

    const contexto = this.falas().filter((fala) => fala.enviada).slice(-FALAS_DE_CONTEXTO);
    const emFoco = this.estado()?.modo === 'Guiada' ? this.secaoAtual()?.id ?? null : null;
    this.processando.set(true);

    try {
      const resultado = await firstValueFrom(this.ocupacional.processarFalas(
        this.id(),
        novas.map(({ sequencia, texto }) => ({ sequencia, texto })),
        contexto.map(({ sequencia, texto, falante }) => ({ sequencia, texto, falante: falante === 'Indefinido' ? null : falante })),
        emFoco,
      ));

      this.erroIA.set(null);
      this.aplicar(resultado.estado);

      // Lote só de confirmações não foi à IA: fica para ir com as próximas falas.
      if (!resultado.consideradas) {
        return;
      }

      const enviadas = new Set(novas.map((fala) => fala.sequencia));
      this.falas.update((lista) => lista.map((fala) => enviadas.has(fala.sequencia)
        ? { ...fala, enviada: true, falante: resultado.falantes[fala.sequencia] ?? fala.falante }
        : fala));

      this.mostrarPreenchidos(resultado.preenchidos);
      this.esclarecimentos.update((lista) => [
        ...lista.filter((item) => !resultado.esclarecimentos.some((novo) => novo.campo === item.campo)),
        ...resultado.esclarecimentos,
      ]);
    } catch (erro) {
      // As falas continuam marcadas como não enviadas e vão de novo no próximo lote.
      this.erroIA.set(this.mensagem(erro, 'A IA não respondeu. Os campos seguem editáveis.'));
    } finally {
      this.processando.set(false);
    }
  }

  /** Abre a seção de cada campo que a IA preencheu e o destaca por um instante. */
  private mostrarPreenchidos(campos: string[]): void {
    if (campos.length === 0) {
      return;
    }

    const secoes = campos.map((campo) => this.secaoDoCampo(campo)).filter((id): id is string => !!id);
    this.abertas.update((abertas) => new Set([...abertas, ...secoes]));
    this.destacados.set(new Set(campos));
    setTimeout(() => this.destacados.set(new Set()), 2_500);
  }

  private aplicar(estado: EstadoOcupacional): void {
    this.estado.set(estado);

    // Pergunta de esclarecimento some quando o campo recebe resposta.
    const respondidos = new Set(estado.secoes.flatMap((secao) => secao.campos).filter((c) => c.valor !== null).map((c) => c.id));
    this.esclarecimentos.update((lista) => lista.filter((item) => !respondidos.has(item.campo)));
  }

  private cancelarEspera(): void {
    if (this.esperaDoLote !== null) {
      clearTimeout(this.esperaDoLote);
      this.esperaDoLote = null;
    }
  }

  private mensagem(erro: unknown, padrao: string): string {
    return erro instanceof HttpErrorResponse && typeof erro.error?.erro === 'string' ? erro.error.erro : padrao;
  }
}
