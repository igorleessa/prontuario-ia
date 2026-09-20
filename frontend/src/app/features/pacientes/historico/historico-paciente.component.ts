import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  CLASSE_STATUS,
  HistoricoPaciente,
  ROTULO_STATUS,
} from '../../../core/models/atendimento.model';
import { PacienteService } from '../../../core/services/paciente.service';

/**
 * Linha do tempo de atendimentos do paciente (RF16). Na Modalidade B o registro
 * definitivo vive no EMR do cliente: aqui aparece o que passou por este sistema.
 */
@Component({
  selector: 'app-historico-paciente',
  standalone: true,
  imports: [RouterLink, DatePipe],
  templateUrl: './historico-paciente.component.html',
  styleUrl: './historico-paciente.component.scss',
})
export class HistoricoPacienteComponent {
  private readonly pacientes = inject(PacienteService);

  readonly id = input.required<string>();

  readonly carregando = signal(true);
  readonly erro = signal<string | null>(null);
  readonly historico = signal<HistoricoPaciente | null>(null);

  readonly rotuloStatus = ROTULO_STATUS;
  readonly classeStatus = CLASSE_STATUS;

  readonly idade = computed(() => {
    const nascimento = this.historico()?.dataNascimento;
    if (!nascimento) {
      return null;
    }

    const data = new Date(nascimento);
    const hoje = new Date();
    let anos = hoje.getFullYear() - data.getFullYear();

    const aniversarioAindaNaoOcorreu =
      hoje.getMonth() < data.getMonth() ||
      (hoje.getMonth() === data.getMonth() && hoje.getDate() < data.getDate());

    return `${aniversarioAindaNaoOcorreu ? anos - 1 : anos} anos`;
  });

  constructor() {
    effect(() => this.carregar(this.id()), { allowSignalWrites: true });
  }

  private carregar(id: string): void {
    this.carregando.set(true);
    this.erro.set(null);

    this.pacientes.obterHistorico(id).subscribe({
      next: (historico) => {
        this.historico.set(historico);
        this.carregando.set(false);
      },
      error: () => {
        this.erro.set('Não foi possível carregar o histórico do paciente.');
        this.carregando.set(false);
      },
    });
  }
}
