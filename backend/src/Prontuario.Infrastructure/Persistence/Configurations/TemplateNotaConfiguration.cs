using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prontuario.Domain.Entities;

namespace Prontuario.Infrastructure.Persistence.Configurations;

public class TemplateNotaConfiguration : IEntityTypeConfiguration<TemplateNota>
{
    public void Configure(EntityTypeBuilder<TemplateNota> builder)
    {
        builder.Property(t => t.Nome).IsRequired().HasMaxLength(120);
        builder.Property(t => t.Especialidade).IsRequired().HasMaxLength(80);
        builder.Property(t => t.Instrucoes).IsRequired().HasMaxLength(4000);

        builder.HasOne(t => t.Clinica)
            .WithMany()
            .HasForeignKey(t => t.ClinicaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DocumentoClinicoConfiguration : IEntityTypeConfiguration<DocumentoClinico>
{
    public void Configure(EntityTypeBuilder<DocumentoClinico> builder)
    {
        builder.Property(d => d.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Conteudo).IsRequired();

        builder.HasOne(d => d.Atendimento)
            .WithMany(a => a.Documentos)
            .HasForeignKey(d => d.AtendimentoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Um documento de cada tipo por atendimento: regerar substitui o anterior.
        builder.HasIndex(d => new { d.AtendimentoId, d.Tipo }).IsUnique();
    }
}
