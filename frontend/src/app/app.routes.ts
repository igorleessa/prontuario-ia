import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'atendimentos' },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'atendimentos',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/atendimentos/lista/atendimentos.component').then((m) => m.AtendimentosComponent),
  },
  {
    path: 'atendimentos/novo',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/atendimentos/novo/novo-atendimento.component').then((m) => m.NovoAtendimentoComponent),
  },
  {
    path: 'atendimentos/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/atendimento/atendimento.component').then((m) => m.AtendimentoComponent),
  },
  {
    path: 'perfil',
    canActivate: [authGuard],
    loadComponent: () => import('./features/perfil/perfil.component').then((m) => m.PerfilComponent),
  },
  {
    path: 'auditoria',
    canActivate: [authGuard, adminGuard],
    loadComponent: () =>
      import('./features/auditoria/auditoria.component').then((m) => m.AuditoriaComponent),
  },
  {
    path: 'configuracoes',
    canActivate: [authGuard, adminGuard],
    loadComponent: () =>
      import('./features/configuracoes/configuracao-ia.component').then((m) => m.ConfiguracaoIAComponent),
  },
  { path: '**', redirectTo: 'atendimentos' },
];
