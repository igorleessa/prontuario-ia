using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prontuario.Domain.Entities;

namespace Prontuario.Infrastructure.Persistence.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.Property(u => u.Nome).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.Papel).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(u => u.Clinica)
            .WithMany(c => c.Usuarios)
            .HasForeignKey(u => u.ClinicaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
