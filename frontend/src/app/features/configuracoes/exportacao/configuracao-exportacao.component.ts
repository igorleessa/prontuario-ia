import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import {
  ChaveIntegracaoGerada,
  ConfiguracaoExportacao,
  ResultadoExportacao,
} from '../../../core/models/exportacao.model';
import { AuthService } from '../../../core/services/auth.service';
import { ConfiguracaoExportacaoService } from '../../../core/services/configuracao-exportacao.service';

/**
 * Saida da Modalidade B: para onde a nota revisada e enviada e com que chave o
 * EMR do cliente abre atendimentos. E a tela que o time tecnico do cliente usa
 * na implantacao, entao ela precisa provar a integracao ali mesmo - dai o botao
 * de envio de teste.
 */
@Component({
  selector: 'app-configuracao-exportacao',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './configuracao-exportacao.component.html',
  styleUrl: './configuracao-exportacao.component.scss',
})
export class ConfiguracaoExportacaoComponent {
  private readonly configuracoes = inject(ConfiguracaoExportacaoService);
  private readonly auth = inject(AuthService);

  readonly carregando = signal(true);
  readonly salvando = signal(false);
  readonly testando = signal(false);
  readonly erro = signal<string | null>(null);
  readonly sucesso = signal<string | null>(null);
  readonly configuracao = signal<ConfiguracaoExportacao | null>(null);
  readonly resultadoTeste = signal<ResultadoExportacao | null>(null);
  readonly chaveGerada = signal<ChaveIntegracaoGerada | null>(null);
  readonly chaveCopiada = signal(false);

  /** No modo Integrado a exportacao e opcional; a tela avisa em vez de esconder. */
  readonly modoConector = computed(() => this.auth.usuario()?.modoOperacao === 'Conector');

  readonly form = inject(FormBuilder).nonNullable.group({
    webhookUrl: [''],
    webhookSecret: [''],
  });

  constructor() {
    this.carregar();
  }

  private carregar(): void {
    this.carregando.set(true);

    this.configuracoes.obter().subscribe({
      next: (configuracao) => {
        this.configuracao.set(configuracao);
        this.form.patchValue({ webhookUrl: configuracao.webhookUrl ?? '' });
        this.carregando.set(false);
      },
      error: () => {
        this.erro.set('Não foi possível carregar a configuração de exportação.');
        this.carregando.set(false);
      },
    });
  }

  salvar(): void {
    if (this.salvando()) {
      return;
    }

    const valores = this.form.getRawValue();
    this.salvando.set(true);
    this.limparAvisos();

    this.configuracoes
      .salvar({
        webhookUrl: valores.webhookUrl.trim() || null,
        // Vazio significa "manter o segredo atual", o que permite trocar so a URL.
        webhookSecret: valores.webhookSecret.trim() || null,
      })
      .subscribe({
        next: (configuracao) => {
          this.configuracao.set(configuracao);
          this.form.patchValue({ webhookSecret: '' });
          this.salvando.set(false);
          this.sucesso.set('Destino de exportação salvo.');
        },
        error: (erro: { error?: { erro?: string } }) => {
          this.salvando.set(false);
          this.erro.set(erro.error?.erro ?? 'Não foi possível salvar o destino de exportação.');
        },
      });
  }

  testar(): void {
    this.testando.set(true);
    this.limparAvisos();

    this.configuracoes.testar().subscribe({
      next: (resultado) => {
        this.testando.set(false);
        this.resultadoTeste.set(resultado);
      },
      error: () => {
        this.testando.set(false);
        this.erro.set('Não foi possível executar o teste.');
      },
    });
  }

  gerarChave(): void {
    if (
      this.configuracao()?.chaveIntegracaoPrefixo &&
      !confirm('Gerar uma nova chave invalida a atual. O EMR do cliente precisará ser atualizado. Continuar?')
    ) {
      return;
    }

    this.limparAvisos();

    this.configuracoes.gerarChaveIntegracao().subscribe({
      next: (chave) => {
        this.chaveGerada.set(chave);
        this.chaveCopiada.set(false);
        this.configuracao.update((atual) =>
          atual
            ? { ...atual, chaveIntegracaoPrefixo: chave.prefixo, chaveIntegracaoCriadaEm: chave.criadaEm }
            : atual,
        );
      },
      error: () => this.erro.set('Não foi possível gerar a chave de integração.'),
    });
  }

  revogarChave(): void {
    if (!confirm('Revogar a chave? O EMR do cliente deixa de conseguir abrir atendimentos.')) {
      return;
    }

    this.limparAvisos();

    this.configuracoes.revogarChaveIntegracao().subscribe({
      next: () => {
        this.chaveGerada.set(null);
        this.configuracao.update((atual) =>
          atual ? { ...atual, chaveIntegracaoPrefixo: null, chaveIntegracaoCriadaEm: null } : atual,
        );
        this.sucesso.set('Chave revogada.');
      },
      error: () => this.erro.set('Não foi possível revogar a chave.'),
    });
  }

  async copiarChave(): Promise<void> {
    const chave = this.chaveGerada()?.chave;
    if (!chave) {
      return;
    }

    try {
      await navigator.clipboard.writeText(chave);
      this.chaveCopiada.set(true);
    } catch {
      this.erro.set('Não foi possível copiar. Selecione o texto e copie manualmente.');
    }
  }

  private limparAvisos(): void {
    this.erro.set(null);
    this.sucesso.set(null);
    this.resultadoTeste.set(null);
  }
}
