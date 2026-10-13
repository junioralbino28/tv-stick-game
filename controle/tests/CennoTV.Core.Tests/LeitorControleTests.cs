using CennoTV.Core;
using CennoTV.Core.Controle;

namespace CennoTV.Core.Tests;

public class LeitorControleTests
{
    [Theory]
    [InlineData("""{"t":"tecla","k":"Cima"}""", ComandoTv.Cima)]
    [InlineData("""{"t":"tecla","k":"TelaCheia"}""", ComandoTv.TelaCheia)]
    [InlineData("""{"t":"tecla","k":"Voltar"}""", ComandoTv.Voltar)]
    [InlineData("""{"t":"tecla","k":"Apagar"}""", ComandoTv.Apagar)]
    public void Tecla_valida(string json, ComandoTv esperado)
    {
        var m = LeitorControle.Ler(json, out var erro);
        Assert.Null(erro);
        Assert.Equal(new MensagemControle.Tecla(esperado), m);
    }

    [Theory]
    [InlineData("""{"t":"tecla","k":"3"}""")]
    [InlineData("""{"t":"tecla","k":"Cima,Baixo"}""")]
    [InlineData("""{"t":"tecla","k":"cima"}""")]
    [InlineData("""{"t":"tecla","k":"Desligar"}""")]
    [InlineData("""{"t":"tecla"}""")]
    public void Tecla_fora_da_lista_e_recusada(string json)
    {
        Assert.Null(LeitorControle.Ler(json, out var erro));
        Assert.Equal("tecla", erro);
    }

    [Fact]
    public void Abrir_aceita_so_id_de_catalogo()
    {
        Assert.Equal(new MensagemControle.AbrirItem("youtube"), LeitorControle.Ler("""{"t":"abrir","id":"youtube"}""", out _));
        Assert.Null(LeitorControle.Ler("""{"t":"abrir","id":"C:\\Windows\\cmd.exe"}""", out _));
        Assert.Null(LeitorControle.Ler("""{"t":"abrir","id":"https://exemplo.com"}""", out _));
        Assert.Null(LeitorControle.Ler("""{"t":"abrir","id":"../x"}""", out _));
    }

    [Fact]
    public void Mouse_respeita_a_faixa()
    {
        Assert.Equal(new MensagemControle.MoverMouse(10, -5), LeitorControle.Ler("""{"t":"mouse","dx":10,"dy":-5}""", out _));
        Assert.Null(LeitorControle.Ler("""{"t":"mouse","dx":401,"dy":0}""", out _));
        Assert.Null(LeitorControle.Ler("""{"t":"mouse","dx":1.5,"dy":0}""", out _));
        Assert.Null(LeitorControle.Ler("""{"t":"mouse","dx":"1","dy":0}""", out _));
    }

    [Fact]
    public void Clique_e_rolagem()
    {
        Assert.Equal(new MensagemControle.Clique(false), LeitorControle.Ler("""{"t":"clique","b":"esquerdo"}""", out _));
        Assert.Equal(new MensagemControle.Clique(true), LeitorControle.Ler("""{"t":"clique","b":"direito"}""", out _));
        Assert.Null(LeitorControle.Ler("""{"t":"clique","b":"meio"}""", out _));
        Assert.Equal(new MensagemControle.Rolar(-3), LeitorControle.Ler("""{"t":"rolar","n":-3}""", out _));
        Assert.Null(LeitorControle.Ler("""{"t":"rolar","n":0}""", out _));
        Assert.Null(LeitorControle.Ler("""{"t":"rolar","n":21}""", out _));
    }

    [Fact]
    public void Texto_recusa_caractere_de_controle_e_excesso()
    {
        Assert.Equal(new MensagemControle.Texto("filme de ação"), LeitorControle.Ler("""{"t":"texto","s":"filme de ação"}""", out _));
        Assert.Null(LeitorControle.Ler("""{"t":"texto","s":"a\nb"}""", out _));
        Assert.Null(LeitorControle.Ler("""{"t":"texto","s":""}""", out _));
        Assert.Null(LeitorControle.Ler($$"""{"t":"texto","s":"{{new string('a', LeitorControle.MaxTexto + 1)}}"}""", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nao e json")]
    [InlineData("[1,2]")]
    [InlineData("""{"t":"shell","cmd":"del"}""")]
    public void Lixo_e_recusado(string json) => Assert.Null(LeitorControle.Ler(json, out _));

    [Theory]
    [InlineData("""{"t":"botao","k":"A","p":true}""", BotaoJogo.A, true)]
    [InlineData("""{"t":"botao","k":"Esquerda","p":false}""", BotaoJogo.Esquerda, false)]
    [InlineData("""{"t":"botao","k":"Start","p":true}""", BotaoJogo.Start, true)]
    [InlineData("""{"t":"botao","k":"L2","p":true}""", BotaoJogo.L2, true)]
    [InlineData("""{"t":"botao","k":"R2","p":false}""", BotaoJogo.R2, false)]
    public void Botao_valido(string json, BotaoJogo esperado, bool apertado)
    {
        Assert.Equal(new MensagemControle.Botao(esperado, apertado), LeitorControle.Ler(json, out var erro));
        Assert.Null(erro);
    }

    [Theory]
    [InlineData("""{"t":"botao","k":"A"}""")]
    [InlineData("""{"t":"botao","k":"A","p":1}""")]
    [InlineData("""{"t":"botao","k":"A","p":"true"}""")]
    [InlineData("""{"t":"botao","k":"a","p":true}""")]
    [InlineData("""{"t":"botao","k":"4","p":true}""")]
    [InlineData("""{"t":"botao","k":"A,B","p":true}""")]
    [InlineData("""{"t":"botao","k":"Fechar","p":true}""")]
    [InlineData("""{"t":"botao","k":"12","p":true}""")]
    [InlineData("""{"t":"botao","k":"L3","p":true}""")]
    public void Botao_fora_do_formato_e_recusado(string json)
    {
        Assert.Null(LeitorControle.Ler(json, out var erro));
        Assert.Equal("botao", erro);
    }

    [Fact]
    public void Mensagem_grande_demais_e_recusada()
    {
        var json = $$"""{"t":"texto","s":"{{new string('a', 2000)}}"}""";
        Assert.Null(LeitorControle.Ler(json, out var erro));
        Assert.Equal("tamanho", erro);
    }
}
