import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

const TOKEN_KEY = 'prontuario.token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  readonly autenticado = signal(this.obterToken() !== null);

  login(email: string, senha: string): Observable<{ token: string }> {
    return this.http
      .post<{ token: string }>(`${environment.apiUrl}/auth/login`, { email, senha })
      .pipe(
        tap(({ token }) => {
          localStorage.setItem(TOKEN_KEY, token);
          this.autenticado.set(true);
        }),
      );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    this.autenticado.set(false);
  }

  obterToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }
}
