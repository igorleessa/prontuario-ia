import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.obterToken();

  const requisicao = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(requisicao).pipe(
    catchError((erro: HttpErrorResponse) => {
      // Token expirado ou invalido: derruba a sessao em vez de deixar a tela
      // presa em erros silenciosos a cada requisicao.
      if (erro.status === 401 && token) {
        auth.logout();
        router.navigate(['/login']);
      }

      return throwError(() => erro);
    }),
  );
};
