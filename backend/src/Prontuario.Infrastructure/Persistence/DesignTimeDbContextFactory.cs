using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Prontuario.Infrastructure.Persistence;

/// <summary>
/// Usada apenas pelo "dotnet ef" ao gerar migrations. Existe para que as
/// migrations nao dependam de subir o host da API, que exige Jwt:Key e um banco
/// acessivel. A connection string aqui so serve para o EF escolher o provider.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=prontuario;Username=prontuario;Password=prontuario";

        var opcoes = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(conexao)
            .Options;

        return new ApplicationDbContext(opcoes);
    }
}
