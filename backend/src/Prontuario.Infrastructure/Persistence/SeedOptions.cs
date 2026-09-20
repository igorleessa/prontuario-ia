namespace Prontuario.Infrastructure.Persistence;

/// <summary>
/// Dados semeados no primeiro start para permitir testar o fluxo localmente.
/// Preenchido pelo script de setup (scripts/setup.sh / setup.ps1) via .env.
/// </summary>
public class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Habilitado { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
    public string NomeMedico { get; set; } = "Medico de Teste";
    public string NomeClinica { get; set; } = "Clinica de Teste";

    /// <summary>"Integrado" ou "Conector" - ver docs/especificacao-mvp.md, secao 2.</summary>
    public string ModoOperacao { get; set; } = "Integrado";

    public string NomePacienteExemplo { get; set; } = "Paciente de Teste";

    /// <summary>
    /// Administrador da clinica semeado ao lado do medico. As telas de
    /// configuracao exigem este papel: quem atende nao mexe nas credenciais nem
    /// no destino de exportacao da clinica. Vazio desliga a criacao.
    /// </summary>
    public string EmailAdministrador { get; set; } = string.Empty;

    public string NomeAdministrador { get; set; } = "Administrador da Clinica";
}
