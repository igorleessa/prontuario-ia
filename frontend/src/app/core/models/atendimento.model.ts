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
  /** Sugestão original da IA, preservada mesmo depois das edições do médico. */
  sugestaoIa: RascunhoClinico | null;
  /** Texto que o STT extraiu do áudio; null enquanto não houver gravação processada. */
  transcricao: string | null;
  /** Motivo da falha do pipeline de IA, quando houve uma. */
  erroProcessamentoIA: string | null;
  /** Modelo de especialidade usado na extração; null quando foi o genérico. */
  templateNome: string | null;
  duracaoGravacaoSegundos: number | null;
}

/** Espelha MetricasClinicaDto. O tempo economizado é estimativa declarada. */
export interface MetricasClinica {
  atendimentosFinalizados: number;
  minutosDeConsultaDocumentados: number;
  tempoMedioRevisaoSegundos: number | null;
  minutosDocumentacaoManualEstimados: number;
  economiaPercentualEstimada: number | null;
}

/** Espelha HistoricoPacienteDto (RF16). */
export interface HistoricoPaciente {
  pacienteId: string;
  pacienteNome: string;
  cpf: string | null;
  dataNascimento: string | null;
  idExternoEmr: string | null;
  atendimentos: HistoricoAtendimento[];
}

export interface HistoricoAtendimento {
  id: string;
  dataHora: string;
  status: StatusAtendimento;
  medicoNome: string;
  queixaPrincipal: string | null;
  hipoteseDiagnostica: string | null;
  finalizado: boolean;
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
