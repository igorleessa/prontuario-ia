import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Paciente } from '../../../core/models/paciente.model';
import { PacienteService } from '../../../core/services/paciente.service';
import { AvatarComponent } from '../../../shared/avatar.component';
import { IconeComponent } from '../../../shared/icone.component';
import { cpfMascarado, idade } from '../../../shared/pessoa';

/** Pacientes da clinica: atalho para o historico e para abrir uma consulta. */
@Component({
  selector: 'app-pacientes',
  standalone: true,
  imports: [RouterLink, FormsModule, IconeComponent, AvatarComponent],
  templateUrl: './pacientes.component.html',
  styleUrl: './pacientes.component.scss',
})
export class PacientesComponent {
  private readonly pacientes = inject(PacienteService);

  readonly carregando = signal(true);
  readonly erro = signal<string | null>(null);
  readonly lista = signal<Paciente[]>([]);
  readonly busca = signal('');

  readonly cpfMascarado = cpfMascarado;

  readonly filtrados = computed(() => {
    const termo = this.busca().trim().toLowerCase();
    const digitos = termo.replace(/\D/g, '');
    if (termo === '') {
      return this.lista();
    }

    return this.lista().filter(
      (p) =>
        p.nome.toLowerCase().includes(termo) ||
        (digitos.length > 0 && (p.cpf ?? '').replace(/\D/g, '').includes(digitos)),
    );
  });

  constructor() {
    this.pacientes.listar().subscribe({
      next: (lista) => {
        this.lista.set(lista);
        this.carregando.set(false);
      },
      error: () => {
        this.erro.set('Não foi possível carregar os pacientes.');
        this.carregando.set(false);
      },
    });
  }

  idade(dataNascimento: string | null): string | null {
    const anos = idade(dataNascimento);
    return anos === null ? null : `${anos} anos`;
  }
}
