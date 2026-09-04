import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { NovoPaciente, Paciente } from '../models/paciente.model';

@Injectable({ providedIn: 'root' })
export class PacienteService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/pacientes`;

  listar(busca?: string): Observable<Paciente[]> {
    const params = busca ? new HttpParams().set('busca', busca) : undefined;
    return this.http.get<Paciente[]>(this.baseUrl, { params });
  }

  criar(novo: NovoPaciente): Observable<Paciente> {
    return this.http.post<Paciente>(this.baseUrl, novo);
  }
}
