import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Paciente } from '../../../core/models/paciente.model';
import { TemplateNota } from '../../../core/models/template.model';
import { AtendimentoService } from '../../../core/services/atendimento.service';
import { PacienteService } from '../../../core/services/paciente.service';
import { TemplateService } from '../../../core/services/template.service';

@Component({
  selector: 'app-novo-atendimento',
  standalone: true,
  imports: [RouterLink, FormsModule, ReactiveFormsModule],
  templateUrl: './novo-atendimento.component.html',
  styleUrl: './novo-atendimento.component.scss',
})
export class NovoAtendimentoComponent {
  private readonly pacientes = inject(PacienteService);
  private readonly atendimentos = inject(AtendimentoService);
  private readonly templates = inject(TemplateService);
  private readonly router = inject(Router);

  readonly carregando = signal(true);
  readonly erro = signal<string | null>(null);
  readonly lista = signal<Paciente[]>([]);
  readonly busca = signal('');
  readonly selecionado = signal<Paciente | null>(null);
  readonly abrindo = signal(false);
  readonly cadastrando = signal(false);

  /** Modelo de nota da especialidade; vazio usa o modelo generico (SOAP). */
  readonly modelos = signal<TemplateNota[]>([]);
  readonly modeloEscolhido = signal<string>('');

  readonly filtrados = computed(() => {
    const termo = this.busca().trim().toLowerCase();
    if (termo === '') {
      return this.lista();
    }

    return this.lista().filter(
      (p) => p.nome.toLowerCase().includes(termo) || (p.cpf ?? '').includes(termo),
    );
  });

  readonly formPaciente = inject(FormBuilder).nonNullable.group({
    nome: ['', Validators.required],
    cpf: [''],
    dataNascimento: [''],
    contato: [''],
  });

  constructor() {
    this.carregarPacientes();

    // Falha no catalogo nao impede abrir atendimento: sem modelo, a extracao
    // usa o prompt generico.
    this.templates.listar().subscribe({
      next: (modelos) => this.modelos.set(modelos),
    });
  }

  carregarPacientes(): void {
    this.carregando.set(true);

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

  selecionar(paciente: Paciente): void {
    this.selecionado.set(this.selecionado()?.id === paciente.id ? null : paciente);
  }

  alternarCadastro(): void {
    this.cadastrando.update((valor) => !valor);
    this.selecionado.set(null);
    this.formPaciente.reset();
  }

  cadastrarPaciente(): void {
    if (this.formPaciente.invalid) {
      return;
    }

    const valores = this.formPaciente.getRawValue();
    this.erro.set(null);

    this.pacientes
      .criar({
        nome: valores.nome,
        cpf: valores.cpf || null,
        dataNascimento: valores.dataNascimento || null,
        contato: valores.contato || null,
        idExternoEmr: null,
      })
      .subscribe({
        next: (paciente) => {
          // Ja deixa o recem-cadastrado escolhido, que e sempre a intencao aqui.
          this.lista.update((atual) => [...atual, paciente].sort((a, b) => a.nome.localeCompare(b.nome)));
          this.selecionado.set(paciente);
          this.cadastrando.set(false);
          this.formPaciente.reset();
        },
        error: () => this.erro.set('Não foi possível cadastrar o paciente.'),
      });
  }

  abrirAtendimento(): void {
    const paciente = this.selecionado();
    if (!paciente || this.abrindo()) {
      return;
    }

    this.abrindo.set(true);
    this.erro.set(null);

    this.atendimentos.abrir(paciente.id, this.modeloEscolhido() || null).subscribe({
      next: ({ id }) => this.router.navigate(['/atendimentos', id]),
      error: () => {
        this.abrindo.set(false);
        this.erro.set('Não foi possível abrir o atendimento.');
      },
    });
  }

  idade(dataNascimento: string | null): string | null {
    if (!dataNascimento) {
      return null;
    }

    const nascimento = new Date(dataNascimento);
    const hoje = new Date();
    let anos = hoje.getFullYear() - nascimento.getFullYear();

    const aniversarioAindaNaoOcorreu =
      hoje.getMonth() < nascimento.getMonth() ||
      (hoje.getMonth() === nascimento.getMonth() && hoje.getDate() < nascimento.getDate());

    if (aniversarioAindaNaoOcorreu) {
      anos -= 1;
    }

    return `${anos} anos`;
  }
}
