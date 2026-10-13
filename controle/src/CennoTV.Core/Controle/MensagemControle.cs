using System.Text.Json;
using System.Text.RegularExpressions;

namespace CennoTV.Core.Controle;

/// <summary>Uma ordem do controle remoto, já validada.</summary>
public abstract record MensagemControle
{
    public sealed record Tecla(ComandoTv Comando) : MensagemControle;
    /// <summary>Abre um item do catálogo pelo id (o controle nunca manda destino).</summary>
    public sealed record AbrirItem(string Id) : MensagemControle;
    public sealed record MoverMouse(int Dx, int Dy) : MensagemControle;
    public sealed record Clique(bool Direito) : MensagemControle;
    public sealed record Rolar(int Passos) : MensagemControle;
    /// <summary>Texto digitado como teclas no app em foco (busca, senha de Wi-Fi). Nunca é executado.</summary>
    public sealed record Texto(string Conteudo) : MensagemControle;
    /// <summary>Botão do modo Jogo: fica apertado até chegar o "solto" (andar segurando a direção).</summary>
    public sealed record Botao(BotaoJogo Qual, bool Apertado) : MensagemControle;
}

/// <summary>Botões do modo Jogo do controle (o desenho de um controle de SNES).</summary>
public enum BotaoJogo
{
    Cima,
    Baixo,
    Esquerda,
    Direita,
    A,
    B,
    X,
    Y,
    L,
    R,
    Start,
    Select,
    /// <summary>Gatilhos: no N64 o L2 é o Z.</summary>
    L2,
    R2,
}

/// <summary>
/// Lê a mensagem JSON do controle. Tudo que não for exatamente um dos formatos conhecidos é
/// recusado: tipo desconhecido, campo fora da faixa, texto com caractere de controle.
/// </summary>
public static partial class LeitorControle
{
    public const int MaxMovimento = 400;
    public const int MaxPassosRolagem = 20;
    public const int MaxTexto = 120;
    public const int MaxBytesMensagem = 1024;

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,39}$")]
    private static partial Regex IdValido();

    public static MensagemControle? Ler(string json, out string? erro)
    {
        erro = null;
        if (string.IsNullOrEmpty(json) || json.Length > MaxBytesMensagem) { erro = "tamanho"; return null; }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { erro = "json"; return null; }

        using (doc)
        {
            var raiz = doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("t", out var tipo) || tipo.ValueKind != JsonValueKind.String)
            { erro = "sem tipo"; return null; }

            switch (tipo.GetString())
            {
                case "tecla":
                    // Só o nome exato: o TryParse sozinho aceitaria "3" e "Cima,Baixo"
                    if (Texto(raiz, "k") is { Length: > 0 } k && k.All(char.IsAsciiLetter)
                        && Enum.TryParse<ComandoTv>(k, ignoreCase: false, out var comando) && Enum.IsDefined(comando))
                        return new MensagemControle.Tecla(comando);
                    erro = "tecla"; return null;

                case "abrir":
                    if (Texto(raiz, "id") is { } id && IdValido().IsMatch(id)) return new MensagemControle.AbrirItem(id);
                    erro = "id"; return null;

                case "mouse":
                    if (Inteiro(raiz, "dx") is { } dx && Inteiro(raiz, "dy") is { } dy
                        && Math.Abs(dx) <= MaxMovimento && Math.Abs(dy) <= MaxMovimento)
                        return new MensagemControle.MoverMouse(dx, dy);
                    erro = "mouse"; return null;

                case "clique":
                    return Texto(raiz, "b") switch
                    {
                        "esquerdo" => new MensagemControle.Clique(false),
                        "direito" => new MensagemControle.Clique(true),
                        _ => Falha(out erro, "clique"),
                    };

                case "rolar":
                    if (Inteiro(raiz, "n") is { } n && n != 0 && Math.Abs(n) <= MaxPassosRolagem) return new MensagemControle.Rolar(n);
                    erro = "rolar"; return null;

                case "texto":
                    if (Texto(raiz, "s") is { Length: > 0 and <= MaxTexto } s && !s.Any(char.IsControl))
                        return new MensagemControle.Texto(s);
                    erro = "texto"; return null;

                case "botao":
                    // Começa com letra (o TryParse aceitaria "4" como valor numérico); dígito só depois (L2, R2)
                    if (Texto(raiz, "k") is { Length: > 0 } b && char.IsAsciiLetter(b[0]) && b.All(char.IsAsciiLetterOrDigit)
                        && Enum.TryParse<BotaoJogo>(b, ignoreCase: false, out var botao) && Enum.IsDefined(botao)
                        && raiz.TryGetProperty("p", out var p) && p.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        return new MensagemControle.Botao(botao, p.GetBoolean());
                    erro = "botao"; return null;

                default:
                    erro = "tipo desconhecido"; return null;
            }
        }
    }

    private static MensagemControle? Falha(out string? erro, string motivo) { erro = motivo; return null; }

    private static string? Texto(JsonElement raiz, string nome) =>
        raiz.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Inteiro(JsonElement raiz, string nome) =>
        raiz.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
}
