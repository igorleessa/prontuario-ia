import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  AtendimentoResumo,
  CLASSE_STATUS,
  MetricasClinica,
  ROTULO_STATUS,
  StatusAtendimento,
} from '../../../core/models/atendimento.model';
import { AtendimentoService } from '../../../core/services/atendimento.service';

type Aba = 'emAndamento' | 'finalizados' | 'todos';

const STATUS_EM_ANDAMENTO: StatusAtendimento[] = [
  'AguardandoConsentimento',
  'EmGravacao',
  'ProcessandoIA',
  'EmRevisao',
];

@Component({
  selector: 'app-atendimentos',
  standalone: true,
  imports: [RouterLink, FormsModule, DatePipe],
  templateUrl: './atendimentos.component.html',
  styleUrl: './atendimentos.component.scss',
})
export class AtendimentosComponent {
  private readonly atendimentos = inject(AtendimentoService);

  readonly carregando = signal(true);
  readonly erro = signal<string | null>(null);
  readonly lista = signal<AtendimentoResumo[]>([]);
  readonly aba = signal<Aba>('emAndamento');
  readonly busca = signal('');
  readonly metricas = signal<MetricasClinica | null>(null);

  /** "2m14s" - formato curto, que cabe no cartao sem quebrar. */
  readonly tempoMedioFormatado = computed(() => {
    const segundos = this.metricas()?.tempoMedioRevisaoSegundos;
    if (segundos == null) {
      return null;
    }

    const minutos = Math.floor(segundos / 60);
    const resto = segundos % 60;
    return minutos > 0 ? `${minutos}m${resto.toString().padStart(2, '0')}s` : `${resto}s`;
  });

  readonly rotuloStatus = ROTULO_STATUS;
  readonly classeStatus = CLASSE_STATUS;

  readonly emAndamento = computed(
    () => this.lista().filter((a) => STATUS_EM_ANDAMENTO.includes(a.status)).length,
  );

  readonly filtrados = computed(() => {
    const termo = this.busca().trim().toLowerCase();
    const aba = this.aba();

    return this.lista().filter((atendimento) => {
      const naAba =
        aba === 'todos' ||
        (aba === 'emAndamento' && STATUS_EM_ANDAMENTO.includes(atendimento.status)) ||
        (aba === 'finalizados' && !STATUS_EM_ANDAMENTO.includes(atendimento.status));

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
    // O item inteiro e um link para o atendimento; cancelar nao deve navegar.
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
}
