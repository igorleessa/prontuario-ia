import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ConfiguracaoIA, SalvarConfiguracaoIA } from '../models/configuracao-ia.model';

@Injectable({ providedIn: 'root' })
export class ConfiguracaoIAService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/configuracao-ia`;

  obter(): Observable<ConfiguracaoIA> {
    return this.http.get<ConfiguracaoIA>(this.baseUrl);
  }

  salvar(dados: SalvarConfiguracaoIA): Observable<ConfiguracaoIA> {
    return this.http.put<ConfiguracaoIA>(this.baseUrl, dados);
  }

  remover(): Observable<void> {
    return this.http.delete<void>(this.baseUrl);
  }
}
