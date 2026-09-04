/** Espelha ConfiguracaoIADto do backend. A chave em si nunca trafega de volta. */
export interface ConfiguracaoIA {
  chaveConfigurada: boolean;
  chaveApiSufixo: string | null;
  modeloTranscricao: string;
  modeloTexto: string;
  atualizadoEm: string | null;
}

/** Espelha SalvarConfiguracaoIADto. chaveApi vazia mantém a chave já cadastrada. */
export interface SalvarConfiguracaoIA {
  chaveApi: string | null;
  modeloTranscricao: string;
  modeloTexto: string;
}
