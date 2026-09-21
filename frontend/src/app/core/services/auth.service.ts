import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { UsuarioLogado } from '../models/usuario.model';

const TOKEN_KEY = 'prontuario.token';
const USUARIO_KEY = 'prontuario.usuario';

interface LoginResposta {
  token: string;
  usuario: UsuarioLogado;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  readonly autenticado = signal(this.obterToken() !== null);
  readonly usuario = signal<UsuarioLogado | null>(this.lerUsuarioSalvo());

  /**
   * Administrador configura a clinica e ve a auditoria; medico atende. A
   * autorizacao de verdade esta no backend - aqui so evitamos oferecer uma
   * tela que responderia 403.
   */
  readonly administrador = computed(() => this.usuario()?.papel === 'Administrador');

  login(email: string, senha: string): Observable<LoginResposta> {
    return this.http.post<LoginResposta>(`${environment.apiUrl}/auth/login`, { email, senha }).pipe(
      tap(({ token, usuario }) => {
        localStorage.setItem(TOKEN_KEY, token);
        localStorage.setItem(USUARIO_KEY, JSON.stringify(usuario));
        this.autenticado.set(true);
        this.usuario.set(usuario);
      }),
    );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USUARIO_KEY);
    this.autenticado.set(false);
    this.usuario.set(null);
  }

  obterToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  private lerUsuarioSalvo(): UsuarioLogado | null {
    const bruto = localStorage.getItem(USUARIO_KEY);
    if (!bruto) {
      return null;
    }

    // O conteudo salvo pode estar de uma versao anterior do app; um JSON
    // invalido nao deve impedir o carregamento da aplicacao.
    try {
      return JSON.parse(bruto) as UsuarioLogado;
    } catch {
      return null;
    }
  }
}
