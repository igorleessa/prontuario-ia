import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TemplateNota } from '../models/template.model';

@Injectable({ providedIn: 'root' })
export class TemplateService {
  private readonly http = inject(HttpClient);

  listar(): Observable<TemplateNota[]> {
    return this.http.get<TemplateNota[]>(`${environment.apiUrl}/templates`);
  }
}
