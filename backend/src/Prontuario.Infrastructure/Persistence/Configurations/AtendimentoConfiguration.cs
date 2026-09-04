using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prontuario.Domain.Entities;

namespace Prontuario.Infrastructure.Persistence.Configurations;

public class AtendimentoConfiguration : IEntityTypeConfiguration<Atendimento>
{
    public void Configure(EntityTypeBuilder<Atendimento> builder)
    {
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(30);

        builder.HasOne(a => a.PacienteRef)
            .WithMany(p => p.Atendimentos)
            .HasForeignKey(a => a.PacienteRefId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Medico)
            .WithMany()
            .HasForeignKey(a => a.MedicoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.GravacaoAudio)
            .WithOne(g => g.Atendimento)
            .HasForeignKey<GravacaoAudio>(g => g.AtendimentoId);

        builder.HasOne(a => a.Prontuario)
            .WithOne(p => p.Atendimento)
            .HasForeignKey<ProntuarioRegistro>(p => p.AtendimentoId);

        builder.HasOne(a => a.NotaExportavel)
            .WithOne(n => n.Atendimento)
            .HasForeignKey<NotaExportavel>(n => n.AtendimentoId);
    }
}
