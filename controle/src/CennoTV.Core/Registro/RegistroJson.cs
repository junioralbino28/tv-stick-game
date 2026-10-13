using System.Text.Encodings.Web;
using System.Text.Json;

namespace CennoTV.Core.Registro;

/// <summary>
/// Log estruturado: uma linha JSON por evento, um arquivo por dia (eventos-AAAAMMDD.jsonl).
/// Nunca registrar senha, token, URL de stream com credencial ou dado pessoal.
/// </summary>
public sealed class RegistroJson
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _pasta;
    private readonly Func<DateTimeOffset> _relogio;
    private readonly Lock _trava = new();

    public RegistroJson(string pasta, Func<DateTimeOffset>? relogio = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pasta);
        _pasta = pasta;
        _relogio = relogio ?? (() => DateTimeOffset.Now);
        Directory.CreateDirectory(_pasta);
    }

    public string ArquivoDoDia(DateTimeOffset momento) => Path.Combine(_pasta, $"eventos-{momento:yyyyMMdd}.jsonl");

    public void Evento(string tipo, object? dados = null)
    {
        var momento = _relogio();
        var linha = JsonSerializer.Serialize(new { momento, tipo, dados }, Opcoes);
        lock (_trava)
        {
            try { File.AppendAllText(ArquivoDoDia(momento), linha + Environment.NewLine); }
            catch (IOException) { /* log nunca derruba a TV */ }
            catch (UnauthorizedAccessException) { }
        }
    }
}
