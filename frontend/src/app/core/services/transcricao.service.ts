import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { RascunhoComOrigens } from '../models/rascunho-clinico.model';
import { Falante, SessaoTranscricao, TranscricaoAtualizada, TrechoRegistrado } from '../models/transcricao.model';

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

  /**
   * Refaz o rascunho do prontuário sobre o que já foi dito. A passada final
   * (encerramento, "refazer") usa o modelo completo; durante a consulta, o rápido.
   */
  gerarRascunho(atendimentoId: string, final = false): Observable<RascunhoComOrigens> {
    return this.http.post<RascunhoComOrigens>(
      `${this.baseUrl}/${atendimentoId}/transcricao/rascunho`,
      {},
      { params: { final } },
    );
  }

  /** O médico corrigiu quem disse o trecho. "Indefinido" devolve a decisão à IA. */
  definirFalante(atendimentoId: string, sequencia: number, falante: Falante): Observable<TrechoRegistrado> {
    return this.http.put<TrechoRegistrado>(
      `${this.baseUrl}/${atendimentoId}/transcricao/trechos/${sequencia}/falante`,
      { falante },
    );
  }

  /** O médico marcou um trecho da transcrição como dado pessoal que a sanitização deixou passar. */
  protegerTermo(atendimentoId: string, termo: string): Observable<TranscricaoAtualizada> {
    return this.http.post<TranscricaoAtualizada>(
      `${this.baseUrl}/${atendimentoId}/transcricao/termos-protegidos`,
      { termo },
    );
  }

  encerrar(atendimentoId: string, duracaoSegundos: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${atendimentoId}/transcricao/encerrar`, { duracaoSegundos });
  }

  /** Falas da consulta fictícia do modo demonstração. */
  consultaDemonstracao(): Observable<{ trechos: string[] }> {
    return this.http.get<{ trechos: string[] }>(`${environment.apiUrl}/demonstracao/consulta`);
  }
}
