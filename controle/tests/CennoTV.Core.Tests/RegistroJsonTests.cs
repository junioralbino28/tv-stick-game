using System.Text.Json;
using CennoTV.Core.Registro;

namespace CennoTV.Core.Tests;

public sealed class RegistroJsonTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "cennotv-testes-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }

    [Fact]
    public void Grava_uma_linha_json_por_evento_no_arquivo_do_dia()
    {
        var momento = new DateTimeOffset(2026, 10, 10, 20, 0, 0, TimeSpan.FromHours(-3));
        var registro = new RegistroJson(_pasta, () => momento);

        registro.Evento("lancamento", new { id = "youtube", ok = true });
        registro.Evento("início");

        var arquivo = Path.Combine(_pasta, "eventos-20261010.jsonl");
        var linhas = File.ReadAllLines(arquivo);
        Assert.Equal(2, linhas.Length);

        using var primeira = JsonDocument.Parse(linhas[0]);
        Assert.Equal("lancamento", primeira.RootElement.GetProperty("tipo").GetString());
        Assert.Equal("youtube", primeira.RootElement.GetProperty("dados").GetProperty("id").GetString());

        Assert.Contains("\"início\"", linhas[1]);
    }
}
