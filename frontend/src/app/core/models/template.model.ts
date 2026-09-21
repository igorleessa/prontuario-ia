/** Espelha TemplateNotaDto: modelo de nota por especialidade. */
export interface TemplateNota {
  id: string;
  nome: string;
  especialidade: string;
}

/** Espelha DocumentoClinicoDto. */
export interface DocumentoClinico {
  id: string;
  tipo: 'Receita' | 'PedidoExame' | 'Atestado' | 'Encaminhamento';
  conteudo: string;
  criadoEm: string;
}

/** Espelha PerfilMedicoDto: preferências do próprio usuário. */
export interface PerfilMedico {
  nome: string;
  email: string;
  instrucoesEstilo: string | null;
}

export const ROTULO_DOCUMENTO: Record<DocumentoClinico['tipo'], string> = {
  Receita: 'Receita',
  PedidoExame: 'Pedido de exame',
  Atestado: 'Atestado',
  Encaminhamento: 'Encaminhamento',
};
