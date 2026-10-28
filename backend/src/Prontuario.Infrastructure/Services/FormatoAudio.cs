namespace Prontuario.Infrastructure.Services;

/// <summary>
/// Identifica o container do audio pelos primeiros bytes. O navegador decide o
/// formato da gravacao (Chrome e Firefox gravam WebM/Ogg, Safari e iPad gravam
/// MP4), e a OpenAI usa o nome do arquivo para escolher o decodificador: mandar
/// um MP4 rotulado como .webm faz a transcricao falhar.
/// </summary>
public sealed record FormatoAudio(string Extensao, string TipoConteudo)
{
    public static readonly FormatoAudio WebM = new("webm", "audio/webm");

    public static FormatoAudio Detectar(ReadOnlySpan<byte> cabecalho)
    {
        if (cabecalho.Length >= 4 && cabecalho[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }))
        {
            return WebM;
        }

        if (cabecalho.Length >= 8 && cabecalho[4..8].SequenceEqual("ftyp"u8))
        {
            return new FormatoAudio("mp4", "audio/mp4");
        }

        if (cabecalho.Length >= 4 && cabecalho[..4].SequenceEqual("OggS"u8))
        {
            return new FormatoAudio("ogg", "audio/ogg");
        }

        if (cabecalho.Length >= 12 && cabecalho[..4].SequenceEqual("RIFF"u8) && cabecalho[8..12].SequenceEqual("WAVE"u8))
        {
            return new FormatoAudio("wav", "audio/wav");
        }

        if ((cabecalho.Length >= 3 && cabecalho[..3].SequenceEqual("ID3"u8))
            || (cabecalho.Length >= 2 && cabecalho[0] == 0xFF && (cabecalho[1] & 0xE0) == 0xE0))
        {
            return new FormatoAudio("mp3", "audio/mpeg");
        }

        // Formato nao reconhecido: o MediaRecorder do Chrome e o caso mais comum.
        return WebM;
    }
}
