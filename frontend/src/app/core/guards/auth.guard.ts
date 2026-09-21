import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/** Mantem as telas clinicas fora do alcance de quem nao esta autenticado (RF01). */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.autenticado() ? true : router.createUrlTree(['/login']);
};

/**
 * Telas de clínica (configuração e auditoria) são do administrador. O backend
 * recusa de qualquer forma; o guard evita mostrar uma tela que daria 403.
 */
export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.administrador() ? true : router.createUrlTree(['/atendimentos']);
};
