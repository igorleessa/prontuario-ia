using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Prontuario.Api;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Infrastructure;
using Prontuario.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioAtual, UsuarioAtual>();

builder.Services.AddInfrastructure(builder.Configuration);

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key ausente ou curta demais (minimo 32 bytes). Rode scripts/setup.sh (macOS/Linux) "
        + "ou scripts/setup.ps1 (Windows) para gerar o .env com uma chave aleatoria.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
    });
builder.Services.AddAuthorization();

var origensPermitidas = builder.Configuration.GetSection("Cors:OrigensPermitidas").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(origensPermitidas).AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// Em ambiente local o proprio start aplica as migrations e semeia os dados de
// teste, para que "docker compose up" deixe o sistema pronto para uso.
if (builder.Configuration.GetValue<bool>("Database:AplicarMigrationsNaInicializacao"))
{
    using var scope = app.Services.CreateScope();
    var inicializador = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await inicializador.InicializarAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Em desenvolvimento o container sobe apenas em HTTP; redirecionar para HTTPS
// quebraria as chamadas do frontend local.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// Capacidades que a interface precisa conhecer antes do login. Nao carrega
// nenhum dado de clinica nem de paciente.
app.MapGet("/api/configuracao-app", (IConfiguration configuracao) => Results.Ok(new
{
    demonstracao = configuracao.GetValue<bool>("Demonstracao:Habilitado"),
}));

app.Run();
