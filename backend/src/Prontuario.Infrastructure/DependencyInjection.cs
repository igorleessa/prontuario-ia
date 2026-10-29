using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Privacidade;
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
            // O rascunho e refeito durante a consulta, com o medico esperando:
            // uma chamada presa nao pode segurar a tela.
            cliente.Timeout = TimeSpan.FromSeconds(90);
        });

        // Toda saida para o provedor de IA passa pela sanitizacao local.
        services.Configure<ReconhecimentoEntidadesOptions>(
            configuration.GetSection(ReconhecimentoEntidadesOptions.SectionName));
        services.AddHttpClient(ReconhecedorEntidadesSpacy.NomeCliente, (provedor, cliente) =>
        {
            var opcoes = provedor.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReconhecimentoEntidadesOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(opcoes.Url))
            {
                cliente.BaseAddress = new Uri(opcoes.Url.TrimEnd('/') + "/");
            }

            // A fala espera por esta resposta: um servico travado vira
            // "protecao indisponivel" rapido, e nao uma tela parada.
            cliente.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutSegundos);
        });
        services.AddSingleton<IReconhecedorEntidades, ReconhecedorEntidadesSpacy>();
        services.AddSingleton<ISanitizadorDados>(
            provedor => new SanitizadorDados(provedor.GetRequiredService<IReconhecedorEntidades>()));
        services.AddSingleton<CofreTermosProtegidos>();

        services.Configure<TranscricaoOptions>(configuration.GetSection(TranscricaoOptions.SectionName));
        services.AddScoped<ITranscriptionService, OpenAiTranscriptionService>();
        services.AddScoped<IProvedorTranscricaoTempoReal, OpenAiTranscricaoTempoReal>();
        services.AddScoped<IClinicalNoteGenerator, OpenAiClinicalNoteGenerator>();
        services.AddScoped<ITranscricaoConsultaService, TranscricaoConsultaService>();

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
        services.AddScoped<ICatalogoTemplatesService, CatalogoTemplatesService>();
        services.AddScoped<IDocumentoClinicoService, DocumentoClinicoService>();
        services.AddScoped<IPerfilService, PerfilService>();

        return services;
    }
}
