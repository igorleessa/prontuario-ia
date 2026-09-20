using Microsoft.AspNetCore.DataProtection;
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
            options.UseNpgsql(PostgresConnectionString.Resolver(configuration)));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<DatabaseInitializer>();

        services.Configure<ArmazenamentoAudioOptions>(
            configuration.GetSection(ArmazenamentoAudioOptions.SectionName));
        services.AddSingleton<IArmazenamentoAudio, ArmazenamentoAudioMinio>();

        // O chaveiro precisa sobreviver ao container: sem isso, toda recriacao
        // tornaria ilegiveis as chaves de API ja cadastradas pelas clinicas.
        var caminhoChaves = configuration["DataProtection:Caminho"];
        var protecao = services.AddDataProtection().SetApplicationName("ProntuarioIA");
        if (!string.IsNullOrWhiteSpace(caminhoChaves))
        {
            protecao.PersistKeysToFileSystem(new DirectoryInfo(caminhoChaves));
        }

        services.AddScoped<IAuditoriaService, AuditoriaService>();
        services.AddScoped<IConfiguracaoIAService, ConfiguracaoIAService>();

        services.AddHttpClient(OpenAiCliente.Nome, cliente =>
        {
            cliente.BaseAddress = new Uri(OpenAiCliente.BaseUrl);
            // Transcricao de consulta inteira e lenta; o padrao de 100s estoura.
            cliente.Timeout = TimeSpan.FromMinutes(10);
        });

        services.AddScoped<ITranscriptionService, OpenAiTranscriptionService>();
        services.AddScoped<IClinicalNoteGenerator, OpenAiClinicalNoteGenerator>();

        services.AddSingleton<IFilaProcessamentoIA, FilaProcessamentoIA>();
        services.AddHostedService<ProcessamentoIAWorker>();
        services.AddHostedService<RetencaoAudioWorker>();

        services.AddSingleton<INotaClinicaFormatter, NotaClinicaFormatter>();
        services.AddSingleton<IGeradorPdfNota, GeradorPdfNota>();

        services.AddScoped<IConfiguracaoExportacaoService, ConfiguracaoExportacaoService>();
        services.AddHttpClient(WebhookExportador.NomeCliente, cliente =>
        {
            // O medico espera a confirmacao na tela: um EMR lento nao pode
            // prender a requisicao por muito tempo.
            cliente.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<IExportadorNota, WebhookExportador>();

        services.AddScoped<ProntuarioNativoOutput>();
        services.AddScoped<ExportacaoEmrOutput>();
        services.AddScoped<IRegistroClinicoOutput, RegistroClinicoOutputResolver>();

        services.Configure<IntegracaoOptions>(configuration.GetSection(IntegracaoOptions.SectionName));
        services.AddScoped<IIntegracaoService, IntegracaoService>();

        services.AddScoped<IAtendimentoService, AtendimentoService>();
        services.AddScoped<IPacienteService, PacienteService>();

        return services;
    }
}
