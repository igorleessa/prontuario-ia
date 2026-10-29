import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CLASSE_STATUS, HistoricoPaciente, ROTULO_STATUS } from '../../../core/models/atendimento.model';
import { PacienteService } from '../../../core/services/paciente.service';
import { AvatarComponent } from '../../../shared/avatar.component';
import { IconeComponent } from '../../../shared/icone.component';
import { cpfMascarado, idade } from '../../../shared/pessoa';

/**
 * Linha do tempo de atendimentos do paciente (RF16). Na Modalidade B o registro
 * definitivo vive no EMR do cliente: aqui aparece o que passou por este sistema.
 */
@Component({
  selector: 'app-historico-paciente',
  standalone: true,
  imports: [RouterLink, DatePipe, IconeComponent, AvatarComponent],
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

  readonly idade = computed(() => idade(this.historico()?.dataNascimento));
  readonly cpf = computed(() => cpfMascarado(this.historico()?.cpf));
  readonly finalizados = computed(() => this.historico()?.atendimentos.filter((a) => a.finalizado).length ?? 0);

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
