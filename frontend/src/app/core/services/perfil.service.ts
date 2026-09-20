import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PerfilMedico } from '../models/template.model';

@Injectable({ providedIn: 'root' })
export class PerfilService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/perfil`;

  obter(): Observable<PerfilMedico> {
    return this.http.get<PerfilMedico>(this.baseUrl);
  }

  salvarEstilo(instrucoesEstilo: string | null): Observable<PerfilMedico> {
    return this.http.put<PerfilMedico>(`${this.baseUrl}/estilo`, { instrucoesEstilo });
  }
}
