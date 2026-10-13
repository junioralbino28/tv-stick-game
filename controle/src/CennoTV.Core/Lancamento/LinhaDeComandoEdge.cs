namespace CennoTV.Core.Lancamento;

/// <summary>
/// Monta os argumentos do Edge para abrir um endereço como app, em tela cheia, num perfil próprio
/// da CENNO TV. Perfil próprio garante uma instância separada do Edge do uso normal do Windows:
/// as opções (como o user-agent) valem só nela, e o processo aberto é o que a CENNO TV vigia.
/// </summary>
public static class LinhaDeComandoEdge
{
    public static IReadOnlyList<string> Argumentos(Uri endereco, string pastaPerfil, string? userAgent = null)
    {
        ArgumentNullException.ThrowIfNull(endereco);
        ArgumentException.ThrowIfNullOrWhiteSpace(pastaPerfil);

        if (!endereco.IsAbsoluteUri || (endereco.Scheme != Uri.UriSchemeHttps && endereco.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("Só endereços http ou https.", nameof(endereco));
        if (userAgent is not null && (userAgent.Contains('"') || userAgent.Contains('\n') || userAgent.Contains('\r')))
            throw new ArgumentException("User-agent com caractere proibido.", nameof(userAgent));

        var argumentos = new List<string>
        {
            $"--user-data-dir={pastaPerfil}",
            $"--app={endereco.AbsoluteUri}",
            "--start-fullscreen",
            "--no-first-run",
            "--no-default-browser-check",
        };
        if (!string.IsNullOrWhiteSpace(userAgent)) argumentos.Add($"--user-agent={userAgent}");
        return argumentos;
    }

    /// <summary>Caminhos usuais do msedge.exe, na ordem de preferência.</summary>
    public static IEnumerable<string> CaminhosProvaveis()
    {
        var x86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var x64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var raiz in new[] { x86, x64, local })
            if (!string.IsNullOrEmpty(raiz))
                yield return Path.Combine(raiz, "Microsoft", "Edge", "Application", "msedge.exe");
    }
}
