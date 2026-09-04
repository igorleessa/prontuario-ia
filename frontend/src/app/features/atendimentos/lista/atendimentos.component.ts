import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  AtendimentoResumo,
  CLASSE_STATUS,
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
