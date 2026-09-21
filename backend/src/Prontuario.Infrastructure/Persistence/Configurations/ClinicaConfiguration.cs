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
        builder.Property(c => c.ChaveIntegracaoHash).HasMaxLength(64);
        builder.Property(c => c.ChaveIntegracaoPrefixo).HasMaxLength(16);

        // A chave chega pelo cabecalho em toda requisicao da API de integracao:
        // sem indice, cada chamada varreria a tabela de clinicas.
        builder.HasIndex(c => c.ChaveIntegracaoHash);
    }
}
