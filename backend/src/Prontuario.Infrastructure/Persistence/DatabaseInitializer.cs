using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prontuario.Domain.Entities;
using Prontuario.Domain.Enums;

namespace Prontuario.Infrastructure.Persistence;

/// <summary>
/// Aplica as migrations pendentes e, quando habilitado, semeia uma clinica,
/// um medico e um paciente para testes locais. Idempotente: nao semeia de novo
/// se ja existir usuario cadastrado.
/// </summary>
public class DatabaseInitializer
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<Usuario> _passwordHasher;
    private readonly SeedOptions _seed;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        ApplicationDbContext db,
        IPasswordHasher<Usuario> passwordHasher,
        IOptions<SeedOptions> seed,
        ILogger<DatabaseInitializer> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _seed = seed.Value;
        _logger = logger;
    }

    public async Task InicializarAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Migrations aplicadas.");

        if (!_seed.Habilitado)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_seed.Email) || string.IsNullOrWhiteSpace(_seed.Senha))
        {
            _logger.LogWarning("Seed habilitado mas sem e-mail/senha configurados. Nada foi semeado.");
            return;
        }

        if (await _db.Usuarios.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Banco ja possui usuarios. Seed ignorado.");
            return;
        }

        var modoOperacao = Enum.TryParse<ModoOperacao>(_seed.ModoOperacao, ignoreCase: true, out var modo)
            ? modo
            : ModoOperacao.Integrado;

        var clinica = new Clinica { Nome = _seed.NomeClinica, ModoOperacao = modoOperacao };

        var medico = new Usuario
        {
            ClinicaId = clinica.Id,
            Nome = _seed.NomeMedico,
            Email = _seed.Email,
            Papel = PapelUsuario.Medico,
        };
        medico.SenhaHash = _passwordHasher.HashPassword(medico, _seed.Senha);

        var paciente = new PacienteRef
        {
            ClinicaId = clinica.Id,
            Nome = _seed.NomePacienteExemplo,
        };

        _db.Clinicas.Add(clinica);
        _db.Usuarios.Add(medico);
        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seed concluido. Clinica {Clinica} ({Modo}), medico {Email}, paciente exemplo {PacienteId}.",
            clinica.Nome, modoOperacao, medico.Email, paciente.Id);
    }
}
