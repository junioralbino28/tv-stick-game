using System.Text.RegularExpressions;

namespace CennoTV.Core.Lancamento;

/// <summary>
/// Endereço que abre um app Android pelo motor atual, o WSA (Windows Subsystem for Android):
/// <c>wsa://nome.do.pacote</c>, aberto pelo explorer.exe na sessão do usuário. O WSA liga sozinho
/// se estiver desligado.
/// </summary>
public static partial class EnderecoAndroid
{
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$")]
    private static partial Regex PacoteValido();

    public static string Wsa(string pacote)
    {
        if (string.IsNullOrEmpty(pacote) || !PacoteValido().IsMatch(pacote))
            throw new ArgumentException("Nome de pacote Android inválido.", nameof(pacote));
        return "wsa://" + pacote;
    }
}
