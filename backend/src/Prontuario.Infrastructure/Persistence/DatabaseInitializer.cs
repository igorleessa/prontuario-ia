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
    /// <summary>
    /// Cria o administrador em uma clinica que ainda nao tem nenhum, sem tocar
    /// no resto do banco. Idempotente.
    /// </summary>
    private async Task GarantirAdministradorAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_seed.EmailAdministrador) || string.IsNullOrWhiteSpace(_seed.Senha))
        {
            return;
        }

        if (await _db.Usuarios.AnyAsync(u => u.Email == _seed.EmailAdministrador, cancellationToken))
        {
            return;
        }

        var clinicaId = await _db.Clinicas
            .OrderBy(c => c.CriadoEm)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (clinicaId is null)
        {
            return;
        }

        var administrador = new Usuario
        {
            ClinicaId = clinicaId.Value,
            Nome = _seed.NomeAdministrador,
            Email = _seed.EmailAdministrador,
            Papel = PapelUsuario.Administrador,
        };
        administrador.SenhaHash = _passwordHasher.HashPassword(administrador, _seed.Senha);

        _db.Usuarios.Add(administrador);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Administrador {Email} criado na clinica existente.", administrador.Email);
    }

    /// <summary>
    /// Catalogo de templates: independe do seed de teste, porque e conteudo do
    /// produto, nao dado de demonstracao. Idempotente por nome.
    /// </summary>
    private async Task SemearCatalogoTemplatesAsync(CancellationToken cancellationToken)
    {
        var existentes = await _db.TemplatesNota
            .Where(t => t.ClinicaId == null)
            .Select(t => t.Nome)
            .ToListAsync(cancellationToken);

        var novos = CatalogoTemplatesSeed.Modelos()
            .Where(t => !existentes.Contains(t.Nome))
            .ToList();

        if (novos.Count == 0)
        {
            return;
        }

        _db.TemplatesNota.AddRange(novos);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Catalogo de templates: {Total} modelos adicionados.", novos.Count);
    }

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

        await SemearCatalogoTemplatesAsync(cancellationToken);

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

            // Instalacao anterior a existencia do papel de administrador ficaria
            // sem ninguem capaz de configurar a clinica, e recriar o banco so
            // por isso custaria os atendimentos ja registrados.
            await GarantirAdministradorAsync(cancellationToken);
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

        if (!string.IsNullOrWhiteSpace(_seed.EmailAdministrador))
        {
            var administrador = new Usuario
            {
                ClinicaId = clinica.Id,
                Nome = _seed.NomeAdministrador,
                Email = _seed.EmailAdministrador,
                Papel = PapelUsuario.Administrador,
            };
            administrador.SenhaHash = _passwordHasher.HashPassword(administrador, _seed.Senha);
            _db.Usuarios.Add(administrador);
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seed concluido. Clinica {Clinica} ({Modo}), medico {Email}, administrador {Admin}, paciente exemplo {PacienteId}.",
            clinica.Nome, modoOperacao, medico.Email,
            string.IsNullOrWhiteSpace(_seed.EmailAdministrador) ? "(nenhum)" : _seed.EmailAdministrador,
            paciente.Id);
    }
}
