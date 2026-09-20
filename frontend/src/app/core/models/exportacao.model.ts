/** Espelha ConfiguracaoExportacaoDto. Nem o segredo nem a chave voltam do servidor. */
export interface ConfiguracaoExportacao {
  modoOperacao: 'Integrado' | 'Conector';
  webhookUrl: string | null;
  segredoConfigurado: boolean;
  chaveIntegracaoPrefixo: string | null;
  chaveIntegracaoCriadaEm: string | null;
}

/** webhookSecret vazio mantém o segredo já cadastrado. */
export interface SalvarConfiguracaoExportacao {
  webhookUrl: string | null;
  webhookSecret: string | null;
}

/** Chave em claro — o servidor só a devolve no momento em que a gera. */
export interface ChaveIntegracaoGerada {
  chave: string;
  prefixo: string;
  criadaEm: string;
}

/** Espelha ResultadoExportacaoDto: usado pelo teste de webhook e pelo reenvio da nota. */
export interface ResultadoExportacao {
  sucesso: boolean;
  codigoHttp: number | null;
  erro: string | null;
  tentativas: number;
}

/** Espelha NotaExportavelDto: o que foi (ou será) enviado ao EMR do cliente. */
export interface NotaExportavel {
  conteudo: string;
  status: 'Rascunho' | 'Revisada' | 'Exportada';
  revisadaEm: string | null;
  exportadoEm: string | null;
  destino: string | null;
  tentativasExportacao: number;
  ultimoErroExportacao: string | null;
  webhookConfigurado: boolean;
}
