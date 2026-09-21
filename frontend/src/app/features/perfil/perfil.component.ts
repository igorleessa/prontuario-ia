import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { PerfilMedico } from '../../core/models/template.model';
import { PerfilService } from '../../core/services/perfil.service';

/**
 * Preferencias de redacao do proprio medico. E o que aproxima a nota do jeito
 * como ele ja escreve sem treinar modelo nenhum: o texto entra como instrucao
 * adicional no prompt, sempre abaixo das regras de nao inventar informacao.
 */
@Component({
  selector: 'app-perfil',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './perfil.component.html',
  styleUrl: './perfil.component.scss',
})
export class PerfilComponent {
  private readonly perfis = inject(PerfilService);

  readonly carregando = signal(true);
  readonly salvando = signal(false);
  readonly erro = signal<string | null>(null);
  readonly sucesso = signal<string | null>(null);
  readonly perfil = signal<PerfilMedico | null>(null);

  readonly exemplos = [
    'HDA em parágrafo único, sem tópicos.',
    'Sempre registrar as negativas relevantes citadas na consulta.',
    'Conduta em lista numerada, uma orientação por linha.',
    'Usar terminologia técnica, sem linguagem coloquial.',
  ];

  readonly form = inject(FormBuilder).nonNullable.group({
    instrucoesEstilo: [''],
  });

  constructor() {
    this.carregar();
  }

  aplicarExemplo(exemplo: string): void {
    const atual = this.form.getRawValue().instrucoesEstilo.trim();
    this.form.patchValue({
      instrucoesEstilo: atual ? `${atual}\n${exemplo}` : exemplo,
    });
  }

  salvar(): void {
    if (this.salvando()) {
      return;
    }

    this.salvando.set(true);
    this.erro.set(null);
    this.sucesso.set(null);

    this.perfis.salvarEstilo(this.form.getRawValue().instrucoesEstilo.trim() || null).subscribe({
      next: (perfil) => {
        this.perfil.set(perfil);
        this.salvando.set(false);
        this.sucesso.set('Preferências salvas. Valem para as próximas consultas.');
      },
      error: (erro: { error?: { erro?: string } }) => {
        this.salvando.set(false);
        this.erro.set(erro.error?.erro ?? 'Não foi possível salvar as preferências.');
      },
    });
  }

  private carregar(): void {
    this.perfis.obter().subscribe({
      next: (perfil) => {
        this.perfil.set(perfil);
        this.form.patchValue({ instrucoesEstilo: perfil.instrucoesEstilo ?? '' });
        this.carregando.set(false);
      },
      error: () => {
        this.erro.set('Não foi possível carregar o perfil.');
        this.carregando.set(false);
      },
    });
  }
}
