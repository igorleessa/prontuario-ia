import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { RascunhoClinico } from '../models/rascunho-clinico.model';
import { SessaoTranscricao, TrechoRegistrado } from '../models/transcricao.model';

/**
 * Rotas da consulta transcrita ao vivo. Nenhuma recebe a gravação da consulta:
 * chega só texto (tempo real) ou trechos de poucos segundos que o servidor
 * transcreve em memória e descarta.
 */
@Injectable({ providedIn: 'root' })
export class TranscricaoService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/atendimentos`;

  iniciarSessao(atendimentoId: string): Observable<SessaoTranscricao> {
    return this.http.post<SessaoTranscricao>(`${this.baseUrl}/${atendimentoId}/transcricao/sessao`, {});
  }

  /** Trecho já transcrito pelo provedor em tempo real. */
  registrarTrecho(atendimentoId: string, sequencia: number, texto: string): Observable<TrechoRegistrado> {
    return this.http.post<TrechoRegistrado>(`${this.baseUrl}/${atendimentoId}/transcricao/trechos`, {
      sequencia,
      texto,
    });
  }

  /** Modo por trechos: o servidor transcreve e descarta. Null quando o trecho não tinha fala. */
  transcreverTrecho(
    atendimentoId: string,
    sequencia: number,
    audio: Blob,
    extensao: string,
  ): Observable<TrechoRegistrado | null> {
    const corpo = new FormData();
    corpo.append('audio', audio, `trecho.${extensao}`);
    corpo.append('sequencia', String(sequencia));

    return this.http.post<TrechoRegistrado | null>(
      `${this.baseUrl}/${atendimentoId}/transcricao/trechos-audio`,
      corpo,
    );
  }

  /** Refaz o rascunho do prontuário sobre o que já foi dito. */
  gerarRascunho(atendimentoId: string): Observable<RascunhoClinico> {
    return this.http.post<RascunhoClinico>(`${this.baseUrl}/${atendimentoId}/transcricao/rascunho`, {});
  }

  encerrar(atendimentoId: string, duracaoSegundos: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${atendimentoId}/transcricao/encerrar`, { duracaoSegundos });
  }

  /** Falas da consulta fictícia do modo demonstração. */
  consultaDemonstracao(): Observable<{ trechos: string[] }> {
    return this.http.get<{ trechos: string[] }>(`${environment.apiUrl}/demonstracao/consulta`);
  }
}
