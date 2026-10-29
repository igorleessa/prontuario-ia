import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { iniciais, tomAvatar } from './pessoa';

@Component({
  selector: 'app-avatar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="avatar" aria-hidden="true"
          [style.width.rem]="tamanho()" [style.height.rem]="tamanho()"
          [style.font-size.rem]="tamanho() * 0.38"
          [style.background]="tom().fundo" [style.color]="tom().texto">{{ letras() }}</span>
  `,
  styles: [':host { display: inline-flex; }'],
})
export class AvatarComponent {
  readonly nome = input<string | null | undefined>(null);
  readonly tamanho = input(2.25);

  readonly letras = computed(() => iniciais(this.nome()));
  readonly tom = computed(() => tomAvatar(this.nome()));
}
