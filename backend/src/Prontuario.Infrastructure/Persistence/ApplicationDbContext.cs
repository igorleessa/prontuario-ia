using Microsoft.EntityFrameworkCore;
using Prontuario.Domain.Entities;

namespace Prontuario.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Clinica> Clinicas => Set<Clinica>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<PacienteRef> Pacientes => Set<PacienteRef>();
    public DbSet<Atendimento> Atendimentos => Set<Atendimento>();
    public DbSet<GravacaoAudio> GravacoesAudio => Set<GravacaoAudio>();
    public DbSet<Transcricao> Transcricoes => Set<Transcricao>();
    public DbSet<RascunhoIA> RascunhosIA => Set<RascunhoIA>();
    public DbSet<ProntuarioRegistro> Prontuarios => Set<ProntuarioRegistro>();
    public DbSet<NotaExportavel> NotasExportaveis => Set<NotaExportavel>();
    public DbSet<LogAuditoria> LogsAuditoria => Set<LogAuditoria>();
    public DbSet<ConfiguracaoIA> ConfiguracoesIA => Set<ConfiguracaoIA>();
    public DbSet<TemplateNota> TemplatesNota => Set<TemplateNota>();
    public DbSet<DocumentoClinico> Documentos => Set<DocumentoClinico>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
