using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prontuario.Domain.Entities;

namespace Prontuario.Infrastructure.Persistence.Configurations;

public class SessaoTranscricaoConfiguration : IEntityTypeConfiguration<SessaoTranscricao>
{
    public void Configure(EntityTypeBuilder<SessaoTranscricao> builder)
    {
        builder.HasOne(s => s.Transcricao)
            .WithOne(t => t.SessaoTranscricao)
            .HasForeignKey<Transcricao>(t => t.SessaoTranscricaoId);
    }
}

public class TranscricaoConfiguration : IEntityTypeConfiguration<Transcricao>
{
    public void Configure(EntityTypeBuilder<Transcricao> builder)
    {
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(t => t.RascunhoIA)
            .WithOne(r => r.Transcricao)
            .HasForeignKey<RascunhoIA>(r => r.TranscricaoId);

        builder.HasMany(t => t.Trechos)
            .WithOne(tr => tr.Transcricao)
            .HasForeignKey(tr => tr.TranscricaoId);
    }
}

public class TrechoTranscricaoConfiguration : IEntityTypeConfiguration<TrechoTranscricao>
{
    public void Configure(EntityTypeBuilder<TrechoTranscricao> builder)
    {
        // A sequencia identifica o trecho: reenvio substitui, nao duplica.
        builder.HasIndex(t => new { t.TranscricaoId, t.Sequencia }).IsUnique();
        builder.Property(t => t.Texto).IsRequired();
        builder.Property(t => t.Falante).HasConversion<string>().HasMaxLength(20);
    }
}

public class NotaExportavelConfiguration : IEntityTypeConfiguration<NotaExportavel>
{
    public void Configure(EntityTypeBuilder<NotaExportavel> builder)
    {
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(n => n.Destino).HasMaxLength(500);
        builder.Property(n => n.UltimoErroExportacao).HasMaxLength(500);
    }
}

public class LogAuditoriaConfiguration : IEntityTypeConfiguration<LogAuditoria>
{
    public void Configure(EntityTypeBuilder<LogAuditoria> builder)
    {
        builder.Property(l => l.Acao).IsRequired().HasMaxLength(100);
        builder.Property(l => l.Detalhe).HasMaxLength(500);

        // A tela de auditoria lista sempre do mais recente para o mais antigo.
        builder.HasIndex(l => l.CriadoEm);

        builder.HasOne(l => l.Usuario)
            .WithMany()
            .HasForeignKey(l => l.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Atendimento)
            .WithMany(a => a.LogsAuditoria)
            .HasForeignKey(l => l.AtendimentoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
