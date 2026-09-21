import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ConfiguracaoIA } from '../../core/models/configuracao-ia.model';
import { ConfiguracaoIAService } from '../../core/services/configuracao-ia.service';
import { ConfiguracaoExportacaoComponent } from './exportacao/configuracao-exportacao.component';

@Component({
  selector: 'app-configuracao-ia',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, DatePipe, ConfiguracaoExportacaoComponent],
  templateUrl: './configuracao-ia.component.html',
  styleUrl: './configuracao-ia.component.scss',
})
export class ConfiguracaoIAComponent {
  private readonly configuracoes = inject(ConfiguracaoIAService);

  readonly carregando = signal(true);
  readonly salvando = signal(false);
  readonly erro = signal<string | null>(null);
  readonly sucesso = signal<string | null>(null);
  readonly configuracao = signal<ConfiguracaoIA | null>(null);

  readonly form = inject(FormBuilder).nonNullable.group({
    chaveApi: [''],
    modeloTranscricao: ['whisper-1'],
    modeloTexto: ['gpt-4o'],
  });

  constructor() {
    this.carregar();
  }

  private carregar(): void {
    this.carregando.set(true);

    this.configuracoes.obter().subscribe({
      next: (configuracao) => {
        this.configuracao.set(configuracao);
        this.form.patchValue({
          modeloTranscricao: configuracao.modeloTranscricao,
          modeloTexto: configuracao.modeloTexto,
        });
        this.carregando.set(false);
      },
      error: () => {
        this.erro.set('Não foi possível carregar a configuração.');
        this.carregando.set(false);
      },
    });
  }

  salvar(): void {
    if (this.salvando()) {
      return;
    }

    const valores = this.form.getRawValue();
    if (!this.configuracao()?.chaveConfigurada && !valores.chaveApi.trim()) {
      this.erro.set('Informe a chave da API para ativar a transcrição.');
      return;
    }

    this.salvando.set(true);
    this.erro.set(null);
    this.sucesso.set(null);

    this.configuracoes
      .salvar({
        // Vazio significa "manter a chave atual", o que permite trocar so o modelo.
        chaveApi: valores.chaveApi.trim() || null,
        modeloTranscricao: valores.modeloTranscricao,
        modeloTexto: valores.modeloTexto,
      })
      .subscribe({
        next: (configuracao) => {
          this.configuracao.set(configuracao);
          this.form.patchValue({ chaveApi: '' });
          this.salvando.set(false);
          this.sucesso.set('Configuração salva. A transcrição automática está ativa.');
        },
        error: (erro: { error?: { erro?: string } }) => {
          this.salvando.set(false);
          this.erro.set(erro.error?.erro ?? 'Não foi possível salvar a configuração.');
        },
      });
  }

  remover(): void {
    if (!confirm('Remover a chave da API? A transcrição automática deixa de funcionar.')) {
      return;
    }

    this.erro.set(null);
    this.sucesso.set(null);

    this.configuracoes.remover().subscribe({
      next: () => {
        this.configuracao.set({
          chaveConfigurada: false,
          chaveApiSufixo: null,
          modeloTranscricao: 'whisper-1',
          modeloTexto: 'gpt-4o',
          atualizadoEm: null,
        });
        this.form.reset({ chaveApi: '', modeloTranscricao: 'whisper-1', modeloTexto: 'gpt-4o' });
        this.sucesso.set('Chave removida.');
      },
      error: () => this.erro.set('Não foi possível remover a chave.'),
    });
  }
}
