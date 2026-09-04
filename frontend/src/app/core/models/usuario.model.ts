/** Espelha UsuarioLogadoDto do backend, devolvido junto com o token no login. */
export interface UsuarioLogado {
  nome: string;
  email: string;
  papel: string;
  clinicaNome: string;
  modoOperacao: 'Integrado' | 'Conector';
}
