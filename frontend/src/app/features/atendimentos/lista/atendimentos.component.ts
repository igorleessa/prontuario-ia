import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  AtendimentoResumo,
  CLASSE_STATUS,
  MetricasClinica,
  ROTULO_STATUS,
  STATUS_EM_ANDAMENTO,
} from '../../../core/models/atendimento.model';
import { AtendimentoService } from '../../../core/services/atendimento.service';
import { AuthService } from '../../../core/services/auth.service';
import { AvatarComponent } from '../../../shared/avatar.component';
import { IconeComponent } from '../../../shared/icone.component';

type Aba = 'emAndamento' | 'finalizados' | 'todos';

@Component({
  selector: 'app-atendimentos',
  standalone: true,
  imports: [RouterLink, FormsModule, DatePipe, IconeComponent, AvatarComponent],
  templateUrl: './atendimentos.component.html',
  styleUrl: './atendimentos.component.scss',
})
export class AtendimentosComponent {
  private readonly atendimentos = inject(AtendimentoService);
  private readonly auth = inject(AuthService);

  readonly carregando = signal(true);
  readonly erro = signal<string | null>(null);
  readonly lista = signal<AtendimentoResumo[]>([]);
  readonly aba = signal<Aba>('emAndamento');
  readonly busca = signal('');
  readonly metricas = signal<MetricasClinica | null>(null);

  readonly hoje = new Date();
  readonly rotuloStatus = ROTULO_STATUS;
  readonly classeStatus = CLASSE_STATUS;

  /** "Bom dia, Dra. Ana": o painel é a primeira tela do dia do médico. */
  readonly saudacao = computed(() => {
    const hora = this.hoje.getHours();
    const periodo = hora < 12 ? 'Bom dia' : hora < 18 ? 'Boa tarde' : 'Boa noite';
    const palavras = (this.auth.usuario()?.nome ?? '').trim().split(/\s+/).filter(Boolean);
    if (palavras.length === 0) {
      return periodo;
    }

    // "Dra. Ana Ribeiro" vira "Dra. Ana"; "Ana Ribeiro" vira "Ana".
    const titulo = /^(dra?|dr\.?|dra\.)$/i.test(palavras[0]!.replace('.', '')) ? 2 : 1;
    return `${periodo}, ${palavras.slice(0, titulo).join(' ')}`;
  });

  /** "2m14s" - formato curto, que cabe no cartão sem quebrar. */
  readonly tempoMedioFormatado = computed(() => {
    const segundos = this.metricas()?.tempoMedioRevisaoSegundos;
    if (segundos == null) {
      return null;
    }

    const minutos = Math.floor(segundos / 60);
    const resto = segundos % 60;
    return minutos > 0 ? `${minutos}m${resto.toString().padStart(2, '0')}s` : `${resto}s`;
  });

  readonly emAndamento = computed(() => this.lista().filter((a) => STATUS_EM_ANDAMENTO.includes(a.status)));
  readonly emConsultaAgora = computed(() => this.lista().filter((a) => a.status === 'EmConsulta').length);
  readonly aguardandoRevisao = computed(() => this.lista().filter((a) => a.status === 'EmRevisao').length);
  readonly encerrados = computed(() => this.lista().length - this.emAndamento().length);

  readonly filtrados = computed(() => {
    const termo = this.busca().trim().toLowerCase();
    const aba = this.aba();

    return this.lista().filter((atendimento) => {
      const andamento = STATUS_EM_ANDAMENTO.includes(atendimento.status);
      const naAba = aba === 'todos' || (aba === 'emAndamento' ? andamento : !andamento);

      const combina =
        termo === '' ||
        atendimento.pacienteNome.toLowerCase().includes(termo) ||
        atendimento.medicoNome.toLowerCase().includes(termo);

      return naAba && combina;
    });
  });

  constructor() {
    this.carregar();

    // Metricas sao informativas: se falharem, a lista de atendimentos continua.
    this.atendimentos.metricas().subscribe({
      next: (metricas) => this.metricas.set(metricas),
    });
  }

  carregar(): void {
    this.carregando.set(true);
    this.erro.set(null);

    this.atendimentos.listar().subscribe({
      next: (lista) => {
        this.lista.set(lista);
        this.carregando.set(false);
      },
      error: () => {
        this.erro.set('Não foi possível carregar os atendimentos.');
        this.carregando.set(false);
      },
    });
  }

  cancelar(atendimento: AtendimentoResumo, evento: Event): void {
    // A linha inteira e um link para o atendimento; cancelar nao deve navegar.
    evento.stopPropagation();
    evento.preventDefault();

    if (!confirm(`Cancelar o atendimento de ${atendimento.pacienteNome}?`)) {
      return;
    }

    this.atendimentos.cancelar(atendimento.id).subscribe({
      next: () => this.carregar(),
      error: () => this.erro.set('Não foi possível cancelar o atendimento.'),
    });
  }

  cancelavel(atendimento: AtendimentoResumo): boolean {
    return atendimento.status !== 'Finalizado' && atendimento.status !== 'Cancelado';
  }

  acao(atendimento: AtendimentoResumo): string {
    switch (atendimento.status) {
      case 'AguardandoConsentimento':
        return 'Iniciar';
      case 'EmConsulta':
        return 'Retomar';
      case 'EmRevisao':
        return 'Revisar';
      default:
        return 'Ver registro';
    }
  }
}
