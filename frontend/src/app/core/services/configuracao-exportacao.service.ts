import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ChaveIntegracaoGerada,
  ConfiguracaoExportacao,
  ResultadoExportacao,
  SalvarConfiguracaoExportacao,
} from '../models/exportacao.model';

@Injectable({ providedIn: 'root' })
export class ConfiguracaoExportacaoService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/configuracao-exportacao`;

  obter(): Observable<ConfiguracaoExportacao> {
    return this.http.get<ConfiguracaoExportacao>(this.baseUrl);
  }

  salvar(dados: SalvarConfiguracaoExportacao): Observable<ConfiguracaoExportacao> {
    return this.http.put<ConfiguracaoExportacao>(this.baseUrl, dados);
  }

  /** Dispara um evento fictício contra o webhook — nenhum dado real de paciente trafega. */
  testar(): Observable<ResultadoExportacao> {
    return this.http.post<ResultadoExportacao>(`${this.baseUrl}/testar`, {});
  }

  gerarChaveIntegracao(): Observable<ChaveIntegracaoGerada> {
    return this.http.post<ChaveIntegracaoGerada>(`${this.baseUrl}/chave-integracao`, {});
  }

  revogarChaveIntegracao(): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/chave-integracao`);
  }
}
