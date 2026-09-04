using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Domain.Entities;
using Prontuario.Infrastructure.Persistence;
using Prontuario.Infrastructure.Services;

namespace Prontuario.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<DatabaseInitializer>();

        services.AddScoped<ITranscriptionService, PlaceholderTranscriptionService>();
        services.AddScoped<IClinicalNoteGenerator, PlaceholderClinicalNoteGenerator>();

        services.AddScoped<ProntuarioNativoOutput>();
        services.AddScoped<ExportacaoEmrOutput>();
        services.AddScoped<IRegistroClinicoOutput, RegistroClinicoOutputResolver>();

        services.AddScoped<IAtendimentoService, AtendimentoService>();

        return services;
    }
}
