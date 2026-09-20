/** Espelha LogAuditoriaDto: quem acessou o quê e quando, sem conteúdo clínico. */
export interface LogAuditoria {
  id: string;
  ocorrido: string;
  usuarioNome: string;
  acao: string;
  atendimentoId: string | null;
  pacienteNome: string | null;
  detalhe: string | null;
}

/** Rótulos legíveis das ações registradas pelo backend (AcoesAuditoria). */
export const ROTULO_ACAO: Record<string, string> = {
  'atendimento.aberto': 'Atendimento aberto',
  'atendimento.lido': 'Atendimento consultado',
  'atendimento.listado': 'Lista de atendimentos',
  'consentimento.registrado': 'Consentimento registrado',
  'audio.enviado': 'Áudio enviado',
  'processamento.solicitado': 'Reprocessamento solicitado',
  'registro.confirmado': 'Registro confirmado',
  'atendimento.cancelado': 'Atendimento cancelado',
  'nota.exportada': 'Nota exportada',
  'nota.pdf': 'Nota baixada em PDF',
  'configuracao.alterada': 'Configuração alterada',
  'audio.expurgado': 'Áudio expurgado',
};
