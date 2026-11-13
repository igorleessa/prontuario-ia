import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  EstadoOcupacional,
  FalaEnviada,
  FalasProcessadas,
  ModoOcupacional,
  SexoPaciente,
} from '../models/ocupacional.model';

/**
 * Rotas do exame ocupacional. A conversa não é guardada no servidor: as falas
 * vão em lotes pequenos, viram respostas do prontuário e são esquecidas.
 */
@Injectable({ providedIn: 'root' })
export class OcupacionalService {
  private readonly http = inject(HttpClient);

  private url(atendimentoId: string): string {
    return `${environment.apiUrl}/atendimentos/${atendimentoId}/ocupacional`;
  }

  iniciar(atendimentoId: string, modo: ModoOcupacional, sexo: SexoPaciente): Observable<EstadoOcupacional> {
    return this.http.post<EstadoOcupacional>(this.url(atendimentoId), { modo, sexo });
  }

  obter(atendimentoId: string): Observable<EstadoOcupacional> {
    return this.http.get<EstadoOcupacional>(this.url(atendimentoId));
  }

  /** Resposta dada pelo médico na tela. Valor nulo apaga a resposta. */
  responder(atendimentoId: string, campo: string, valor: string | null): Observable<EstadoOcupacional> {
    return this.http.put<EstadoOcupacional>(`${this.url(atendimentoId)}/respostas/${campo}`, { valor });
  }

  /** Falas novas, com as últimas já consideradas como contexto. */
  processarFalas(
    atendimentoId: string,
    novas: FalaEnviada[],
    contexto: FalaEnviada[],
    secaoEmFoco: string | null,
  ): Observable<FalasProcessadas> {
    return this.http.post<FalasProcessadas>(`${this.url(atendimentoId)}/falas`, { novas, contexto, secaoEmFoco });
  }

  /** Modo alternativo ao tempo real: o servidor transcreve o trecho em memória e descarta. */
  transcrever(atendimentoId: string, audio: Blob, extensao: string): Observable<{ texto: string }> {
    const corpo = new FormData();
    corpo.append('audio', audio, `trecho.${extensao}`);
    return this.http.post<{ texto: string }>(`${this.url(atendimentoId)}/transcrever`, corpo);
  }

  justificar(atendimentoId: string, chave: string, texto: string | null): Observable<EstadoOcupacional> {
    return this.http.put<EstadoOcupacional>(`${this.url(atendimentoId)}/justificativas/${chave}`, { texto });
  }

  concluir(atendimentoId: string): Observable<EstadoOcupacional> {
    return this.http.post<EstadoOcupacional>(`${this.url(atendimentoId)}/concluir`, {});
  }

  /** Falas da conversa fictícia do modo demonstração. */
  conversaDemonstracao(): Observable<{ falas: string[] }> {
    return this.http.get<{ falas: string[] }>(`${environment.apiUrl}/demonstracao/exame-ocupacional`);
  }
}
