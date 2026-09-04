using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prontuario.Domain.Entities;

namespace Prontuario.Infrastructure.Persistence.Configurations;

public class PacienteRefConfiguration : IEntityTypeConfiguration<PacienteRef>
{
    public void Configure(EntityTypeBuilder<PacienteRef> builder)
    {
        builder.Property(p => p.Nome).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Cpf).HasMaxLength(14);
        builder.Property(p => p.IdExternoEmr).HasMaxLength(100);

        builder.HasOne(p => p.Clinica)
            .WithMany(c => c.Pacientes)
            .HasForeignKey(p => p.ClinicaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
