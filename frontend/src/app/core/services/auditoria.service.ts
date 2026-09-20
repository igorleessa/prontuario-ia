import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LogAuditoria } from '../models/auditoria.model';

@Injectable({ providedIn: 'root' })
export class AuditoriaService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/auditoria`;

  listar(acao?: string | null, limite = 200): Observable<LogAuditoria[]> {
    let params = new HttpParams().set('limite', limite);
    if (acao) {
      params = params.set('acao', acao);
    }

    return this.http.get<LogAuditoria[]>(this.baseUrl, { params });
  }
}
