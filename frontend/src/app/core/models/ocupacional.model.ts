import { Falante } from './transcricao.model';

/** As três formas de conduzir o exame ocupacional. */
export type ModoOcupacional = 'Tradicional' | 'Guiada' | 'Escriba';
export type SexoPaciente = 'F' | 'M';
export type TipoCampo = 'simnao' | 'lista' | 'opcao' | 'numero' | 'texto' | 'textolongo' | 'data';
export type OrigemResposta = 'ia' | 'medico' | 'tradicional';

/** Espelha CampoEstadoDto. */
export interface CampoOcupacional {
  id: string;
  rotulo: string;
  tipo: TipoCampo;
  opcoes: string[];
  obrigatorio: boolean;
  /** Falso para campo condicional que a resposta anterior ainda não liberou. */
  liberado: boolean;
  unidade: string | null;
  valor: string | null;
  origem: OrigemResposta | null;
  /** Resposta diferente da do exame anterior. */
  diferenca: string | null;
  porVoz: boolean;
}

export interface SecaoOcupacional {
  id: string;
  rotulo: string;
  campos: CampoOcupacional[];
  obrigatorias: number;
  respondidas: number;
  pendentes: string[];
}

export interface AlertaOcupacional {
  chave: string;
  mensagem: string;
  campos: string[];
  /** Crítico bloqueia salvar até corrigir ou justificar. */
  critico: boolean;
  justificativa: string | null;
}

/** Espelha EstadoOcupacionalDto: o prontuário inteiro, devolvido a cada alteração. */
export interface EstadoOcupacional {
  modo: ModoOcupacional;
  sexo: SexoPaciente;
  secoes: SecaoOcupacional[];
  alertas: AlertaOcupacional[];
  imc: number | null;
  auscultaCardiaca: boolean;
  auscultaPulmonar: boolean;
  anoExameAnterior: number | null;
  podeConcluir: boolean;
  concluido: boolean;
}

export interface Esclarecimento {
  campo: string;
  rotulo: string;
  pergunta: string;
}

/** Fala enviada à IA. O texto só existe na memória do navegador e durante a requisição. */
export interface FalaEnviada {
  sequencia: number;
  texto: string;
  falante?: Falante | null;
}

/** Espelha FalasProcessadasDto. */
export interface FalasProcessadas {
  estado: EstadoOcupacional;
  preenchidos: string[];
  esclarecimentos: Esclarecimento[];
  falantes: Record<number, Falante>;
  /** Falso: lote só de confirmações, a IA não foi chamada; reenviar com as próximas falas. */
  consideradas: boolean;
}

export const ROTULO_MODO: Record<ModoOcupacional, string> = {
  Tradicional: 'Forma atual',
  Guiada: 'IA guiada',
  Escriba: 'Modo Escriba',
};
