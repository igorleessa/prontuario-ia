import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'atendimento',
    loadComponent: () =>
      import('./features/atendimento/atendimento.component').then((m) => m.AtendimentoComponent),
  },
];
