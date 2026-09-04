using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prontuario.Domain.Entities;

namespace Prontuario.Infrastructure.Persistence.Configurations;

public class ClinicaConfiguration : IEntityTypeConfiguration<Clinica>
{
    public void Configure(EntityTypeBuilder<Clinica> builder)
    {
        builder.Property(c => c.Nome).IsRequired().HasMaxLength(200);
        builder.Property(c => c.ModoOperacao).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.WebhookUrl).HasMaxLength(500);
    }
}
