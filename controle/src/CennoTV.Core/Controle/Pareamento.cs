using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CennoTV.Core.Controle;

/// <summary>Um celular ou tablet pareado. Só o hash do token fica guardado.</summary>
public sealed record ControlePareado(string Nome, string HashToken, DateTimeOffset Criado);

/// <summary>
/// Pareamento do controle: o celular pede, a TV mostra um código de 6 dígitos, o celular manda o
/// código e recebe um token. O código vale <see cref="Validade"/> e aceita no máximo
/// <see cref="MaxTentativas"/> erros; um pedido novo só depois de <see cref="IntervaloPedidos"/>.
/// Tokens são aleatórios (32 bytes) e guardados só como SHA-256.
/// </summary>
public sealed class Pareamento
{
    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan IntervaloPedidos = TimeSpan.FromSeconds(10);
    public const int MaxTentativas = 5;
    public const int MaxControles = 10;

    private readonly string _arquivo;
    private readonly Func<DateTimeOffset> _relogio;
    private readonly Lock _trava = new();
    private List<ControlePareado> _controles;

    private string? _codigo;
    private DateTimeOffset _expira;
    private DateTimeOffset _ultimoPedido = DateTimeOffset.MinValue;
    private int _erros;

    public Pareamento(string arquivo, Func<DateTimeOffset>? relogio = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arquivo);
        _arquivo = arquivo;
        _relogio = relogio ?? (() => DateTimeOffset.Now);
        _controles = Carregar(arquivo);
    }

    public IReadOnlyList<ControlePareado> Controles { get { lock (_trava) return [.. _controles]; } }

    /// <summary>Gera um código novo. Devolve null se ainda está no intervalo entre pedidos.</summary>
    public string? PedirCodigo()
    {
        lock (_trava)
        {
            var agora = _relogio();
            if (agora - _ultimoPedido < IntervaloPedidos) return null;
            _ultimoPedido = agora;
            _codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            _expira = agora + Validade;
            _erros = 0;
            return _codigo;
        }
    }

    /// <summary>Confere o código e, se certo, devolve o token do novo controle (só desta vez).</summary>
    public string? Confirmar(string codigo, string nome)
    {
        lock (_trava)
        {
            if (_codigo is null || _relogio() > _expira) { _codigo = null; return null; }
            var certo = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(codigo ?? ""), Encoding.ASCII.GetBytes(_codigo));
            if (!certo)
            {
                if (++_erros >= MaxTentativas) _codigo = null;
                return null;
            }
            _codigo = null;

            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var nomeLimpo = new string((nome ?? "").Where(c => !char.IsControl(c)).Take(40).ToArray()).Trim();
            _controles.Add(new(nomeLimpo.Length > 0 ? nomeLimpo : "Controle", Hash(token), _relogio()));
            if (_controles.Count > MaxControles) _controles = [.. _controles.OrderByDescending(c => c.Criado).Take(MaxControles)];
            Salvar();
            return token;
        }
    }

    public bool Valido(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 100) return false;
        var hash = Encoding.ASCII.GetBytes(Hash(token));
        lock (_trava)
            return _controles.Any(c => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(c.HashToken), hash));
    }

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private void Salvar()
    {
        var pasta = Path.GetDirectoryName(_arquivo);
        if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);
        var tmp = _arquivo + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_controles));
        File.Move(tmp, _arquivo, overwrite: true);
    }

    private static List<ControlePareado> Carregar(string arquivo)
    {
        try { return File.Exists(arquivo) ? JsonSerializer.Deserialize<List<ControlePareado>>(File.ReadAllText(arquivo)) ?? [] : []; }
        catch (JsonException) { return []; }
    }
}
