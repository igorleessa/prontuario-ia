import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { RascunhoClinico } from '../models/rascunho-clinico.model';

@Injectable({ providedIn: 'root' })
export class AtendimentoService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/atendimentos`;

  abrir(pacienteRefId: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, { pacienteRefId });
  }

  registrarConsentimento(atendimentoId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${atendimentoId}/consentimento`, {});
  }

  /** Confirma a revisao do medico (RF10). O backend decide entre gravar no prontuario nativo ou exportar para o EMR. */
  confirmar(atendimentoId: string, revisado: RascunhoClinico): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${atendimentoId}/confirmar`, revisado);
  }
}
