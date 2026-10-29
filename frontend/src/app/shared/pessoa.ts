/** Utilitarios de exibicao de pessoa, usados nas fichas de paciente e no menu. */

/** Iniciais para o avatar: primeira letra do primeiro e do ultimo nome. */
export function iniciais(nome: string | null | undefined): string {
  const partes = (nome ?? '')
    .trim()
    .split(/\s+/)
    .filter((parte) => parte.length > 0 && !/^(dra?|sra?|da|de|do|das|dos)\.?$/i.test(parte));

  if (partes.length === 0) {
    return '–';
  }

  const primeira = partes[0]!.charAt(0);
  const ultima = partes.length > 1 ? partes[partes.length - 1]!.charAt(0) : '';
  return (primeira + ultima).toUpperCase();
}

/** Idade em anos completos a partir da data de nascimento (yyyy-MM-dd). */
export function idade(dataNascimento: string | null | undefined): number | null {
  if (!dataNascimento) {
    return null;
  }

  const [ano, mes, dia] = dataNascimento.split('-').map(Number);
  if (!ano || !mes || !dia) {
    return null;
  }

  const hoje = new Date();
  let anos = hoje.getFullYear() - ano;
  const aniversarioAindaNaoOcorreu =
    hoje.getMonth() + 1 < mes || (hoje.getMonth() + 1 === mes && hoje.getDate() < dia);

  if (aniversarioAindaNaoOcorreu) {
    anos -= 1;
  }

  return anos;
}

/**
 * CPF com o miolo oculto ("529.***.***-25"). A ficha do paciente fica na tela
 * durante a consulta, de frente para quem estiver na sala.
 */
export function cpfMascarado(cpf: string | null | undefined): string | null {
  const digitos = (cpf ?? '').replace(/\D/g, '');
  if (digitos.length !== 11) {
    return cpf ?? null;
  }

  return `${digitos.slice(0, 3)}.***.***-${digitos.slice(9)}`;
}

/** Tons do avatar: estaveis por nome, para o mesmo paciente ter sempre a mesma cor. */
const TONS = [
  ['#e6f4f3', '#08615d'],
  ['#edf4fe', '#1b4f9c'],
  ['#f1eeff', '#5136c9'],
  ['#fff1e6', '#9a4a09'],
  ['#fdeef3', '#a3264f'],
  ['#eaf6ec', '#1f6b34'],
] as const;

export function tomAvatar(nome: string | null | undefined): { fundo: string; texto: string } {
  let soma = 0;
  for (const caractere of nome ?? '') {
    soma = (soma * 31 + caractere.charCodeAt(0)) >>> 0;
  }

  const [fundo, texto] = TONS[soma % TONS.length]!;
  return { fundo, texto };
}
