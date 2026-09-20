using Prontuario.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Monta o PDF da nota clinica (RF18). O rodape avisa em que sistema a nota foi
/// gerada e que ela foi revisada por medico - na Modalidade B o documento circula
/// fora daqui, entao precisa se explicar sozinho.
/// </summary>
public class GeradorPdfNota : IGeradorPdfNota
{
    static GeradorPdfNota() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Gerar(DadosPdfNota dados) => Document.Create(documento =>
    {
        documento.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.Margin(2, Unit.Centimetre);
            pagina.DefaultTextStyle(estilo => estilo.FontSize(10).FontFamily("Helvetica"));

            pagina.Header().Element(elemento => Cabecalho(elemento, dados));
            pagina.Content().PaddingVertical(16).Text(dados.Conteudo).LineHeight(1.4f);
            pagina.Footer().Element(Rodape);
        });
    }).GeneratePdf();

    private static void Cabecalho(IContainer container, DadosPdfNota dados) => container.Column(coluna =>
    {
        coluna.Item().Text(dados.ClinicaNome).FontSize(15).SemiBold();
        coluna.Item().PaddingTop(2).Text("Nota clinica").FontSize(10).FontColor(Colors.Grey.Darken1);

        coluna.Item().PaddingTop(10).Row(linha =>
        {
            linha.RelativeItem().Column(esquerda =>
            {
                esquerda.Item().Text(texto =>
                {
                    texto.Span("Paciente: ").SemiBold();
                    texto.Span(dados.PacienteNome);
                });

                if (!string.IsNullOrWhiteSpace(dados.PacienteDocumento))
                {
                    esquerda.Item().Text(texto =>
                    {
                        texto.Span("Documento: ").SemiBold();
                        texto.Span(dados.PacienteDocumento);
                    });
                }
            });

            linha.RelativeItem().Column(direita =>
            {
                direita.Item().AlignRight().Text(texto =>
                {
                    texto.Span("Atendimento: ").SemiBold();
                    texto.Span(dados.DataHora.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                });

                direita.Item().AlignRight().Text(texto =>
                {
                    texto.Span("Profissional: ").SemiBold();
                    texto.Span(dados.MedicoNome);
                });
            });
        });

        coluna.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
    });

    private static void Rodape(IContainer container) => container.Column(coluna =>
    {
        coluna.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        coluna.Item().PaddingTop(6).Row(linha =>
        {
            linha.RelativeItem().Text("Gerado pelo Prontuario IA e revisado pelo profissional responsavel.")
                .FontSize(8).FontColor(Colors.Grey.Darken1);

            linha.ConstantItem(80).AlignRight().Text(texto =>
            {
                texto.DefaultTextStyle(estilo => estilo.FontSize(8).FontColor(Colors.Grey.Darken1));
                texto.CurrentPageNumber();
                texto.Span(" / ");
                texto.TotalPages();
            });
        });
    });
}
