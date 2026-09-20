import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AtendimentoDetalhe, AtendimentoResumo } from '../models/atendimento.model';
import { NotaExportavel, ResultadoExportacao } from '../models/exportacao.model';
import { RascunhoClinico } from '../models/rascunho-clinico.model';

@Injectable({ providedIn: 'root' })
export class AtendimentoService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/atendimentos`;

  listar(): Observable<AtendimentoResumo[]> {
    return this.http.get<AtendimentoResumo[]>(this.baseUrl);
  }

  obter(atendimentoId: string): Observable<AtendimentoDetalhe> {
    return this.http.get<AtendimentoDetalhe>(`${this.baseUrl}/${atendimentoId}`);
  }

  abrir(pacienteRefId: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, { pacienteRefId });
  }

  /** Envia o audio da consulta e dispara a transcricao e a extracao (RF06/RF07/RF08). */
  enviarAudio(atendimentoId: string, audio: Blob, duracaoSegundos: number): Observable<void> {
    const corpo = new FormData();
    corpo.append('audio', audio, 'consulta.webm');
    corpo.append('duracaoSegundos', String(duracaoSegundos));

    return this.http.post<void>(`${this.baseUrl}/${atendimentoId}/audio`, corpo);
  }

  registrarConsentimento(atendimentoId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${atendimentoId}/consentimento`, {});
  }

  /** Marca o fim da captura de audio e a entrada na revisao do medico (RF09). */
  iniciarRevisao(atendimentoId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${atendimentoId}/revisao`, {});
  }

  /** Confirma a revisao do medico (RF10). O backend decide entre gravar no prontuario nativo ou exportar para o EMR. */
  confirmar(atendimentoId: string, revisado: RascunhoClinico): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${atendimentoId}/confirmar`, revisado);
  }

  /**
   * Pre-visualiza a nota clinica de texto corrido (Modalidade B) sem persistir.
   * A formatacao vem do backend para ser identica a que seria exportada ao EMR.
   */
  preverNota(revisado: RascunhoClinico): Observable<{ conteudo: string }> {
    return this.http.post<{ conteudo: string }>(`${this.baseUrl}/nota-previa`, revisado);
  }

  /** Nota clínica gravada e estado da exportação (RF20) — o que de fato foi enviado ao EMR. */
  obterNota(atendimentoId: string): Observable<NotaExportavel> {
    return this.http.get<NotaExportavel>(`${this.baseUrl}/${atendimentoId}/nota`);
  }

  /** Reenvia a nota ao webhook da clínica quando o envio automático falhou (RF19). */
  exportar(atendimentoId: string): Observable<ResultadoExportacao> {
    return this.http.post<ResultadoExportacao>(`${this.baseUrl}/${atendimentoId}/exportar`, {});
  }

  /** PDF da nota clínica (RF18), para anexar ou imprimir no EMR do cliente. */
  baixarNotaPdf(atendimentoId: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${atendimentoId}/nota.pdf`, { responseType: 'blob' });
  }

  cancelar(atendimentoId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${atendimentoId}/cancelar`, {});
  }
}
