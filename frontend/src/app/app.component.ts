import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/services/auth.service';
import { AvatarComponent } from './shared/avatar.component';
import { IconeComponent } from './shared/icone.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, IconeComponent, AvatarComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly autenticado = this.auth.autenticado;
  readonly usuario = this.auth.usuario;
  readonly administrador = this.auth.administrador;

  /** Menu lateral em telas estreitas (tablet na mão do médico, celular). */
  readonly menuAberto = signal(false);

  alternarMenu(): void {
    this.menuAberto.update((aberto) => !aberto);
  }

  fecharMenu(): void {
    this.menuAberto.set(false);
  }

  sair(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
