using CennoTV.Core.Controle;

namespace CennoTV.Core.Tests;

public class PareamentoTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "cenno-pareamento-" + Guid.NewGuid().ToString("N"));
    private DateTimeOffset _agora = new(2026, 10, 11, 17, 0, 0, TimeSpan.FromHours(-3));
    private string Arquivo => Path.Combine(_pasta, "controles.json");
    private Pareamento Novo() => new(Arquivo, () => _agora);

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }

    [Fact]
    public void Codigo_certo_gera_token_valido_e_persistido_so_como_hash()
    {
        var p = Novo();
        var codigo = p.PedirCodigo();
        Assert.Matches("^[0-9]{6}$", codigo!);

        var token = p.Confirmar(codigo!, "Celular da sala");
        Assert.NotNull(token);
        Assert.True(p.Valido(token));
        Assert.False(p.Valido(token + "x"));

        var arquivo = File.ReadAllText(Arquivo);
        Assert.DoesNotContain(token!, arquivo);
        Assert.Contains(Pareamento.Hash(token!), arquivo);

        // Outra instância (reinício do receptor) continua aceitando o token
        Assert.True(Novo().Valido(token));
    }

    [Fact]
    public void Codigo_vale_uma_vez_so()
    {
        var p = Novo();
        var codigo = p.PedirCodigo()!;
        Assert.NotNull(p.Confirmar(codigo, "a"));
        Assert.Null(p.Confirmar(codigo, "b"));
    }

    [Fact]
    public void Codigo_expira()
    {
        var p = Novo();
        var codigo = p.PedirCodigo()!;
        _agora += Pareamento.Validade + TimeSpan.FromSeconds(1);
        Assert.Null(p.Confirmar(codigo, "a"));
    }

    [Fact]
    public void Cinco_erros_queimam_o_codigo()
    {
        var p = Novo();
        var codigo = p.PedirCodigo()!;
        var errado = codigo == "000000" ? "111111" : "000000";
        for (int i = 0; i < Pareamento.MaxTentativas; i++) Assert.Null(p.Confirmar(errado, "x"));
        Assert.Null(p.Confirmar(codigo, "x"));
    }

    [Fact]
    public void Pedidos_seguidos_sao_limitados()
    {
        var p = Novo();
        Assert.NotNull(p.PedirCodigo());
        Assert.Null(p.PedirCodigo());
        _agora += Pareamento.IntervaloPedidos;
        Assert.NotNull(p.PedirCodigo());
    }

    [Fact]
    public void Sem_pedido_nao_ha_pareamento_e_token_vazio_e_invalido()
    {
        var p = Novo();
        Assert.Null(p.Confirmar("123456", "x"));
        Assert.False(p.Valido(null));
        Assert.False(p.Valido(""));
    }
}
