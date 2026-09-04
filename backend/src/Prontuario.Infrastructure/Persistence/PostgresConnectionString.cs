using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Prontuario.Infrastructure.Persistence;

/// <summary>
/// Resolve a connection string do Postgres.
/// </summary>
public static class PostgresConnectionString
{
    // Senhas geradas pelo usuario podem conter ";" ou "=", caracteres que
    // separam os pares de uma connection string e a tornariam invalida se ela
    // fosse montada por concatenacao (como no docker-compose). Por isso o
    // compose passa as partes separadas em Postgres:* e quem monta o texto
    // final e o builder do Npgsql, que faz o escape necessario.
    public static string Resolver(IConfiguration configuration)
    {
        var secao = configuration.GetSection("Postgres");
        var host = secao["Host"];

        if (string.IsNullOrWhiteSpace(host))
        {
            return configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException(
                    "Configure Postgres:Host (partes separadas) ou ConnectionStrings:Postgres.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Database = secao["Database"],
            Username = secao["Username"],
            Password = secao["Password"],
        };

        if (int.TryParse(secao["Port"], out var porta))
        {
            builder.Port = porta;
        }

        return builder.ConnectionString;
    }
}
