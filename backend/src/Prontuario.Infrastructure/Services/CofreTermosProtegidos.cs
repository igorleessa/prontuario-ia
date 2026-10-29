using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Prontuario.Domain.Entities;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Guarda os termos que o medico marcou como dado pessoal. O termo e o proprio
/// dado pessoal (um nome, uma matricula), entao vai cifrado para o banco, como
/// as chaves de API; so a sanitizacao o le, para reconhece-lo nas proximas falas.
/// </summary>
public class CofreTermosProtegidos
{
    private const string Proposito = "Prontuario.Atendimento.TermosProtegidos";

    private readonly IDataProtector _protetor;

    public CofreTermosProtegidos(IDataProtectionProvider protecao)
        => _protetor = protecao.CreateProtector(Proposito);

    public IReadOnlyList<string> Ler(Atendimento atendimento)
    {
        if (string.IsNullOrEmpty(atendimento.TermosProtegidosCifrados))
        {
            return [];
        }

        var json = _protetor.Unprotect(atendimento.TermosProtegidosCifrados);
        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }

    /// <summary>Acrescenta o termo; repetido (mesma grafia, sem diferenciar maiusculas) nao duplica.</summary>
    public void Adicionar(Atendimento atendimento, string termo)
    {
        var termos = Ler(atendimento).ToList();
        if (!termos.Contains(termo, StringComparer.OrdinalIgnoreCase))
        {
            termos.Add(termo);
        }

        atendimento.TermosProtegidosCifrados = _protetor.Protect(JsonSerializer.Serialize(termos));
    }
}
