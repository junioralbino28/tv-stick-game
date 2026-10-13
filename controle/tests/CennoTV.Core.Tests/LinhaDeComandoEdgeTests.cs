using CennoTV.Core.Lancamento;

namespace CennoTV.Core.Tests;

public class LinhaDeComandoEdgeTests
{
    [Fact]
    public void Abre_como_app_em_tela_cheia_no_perfil_proprio()
    {
        var args = LinhaDeComandoEdge.Argumentos(new Uri("https://www.netflix.com/browse"), @"C:\perfil\tv");

        Assert.Contains(@"--user-data-dir=C:\perfil\tv", args);
        Assert.Contains("--app=https://www.netflix.com/browse", args);
        Assert.Contains("--start-fullscreen", args);
        Assert.DoesNotContain(args, a => a.StartsWith("--user-agent"));
    }

    [Fact]
    public void User_agent_entra_quando_informado()
    {
        var args = LinhaDeComandoEdge.Argumentos(new Uri("https://www.youtube.com/tv"), @"C:\perfil", "Mozilla/5.0 (SMART-TV; Linux; Tizen 6.0)");

        Assert.Contains("--user-agent=Mozilla/5.0 (SMART-TV; Linux; Tizen 6.0)", args);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("ftp://exemplo.com/")]
    public void Recusa_esquema_que_nao_e_web(string endereco)
    {
        Assert.Throws<ArgumentException>(() => LinhaDeComandoEdge.Argumentos(new Uri(endereco), @"C:\perfil"));
    }

    [Theory]
    [InlineData("abc\" --remote-debugging-port=9222")]
    [InlineData("abc\n--outra")]
    public void Recusa_user_agent_que_tentaria_injetar_argumento(string ua)
    {
        Assert.Throws<ArgumentException>(() => LinhaDeComandoEdge.Argumentos(new Uri("https://a.example"), @"C:\perfil", ua));
    }
}
