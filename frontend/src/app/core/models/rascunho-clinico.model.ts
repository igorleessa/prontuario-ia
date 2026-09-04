/** Espelha RascunhoClinicoDto do backend (RF08). */
export interface RascunhoClinico {
  queixaPrincipal: string | null;
  hda: string | null;
  antecedentes: string | null;
  exameFisico: string | null;
  hipoteseDiagnostica: string | null;
  cid10Sugerido: string | null;
  conduta: string | null;
}

export const RASCUNHO_VAZIO: RascunhoClinico = {
  queixaPrincipal: null,
  hda: null,
  antecedentes: null,
  exameFisico: null,
  hipoteseDiagnostica: null,
  cid10Sugerido: null,
  conduta: null,
};
