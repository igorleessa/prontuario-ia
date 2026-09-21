using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prontuario.Application.Common.Interfaces;
using Prontuario.Application.Common.Models;

namespace Prontuario.Api.Controllers;

/// <summary>
/// Receita, pedido de exame, atestado e encaminhamento redigidos a partir da
/// consulta. Como a nota clinica, saem como rascunho: o medico revisa antes de
/// o documento valer.
/// </summary>
[ApiController]
[Authorize]
[Route("api/atendimentos/{atendimentoId:guid}/documentos")]
public class DocumentosController : ApiControllerBase
{
    private readonly IDocumentoClinicoService _documentos;
    private readonly IGeradorPdfNota _pdf;
    private readonly IAtendimentoService _atendimentos;

    public DocumentosController(
        IDocumentoClinicoService documentos, IGeradorPdfNota pdf, IAtendimentoService atendimentos)
    {
        _documentos = documentos;
        _pdf = pdf;
        _atendimentos = atendimentos;
    }

    public record GerarDocumentoRequest(string Tipo);

    public record SalvarDocumentoRequest(string Conteudo);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentoClinicoDto>>> Listar(
        Guid atendimentoId, CancellationToken cancellationToken)
        => Ok(await _documentos.ListarAsync(atendimentoId, ClinicaId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<DocumentoClinicoDto>> Gerar(
        Guid atendimentoId, GerarDocumentoRequest pedido, CancellationToken cancellationToken)
    {
        try
        {
            var documento = await _documentos.GerarAsync(atendimentoId, ClinicaId, pedido.Tipo, cancellationToken);
            return documento is null ? NotFound() : Ok(documento);
        }
        catch (InvalidOperationException excecao)
        {
            return BadRequest(new { erro = excecao.Message });
        }
    }

    [HttpPut("{documentoId:guid}")]
    public async Task<ActionResult<DocumentoClinicoDto>> Salvar(
        Guid atendimentoId, Guid documentoId, SalvarDocumentoRequest revisado, CancellationToken cancellationToken)
    {
        var documento = await _documentos.SalvarAsync(
            atendimentoId, ClinicaId, documentoId, revisado.Conteudo, cancellationToken);

        return documento is null ? NotFound() : Ok(documento);
    }

    /// <summary>PDF do documento revisado, com o mesmo cabecalho da nota clinica.</summary>
    [HttpGet("{documentoId:guid}/pdf")]
    public async Task<IActionResult> BaixarPdf(
        Guid atendimentoId, Guid documentoId, CancellationToken cancellationToken)
    {
        var documentos = await _documentos.ListarAsync(atendimentoId, ClinicaId, cancellationToken);
        var documento = documentos.FirstOrDefault(d => d.Id == documentoId);
        var contexto = await _atendimentos.ObterDadosPdfAsync(atendimentoId, ClinicaId, cancellationToken);

        if (documento is null || contexto is null)
        {
            return NotFound();
        }

        var dados = contexto with { Conteudo = documento.Conteudo };
        return File(_pdf.Gerar(dados), "application/pdf", $"{documento.Tipo.ToLowerInvariant()}.pdf");
    }
}
