import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  AtendimentoDetalhe,
  CLASSE_STATUS,
  ROTULO_STATUS,
} from '../../core/models/atendimento.model';
import { NotaExportavel, ResultadoExportacao } from '../../core/models/exportacao.model';
import { RascunhoClinico } from '../../core/models/rascunho-clinico.model';
import { AtendimentoService } from '../../core/services/atendimento.service';
import { AuthService } from '../../core/services/auth.service';
import { GravacaoAudioService } from '../../core/services/gravacao-audio.service';

type Etapa = 'consentimento' | 'gravacao' | 'processando' | 'revisao' | 'encerrado';

/**
 * Os dois formatos de registro previstos na especificacao: o prontuario nativo
 * estruturado (Modalidade A) e a nota de texto corrido enviada ao EMR externo
 * (Modalidade B). O medico pode ver o atendimento nos dois, mesmo que a clinica
 * so exporte um deles.
 */
type Visao = 'estruturado' | 'nota';

@Component({
  selector: 'app-atendimento',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './atendimento.component.html',
  styleUrl: './atendimento.component.scss',
})
export class AtendimentoComponent {
  private readonly atendimentos = inject(AtendimentoService);
  private readonly gravacaoAudio = inject(GravacaoAudioService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  /** Vem do parametro de rota via withComponentInputBinding(). */
  readonly id = input.required<string>();

  readonly carregando = signal(true);
  readonly atendimento = signal<AtendimentoDetalhe | null>(null);
  readonly erro = signal<string | null>(null);
  readonly aviso = signal<string | null>(null);
  readonly salvando = signal(false);
  readonly segundosGravados = signal(0);
  readonly visao = signal<Visao>('estruturado');
  readonly nota = signal<string | null>(null);
  readonly carregandoNota = signal(false);
  readonly notaCopiada = signal(false);
  readonly enviandoAudio = signal(false);
  readonly transcricao = signal<string | null>(null);
  readonly erroIA = signal<string | null>(null);
  readonly mostrarTranscricao = signal(false);
  readonly notaGravada = signal<NotaExportavel | null>(null);
  readonly exportando = signal(false);
  readonly resultadoExportacao = signal<ResultadoExportacao | null>(null);
  readonly baixandoPdf = signal(false);
  readonly reprocessando = signal(false);

  readonly gravando = this.gravacaoAudio.gravando;
  readonly rotuloStatus = ROTULO_STATUS;
  readonly classeStatus = CLASSE_STATUS;

  private cronometro: ReturnType<typeof setInterval> | null = null;
  private consulta: ReturnType<typeof setInterval> | null = null;

  /** A etapa exibida deriva do status persistido, para que o atendimento possa ser retomado. */
  readonly etapa = computed<Etapa>(() => {
    const status = this.atendimento()?.status;

    switch (status) {
      case 'EmGravacao':
        return 'gravacao';
      case 'ProcessandoIA':
        return 'processando';
      case 'EmRevisao':
        return 'revisao';
      case 'Finalizado':
      case 'Cancelado':
        return 'encerrado';
      default:
        return 'consentimento';
    }
  });

  readonly etapaConcluida = computed(() => {
    const etapa = this.etapa();
    return {
      consentimento: etapa !== 'consentimento',
      gravacao: etapa === 'processando' || etapa === 'revisao' || etapa === 'encerrado',
      revisao: etapa === 'encerrado',
    };
  });

  // O alias do @if nao alcanca o interior dos blocos @switch do template.
  readonly consentimentoEm = computed(() => this.atendimento()?.consentimentoEm ?? null);
  readonly finalizado = computed(() => this.atendimento()?.status === 'Finalizado');

  /** Formato que a clinica de fato exporta, usado apenas para orientar o medico. */
  readonly modoOperacao = computed(() => this.auth.usuario()?.modoOperacao ?? null);
  readonly visaoOficial = computed<Visao>(() =>
    this.modoOperacao() === 'Conector' ? 'nota' : 'estruturado',
  );

  readonly tempoGravado = computed(() => {
    const total = this.segundosGravados();
    const minutos = Math.floor(total / 60).toString().padStart(2, '0');
    const segundos = (total % 60).toString().padStart(2, '0');
    return `${minutos}:${segundos}`;
  });

  /** Campos do formulario de atendimento, pre-preenchidos pela IA e editaveis pelo medico (RF09/RF10). */
  readonly form = inject(FormBuilder).nonNullable.group({
    queixaPrincipal: [''],
    hda: [''],
    antecedentes: [''],
    exameFisico: [''],
    hipoteseDiagnostica: [''],
    cid10Sugerido: [''],
    conduta: [''],
  });

  constructor() {
    // O id so existe apos o binding do parametro de rota, entao a carga inicial
    // acontece no effect; allowSignalWrites porque ela alimenta os signals da tela.
    effect(() => this.carregar(this.id()), { allowSignalWrites: true });

    inject(DestroyRef).onDestroy(() => {
      this.pararCronometro();
      this.pararConsulta();
    });
  }

  private carregar(id: string, silencioso = false): void {
    if (!silencioso) {
      this.carregando.set(true);
      this.erro.set(null);
    }

    this.atendimentos.obter(id).subscribe({
      next: (detalhe) => {
        this.atendimento.set(detalhe);
        this.transcricao.set(detalhe.transcricao);
        this.erroIA.set(detalhe.erroProcessamentoIA);
        this.form.patchValue({
          queixaPrincipal: detalhe.rascunho.queixaPrincipal ?? '',
          hda: detalhe.rascunho.hda ?? '',
          antecedentes: detalhe.rascunho.antecedentes ?? '',
          exameFisico: detalhe.rascunho.exameFisico ?? '',
          hipoteseDiagnostica: detalhe.rascunho.hipoteseDiagnostica ?? '',
          cid10Sugerido: detalhe.rascunho.cid10Sugerido ?? '',
          conduta: detalhe.rascunho.conduta ?? '',
        });

        if (detalhe.status === 'Finalizado' || detalhe.status === 'Cancelado') {
          this.form.disable();
        }

        // Um atendimento retomado pode estar em processamento desde outra sessao.
        if (detalhe.status === 'ProcessandoIA') {
          this.acompanharProcessamento();
        } else {
          this.pararConsulta();
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

  aceitarConsentimento(): void {
    this.erro.set(null);

    this.atendimentos.registrarConsentimento(this.id()).subscribe({
      next: () => {
        // O backend carimba o horario do aceite; refleti-lo aqui evita ter de
        // recarregar a tela para o medico ver o registro que acabou de fazer.
        const agora = new Date().toISOString();
        this.atendimento.update((atual) =>
          atual
            ? { ...atual, status: 'EmGravacao', consentimentoGravacao: true, consentimentoEm: agora }
            : atual,
        );
      },
      error: () => this.erro.set('Não foi possível registrar o consentimento.'),
    });
  }

  async iniciarGravacao(): Promise<void> {
    this.erro.set(null);

    try {
      await this.gravacaoAudio.iniciar();
      this.segundosGravados.set(0);
      this.cronometro = setInterval(() => this.segundosGravados.update((s) => s + 1), 1000);
    } catch {
      this.erro.set('Não foi possível acessar o microfone. Verifique a permissão do navegador.');
    }
  }

  async pararGravacao(): Promise<void> {
    const audio = await this.gravacaoAudio.parar();
    const duracao = this.segundosGravados();
    this.pararCronometro();

    this.enviandoAudio.set(true);
    this.erro.set(null);

    this.atendimentos.enviarAudio(this.id(), audio, duracao).subscribe({
      next: () => {
        this.enviandoAudio.set(false);
        this.atualizarStatus('ProcessandoIA');
        this.acompanharProcessamento();
      },
      error: () => {
        this.enviandoAudio.set(false);
        this.erro.set(
          'Não foi possível enviar o áudio. Você pode preencher o prontuário manualmente.',
        );
      },
    });
  }

  /**
   * Enquanto o worker transcreve e extrai os campos, a tela consulta o
   * atendimento periodicamente. Substituivel por SignalR (RF12) quando a
   * notificacao precisar chegar tambem fora desta tela.
   */
  private acompanharProcessamento(): void {
    if (this.consulta !== null) {
      return;
    }

    this.consulta = setInterval(() => {
      if (this.atendimento()?.status !== 'ProcessandoIA') {
        this.pararConsulta();
        return;
      }

      this.carregar(this.id(), true);
    }, 3000);
  }

  private pararConsulta(): void {
    if (this.consulta !== null) {
      clearInterval(this.consulta);
      this.consulta = null;
    }
  }

  alternarTranscricao(): void {
    this.mostrarTranscricao.update((valor) => !valor);
  }

  /** Permite revisar sem gravar - teleconsulta por escrito, retomada de atendimento, paciente que recusa a gravação. */
  pularGravacao(): void {
    this.atendimentos.iniciarRevisao(this.id()).subscribe({
      next: () => this.atualizarStatus('EmRevisao'),
      error: () => this.erro.set('Não foi possível avançar para a revisão.'),
    });
  }

  /** Tenta de novo a transcrição que falhou, sem abrir outro atendimento (RF13). */
  reprocessar(): void {
    if (this.reprocessando()) {
      return;
    }

    this.reprocessando.set(true);
    this.erro.set(null);

    this.atendimentos.reprocessar(this.id()).subscribe({
      next: () => {
        this.reprocessando.set(false);
        this.erroIA.set(null);
        this.atualizarStatus('ProcessandoIA');
        this.acompanharProcessamento();
      },
      error: (erro: { error?: { erro?: string } }) => {
        this.reprocessando.set(false);
        this.erro.set(erro.error?.erro ?? 'Não foi possível reprocessar o áudio.');
      },
    });
  }

  confirmar(): void {
    if (this.salvando()) {
      return;
    }

    this.salvando.set(true);
    this.erro.set(null);

    this.atendimentos
      .confirmar(this.id(), this.conteudoRevisado())
      .subscribe({
        next: () => {
          this.salvando.set(false);
          this.atualizarStatus('Finalizado');
          this.form.disable();
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

  private atualizarStatus(status: AtendimentoDetalhe['status']): void {
    this.atendimento.update((atual) => (atual ? { ...atual, status } : atual));
  }

  private pararCronometro(): void {
    if (this.cronometro !== null) {
      clearInterval(this.cronometro);
      this.cronometro = null;
    }
  }
}
