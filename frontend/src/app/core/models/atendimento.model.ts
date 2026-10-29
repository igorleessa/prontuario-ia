import { OrigensRascunho, RascunhoClinico } from './rascunho-clinico.model';
import { TrechoRegistrado } from './transcricao.model';

/** Espelha StatusAtendimento do backend, serializado como texto. */
export type StatusAtendimento =
  | 'AguardandoConsentimento'
  | 'EmConsulta'
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
  consentimentoTranscricao: boolean;
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
  consentimentoTranscricao: boolean;
  consentimentoEm: string | null;
  rascunho: RascunhoClinico;
  /** Sugestão original da IA, preservada mesmo depois das edições do médico. */
  sugestaoIa: RascunhoClinico | null;
  /**
   * Transcrição da consulta para exibição: o paciente aparece pelo nome do
   * cadastro, e os dados de terceiros como marcadores ([PESSOA], [TELEFONE]).
   */
  transcricao: string | null;
  /** Motivo da falha do pipeline de IA, quando houve uma. */
  erroProcessamentoIA: string | null;
  /** Modelo de especialidade usado na extração; null quando foi o genérico. */
  templateNome: string | null;
  /** Tempo efetivo de escuta, sem as pausas. */
  duracaoTranscricaoSegundos: number | null;
  /** Quantos dados pessoais a sanitização retirou do que foi dito. */
  dadosPessoaisProtegidos: number;
  pacienteContato: string | null;
  /** Trechos da transcrição com a sequência real, a mesma que as origens da IA citam. */
  trechos: TrechoRegistrado[];
  /** De quais trechos a IA tirou cada campo do rascunho. */
  origensIA: OrigensRascunho;
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
  EmConsulta: 'Em consulta',
  EmRevisao: 'Em revisão',
  Finalizado: 'Finalizado',
  Cancelado: 'Cancelado',
};

/** Sufixo da classe .selo--* que colore cada status. */
export const CLASSE_STATUS: Record<StatusAtendimento, string> = {
  AguardandoConsentimento: 'aguardando',
  EmConsulta: 'consulta',
  EmRevisao: 'revisao',
  Finalizado: 'finalizado',
  Cancelado: 'cancelado',
};

export const STATUS_EM_ANDAMENTO: StatusAtendimento[] = ['AguardandoConsentimento', 'EmConsulta', 'EmRevisao'];
