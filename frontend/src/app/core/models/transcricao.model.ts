/** Espelha SessaoTranscricaoDto: como o navegador deve transcrever a consulta. */
export interface SessaoTranscricao {
  modo: ModoTranscricao;
  /** Só no modo tempo real: credencial de curta duração emitida pelo provedor. */
  chaveTemporaria: string | null;
  chaveExpiraEm: string | null;
  urlConexao: string | null;
  modelo: string;
  /** Próxima sequência livre: retomar a consulta não sobrescreve o que já foi dito. */
  proximaSequencia: number;
  motivoModoAlternativo: string | null;
}

/**
 * TempoReal: o áudio vai do navegador direto ao provedor, sem passar pelo
 * servidor. Trechos: alguns segundos por vez, transcritos em memória no
 * servidor e descartados.
 */
export type ModoTranscricao = 'TempoReal' | 'Trechos';

/** Espelha TranscricaoAtualizadaDto: trechos depois de o médico proteger um termo. */
export interface TranscricaoAtualizada {
  trechos: TrechoRegistrado[];
  dadosProtegidos: number;
}

/** Espelha TrechoRegistradoDto: o trecho como o médico o vê, já sanitizado. */
export interface TrechoRegistrado {
  sequencia: number;
  texto: string;
  dadosProtegidos: number;
  falante: Falante;
  /** O médico definiu o falante; a IA não o muda mais. */
  falanteConfirmado: boolean;
}

/** Quem disse o trecho. Atribuído pela IA pelo conteúdo da fala, corrigível pelo médico. */
export type Falante = 'Indefinido' | 'Medico' | 'Paciente' | 'Acompanhante';
