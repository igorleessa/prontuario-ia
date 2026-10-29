import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Rotulo legivel de cada marcador que a sanitizacao do backend aplica. */
const ROTULO_MARCADOR: Record<string, string> = {
  PESSOA: 'nome',
  CPF: 'CPF',
  CNS: 'cartão SUS',
  TELEFONE: 'telefone',
  EMAIL: 'e-mail',
  CEP: 'CEP',
  ENDERECO: 'endereço',
  DOCUMENTO: 'documento',
  PACIENTE: 'paciente',
  MEDICO: 'médico',
  CPF_PACIENTE: 'CPF',
  CONTATO_PACIENTE: 'contato',
  DATA_NASCIMENTO: 'nascimento',
  DADO_PESSOAL: 'dado pessoal',
  LOCAL: 'local',
  ORGANIZACAO: 'organização',
};

interface Pedaco {
  texto: string;
  marcador: boolean;
}

/**
 * Texto com os dados pessoais apagados pela sanitizacao destacados como
 * etiquetas ("nome protegido"), em vez de colchetes crus. O medico ve, na
 * propria transcricao, o que nao saiu do servidor.
 */
@Component({
  selector: 'app-texto-protegido',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@for (pedaco of pedacos(); track $index) {@if (pedaco.marcador) {<span class="marcador-privado" title="Dado pessoal protegido: não foi enviado à IA">{{ pedaco.texto }}</span>} @else {{{ pedaco.texto }}}}`,
})
export class TextoProtegidoComponent {
  readonly texto = input<string | null | undefined>('');

  readonly pedacos = computed<Pedaco[]>(() => {
    const texto = this.texto() ?? '';
    const pedacos: Pedaco[] = [];
    let ultimo = 0;

    for (const achado of texto.matchAll(/\[([A-Z_]+)\]/g)) {
      const rotulo = ROTULO_MARCADOR[achado[1]!];
      if (!rotulo) {
        continue;
      }

      if (achado.index! > ultimo) {
        pedacos.push({ texto: texto.slice(ultimo, achado.index), marcador: false });
      }

      pedacos.push({ texto: `${rotulo} protegido`, marcador: true });
      ultimo = achado.index! + achado[0].length;
    }

    if (ultimo < texto.length) {
      pedacos.push({ texto: texto.slice(ultimo), marcador: false });
    }

    return pedacos;
  });
}
