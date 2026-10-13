namespace CennoTV.Core.Catalogo;

public enum TipoItem
{
    /// <summary>Endereço web aberto no Edge em modo app.</summary>
    Web,
    /// <summary>Programa do Windows.</summary>
    Programa,
    /// <summary>App Android, aberto pelo motor Android pelo nome do pacote.</summary>
    Android,
}

/// <summary>Um bloco da tela inicial.</summary>
/// <param name="Id">Identificador estável (letras minúsculas, números e hífen). É o que o controle remoto manda.</param>
/// <param name="Destino">URL (Web), executável (Programa) ou nome do pacote (Android).</param>
/// <param name="PerfilEdge">Perfil próprio do Edge para este bloco (Web). Perfis separados guardam logins e opções separados.</param>
/// <param name="UserAgent">User-agent do perfil (Web), por exemplo para a interface de TV do YouTube.</param>
/// <param name="Motivo">Texto mostrado quando o bloco está desabilitado.</param>
public sealed record ItemCatalogo(
    string Id,
    string Titulo,
    TipoItem Tipo,
    string Destino,
    string? Argumentos = null,
    string? PerfilEdge = null,
    string? UserAgent = null,
    string? Cor = null,
    bool Habilitado = true,
    string? Motivo = null);

public sealed record ResultadoCatalogo(IReadOnlyList<ItemCatalogo> Itens, IReadOnlyList<string> Problemas)
{
    public static readonly ResultadoCatalogo Vazio = new([], []);
}
