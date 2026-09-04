/** Espelha PacienteResumoDto do backend. */
export interface Paciente {
  id: string;
  nome: string;
  cpf: string | null;
  dataNascimento: string | null;
  contato: string | null;
  idExternoEmr: string | null;
}

/** Espelha NovoPacienteDto do backend. */
export interface NovoPaciente {
  nome: string;
  cpf: string | null;
  dataNascimento: string | null;
  contato: string | null;
  idExternoEmr: string | null;
}
