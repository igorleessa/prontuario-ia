using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prontuario.Domain.Entities;

namespace Prontuario.Infrastructure.Persistence.Configurations;

public class ConfiguracaoIAConfiguration : IEntityTypeConfiguration<ConfiguracaoIA>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoIA> builder)
    {
        builder.Property(c => c.ChaveApiProtegida).IsRequired();
        builder.Property(c => c.ChaveApiSufixo).IsRequired().HasMaxLength(8);
        builder.Property(c => c.ModeloTranscricao).IsRequired().HasMaxLength(100);
        builder.Property(c => c.ModeloTexto).IsRequired().HasMaxLength(100);

        // Uma configuracao por clinica.
        builder.HasIndex(c => c.ClinicaId).IsUnique();

        builder.HasOne(c => c.Clinica)
            .WithMany()
            .HasForeignKey(c => c.ClinicaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
