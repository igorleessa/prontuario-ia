import { RascunhoClinico } from './rascunho-clinico.model';

/** Espelha StatusAtendimento do backend, serializado como texto. */
export type StatusAtendimento =
  | 'AguardandoConsentimento'
  | 'EmGravacao'
  | 'ProcessandoIA'
  | 'EmRevisao'
  | 'Finalizado'
  | 'Cancelado';

/** Espelha AtendimentoResumoDto do backend. */
export interface AtendimentoResumo {
  id: string;
  pacienteId: string;
  pacienteNome: string;
  medicoNome: string;
  dataHora: string;
  status: StatusAtendimento;
  consentimentoGravacao: boolean;
}

/** Espelha AtendimentoDetalheDto do backend. */
export interface AtendimentoDetalhe {
  id: string;
  pacienteId: string;
  pacienteNome: string;
  pacienteCpf: string | null;
  pacienteDataNascimento: string | null;
  medicoNome: string;
  dataHora: string;
  status: StatusAtendimento;
  consentimentoGravacao: boolean;
  consentimentoEm: string | null;
  rascunho: RascunhoClinico;
  /** Texto que o STT extraiu do áudio; null enquanto não houver gravação processada. */
  transcricao: string | null;
  /** Motivo da falha do pipeline de IA, quando houve uma. */
  erroProcessamentoIA: string | null;
}

export const ROTULO_STATUS: Record<StatusAtendimento, string> = {
  AguardandoConsentimento: 'Aguardando consentimento',
  EmGravacao: 'Em gravação',
  ProcessandoIA: 'Processando IA',
  EmRevisao: 'Em revisão',
  Finalizado: 'Finalizado',
  Cancelado: 'Cancelado',
};

/** Sufixo da classe .selo--* que colore cada status. */
export const CLASSE_STATUS: Record<StatusAtendimento, string> = {
  AguardandoConsentimento: 'aguardando',
  EmGravacao: 'gravacao',
  ProcessandoIA: 'processando',
  EmRevisao: 'revisao',
  Finalizado: 'finalizado',
  Cancelado: 'cancelado',
};
