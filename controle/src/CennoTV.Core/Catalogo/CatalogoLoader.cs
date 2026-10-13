using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CennoTV.Core.Catalogo;

/// <summary>
/// Lê o catálogo de blocos em JSON. Item inválido é descartado e vira um problema registrado,
/// para a TV sempre ligar, mesmo com um arquivo de configuração com erro.
/// </summary>
public static partial class CatalogoLoader
{
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,39}$")]
    private static partial Regex IdValido();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$")]
    private static partial Regex PacoteAndroidValido();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex CorValida();

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ResultadoCatalogo Ler(string json, string origem)
    {
        ArquivoDto? arquivo;
        try
        {
            arquivo = JsonSerializer.Deserialize<ArquivoDto>(json, Opcoes);
        }
        catch (JsonException e)
        {
            return new([], [$"{origem}: JSON inválido ({e.Message})"]);
        }

        if (arquivo?.Itens is null) return new([], [$"{origem}: sem lista 'itens'"]);

        var itens = new List<ItemCatalogo>();
        var problemas = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < arquivo.Itens.Count; i++)
        {
            var dto = arquivo.Itens[i];
            var onde = $"{origem}, item {i + 1}";
            var erro = Validar(dto, out var item);
            if (erro is not null) { problemas.Add($"{onde}: {erro}"); continue; }
            if (!ids.Add(item!.Id)) { problemas.Add($"{onde}: id '{item.Id}' repetido"); continue; }
            itens.Add(item);
        }

        return new(itens, problemas);
    }

    /// <summary>
    /// Junta o catálogo padrão com o local: item local com o mesmo id substitui o padrão
    /// (inclusive para desabilitar); id novo entra no fim, na ordem do arquivo local.
    /// </summary>
    public static ResultadoCatalogo Combinar(ResultadoCatalogo padrao, ResultadoCatalogo? local)
    {
        if (local is null) return padrao;

        var porId = local.Itens.ToDictionary(i => i.Id, StringComparer.Ordinal);
        var resultado = padrao.Itens.Select(i => porId.GetValueOrDefault(i.Id, i)).ToList();
        var idsPadrao = padrao.Itens.Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        resultado.AddRange(local.Itens.Where(i => !idsPadrao.Contains(i.Id)));

        return new(resultado, [.. padrao.Problemas, .. local.Problemas]);
    }

    private static string? Validar(ItemDto dto, out ItemCatalogo? item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(dto.Id) || !IdValido().IsMatch(dto.Id)) return "id ausente ou inválido";
        if (string.IsNullOrWhiteSpace(dto.Titulo)) return "título ausente";
        if (string.IsNullOrWhiteSpace(dto.Destino)) return "destino ausente";
        if (dto.Cor is not null && !CorValida().IsMatch(dto.Cor)) return $"cor '{dto.Cor}' inválida (use #RRGGBB)";

        TipoItem tipo;
        switch (dto.Tipo?.Trim().ToLowerInvariant())
        {
            case "web":
                tipo = TipoItem.Web;
                if (!Uri.TryCreate(dto.Destino, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                    return "destino web precisa ser endereço http ou https";
                if (dto.PerfilEdge is not null && !IdValido().IsMatch(dto.PerfilEdge))
                    return "perfilEdge inválido (letras minúsculas, números e hífen)";
                break;
            case "programa":
                tipo = TipoItem.Programa;
                break;
            case "android":
                tipo = TipoItem.Android;
                if (!PacoteAndroidValido().IsMatch(dto.Destino)) return "destino android precisa ser um nome de pacote";
                break;
            default:
                return $"tipo '{dto.Tipo}' desconhecido (web, programa ou android)";
        }

        item = new ItemCatalogo(
            dto.Id, dto.Titulo.Trim(), tipo, dto.Destino.Trim(), dto.Argumentos,
            dto.PerfilEdge, dto.UserAgent, dto.Cor, dto.Habilitado ?? true, dto.Motivo);
        return null;
    }

    private sealed class ArquivoDto
    {
        public int Versao { get; set; }
        public List<ItemDto>? Itens { get; set; }
    }

    private sealed class ItemDto
    {
        public string? Id { get; set; }
        public string? Titulo { get; set; }
        public string? Tipo { get; set; }
        public string? Destino { get; set; }
        public string? Argumentos { get; set; }
        public string? PerfilEdge { get; set; }
        public string? UserAgent { get; set; }
        public string? Cor { get; set; }
        public bool? Habilitado { get; set; }
        public string? Motivo { get; set; }
    }
}
