using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// EMR ficticio usado para demonstrar a Modalidade B: recebe a nota clinica pelo
// webhook do Prontuario IA, confere a assinatura e mostra o documento numa tela
// que imita o prontuario de um sistema de terceiros. Nao guarda nada em disco.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var segredo = builder.Configuration["Webhook:Secret"];
var recebidas = new ConcurrentQueue<NotaRecebida>();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/webhook", async (HttpRequest requisicao) =>
{
    using var leitor = new StreamReader(requisicao.Body, Encoding.UTF8);
    var corpo = await leitor.ReadToEndAsync();

    var evento = requisicao.Headers["X-Prontuario-Evento"].ToString();
    var timestamp = requisicao.Headers["X-Prontuario-Timestamp"].ToString();
    var assinatura = requisicao.Headers["X-Prontuario-Assinatura"].ToString();

    var assinaturaValida = Conferir(segredo, timestamp, corpo, assinatura);
    if (!string.IsNullOrEmpty(segredo) && !assinaturaValida)
    {
        // Um EMR real recusa aqui: sem assinatura valida, nao ha prova de origem.
        return Results.Json(new { erro = "Assinatura invalida." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    Payload? payload;
    try
    {
        payload = JsonSerializer.Deserialize<Payload>(corpo, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { erro = "Payload invalido." });
    }

    recebidas.Enqueue(new NotaRecebida(
        DateTime.UtcNow,
        string.IsNullOrEmpty(evento) ? "desconhecido" : evento,
        assinaturaValida,
        payload?.Paciente?.Nome ?? "(sem nome)",
        payload?.Paciente?.IdExternoEmr,
        payload?.Atendimento?.Medico ?? "(sem medico)",
        payload?.Atendimento?.DataHora,
        payload?.Nota?.Conteudo ?? string.Empty));

    // A tela mostra as ultimas notas; a fila nao pode crescer sem limite.
    while (recebidas.Count > 20 && recebidas.TryDequeue(out _))
    {
    }

    return Results.Ok(new { recebido = true });
});

app.MapGet("/api/notas", () => recebidas.Reverse());
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", assinaturaExigida = !string.IsNullOrEmpty(segredo) }));

app.Run();

/// <summary>
/// Confere o HMAC no mesmo formato que o Prontuario IA gera: sha256 de
/// "timestamp.corpo", em hexadecimal minusculo.
/// </summary>
static bool Conferir(string? segredo, string timestamp, string corpo, string assinaturaRecebida)
{
    if (string.IsNullOrEmpty(segredo) || string.IsNullOrEmpty(assinaturaRecebida))
    {
        return false;
    }

    var esperado = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(segredo), Encoding.UTF8.GetBytes($"{timestamp}.{corpo}"))).ToLowerInvariant();

    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(esperado), Encoding.UTF8.GetBytes(assinaturaRecebida));
}

record NotaRecebida(
    DateTime RecebidoEm,
    string Evento,
    bool AssinaturaValida,
    string PacienteNome,
    string? PacienteIdExterno,
    string MedicoNome,
    DateTime? AtendimentoEm,
    string Conteudo);

record Payload(PayloadPaciente? Paciente, PayloadAtendimento? Atendimento, PayloadNota? Nota);
record PayloadPaciente(string? Nome, string? Cpf, string? IdExternoEmr);
record PayloadAtendimento(Guid Id, DateTime DataHora, string? Medico);
record PayloadNota(string? Formato, string? Conteudo);
