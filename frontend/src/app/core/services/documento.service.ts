import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DocumentoClinico } from '../models/template.model';

@Injectable({ providedIn: 'root' })
export class DocumentoService {
  private readonly http = inject(HttpClient);

  private url(atendimentoId: string): string {
    return `${environment.apiUrl}/atendimentos/${atendimentoId}/documentos`;
  }

  listar(atendimentoId: string): Observable<DocumentoClinico[]> {
    return this.http.get<DocumentoClinico[]>(this.url(atendimentoId));
  }

  /** Redige o documento a partir da consulta. Regerar substitui a versão anterior do mesmo tipo. */
  gerar(atendimentoId: string, tipo: string): Observable<DocumentoClinico> {
    return this.http.post<DocumentoClinico>(this.url(atendimentoId), { tipo });
  }

  /** Grava a versão revisada pelo médico, que passa a ser o conteúdo oficial. */
  salvar(atendimentoId: string, documentoId: string, conteudo: string): Observable<DocumentoClinico> {
    return this.http.put<DocumentoClinico>(`${this.url(atendimentoId)}/${documentoId}`, { conteudo });
  }

  baixarPdf(atendimentoId: string, documentoId: string): Observable<Blob> {
    return this.http.get(`${this.url(atendimentoId)}/${documentoId}/pdf`, { responseType: 'blob' });
  }
}
