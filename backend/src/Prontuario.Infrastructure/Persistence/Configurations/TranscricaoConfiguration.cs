using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prontuario.Domain.Entities;

namespace Prontuario.Infrastructure.Persistence.Configurations;

public class GravacaoAudioConfiguration : IEntityTypeConfiguration<GravacaoAudio>
{
    public void Configure(EntityTypeBuilder<GravacaoAudio> builder)
    {
        builder.Property(g => g.StoragePath).IsRequired().HasMaxLength(500);

        builder.HasOne(g => g.Transcricao)
            .WithOne(t => t.GravacaoAudio)
            .HasForeignKey<Transcricao>(t => t.GravacaoAudioId);
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
