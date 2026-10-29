import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';

interface ConfiguracaoApp {
  demonstracao: boolean;
  reconhecimentoNomes: boolean;
}

/**
 * Capacidades do servidor que a interface precisa conhecer — hoje, apenas se o
 * modo demonstração está ligado. Consultado uma vez e guardado em signal.
 */
@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private readonly http = inject(HttpClient);

  readonly demonstracao = signal(false);

  /** Se a camada de reconhecimento de nomes (modelo local) está em uso neste ambiente. */
  readonly reconhecimentoNomes = signal(false);

  constructor() {
    this.http.get<ConfiguracaoApp>(`${environment.apiUrl}/configuracao-app`).subscribe({
      next: ({ demonstracao, reconhecimentoNomes }) => {
        this.demonstracao.set(demonstracao);
        this.reconhecimentoNomes.set(reconhecimentoNomes ?? false);
      },
      // Sem resposta, a interface assume o modo normal: melhor esconder o botão
      // de demonstração do que oferecê-lo e ele falhar na frente do cliente.
      error: () => this.demonstracao.set(false),
    });
  }
}
