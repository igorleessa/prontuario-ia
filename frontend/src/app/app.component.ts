import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from './core/services/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly autenticado = this.auth.autenticado;
  readonly usuario = this.auth.usuario;
  readonly administrador = this.auth.administrador;

  /** Iniciais do medico para o avatar do cabecalho. */
  readonly iniciais = computed(() => {
    const nome = this.usuario()?.nome?.trim();
    if (!nome) {
      return '–';
    }

    const partes = nome.split(/\s+/);
    const primeira = partes[0]!.charAt(0);
    const ultima = partes.length > 1 ? partes[partes.length - 1]!.charAt(0) : '';
    return (primeira + ultima).toUpperCase();
  });

  sair(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
