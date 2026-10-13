using CennoTV.Core.Catalogo;

namespace CennoTV.Core.Tests;

public class CatalogoLoaderTests
{
    private const string Padrao = """
    {
      "versao": 1,
      "itens": [
        { "id": "youtube", "titulo": "YouTube", "tipo": "web", "destino": "https://www.youtube.com/tv", "perfilEdge": "youtube-tv", "cor": "#C4302B" },
        { "id": "arquivos", "titulo": "Arquivos", "tipo": "programa", "destino": "explorer.exe", "argumentos": "D:\\" },
        { "id": "apps-android", "titulo": "Apps Android", "tipo": "android", "destino": "com.exemplo.loja", "habilitado": false, "motivo": "Motor em teste" }
      ]
    }
    """;

    [Fact]
    public void Le_itens_validos_com_tipos_e_campos()
    {
        var r = CatalogoLoader.Ler(Padrao, "padrao");

        Assert.Empty(r.Problemas);
        Assert.Equal(["youtube", "arquivos", "apps-android"], r.Itens.Select(i => i.Id));
        Assert.Equal(TipoItem.Web, r.Itens[0].Tipo);
        Assert.Equal("youtube-tv", r.Itens[0].PerfilEdge);
        Assert.Equal("D:\\", r.Itens[1].Argumentos);
        Assert.False(r.Itens[2].Habilitado);
        Assert.Equal("Motor em teste", r.Itens[2].Motivo);
    }

    [Theory]
    [InlineData("""{ "id": "x", "titulo": "X", "tipo": "web", "destino": "file:///C:/Windows/System32/cmd.exe" }""", "http ou https")]
    [InlineData("""{ "id": "x", "titulo": "X", "tipo": "web", "destino": "javascript:alert(1)" }""", "http ou https")]
    [InlineData("""{ "id": "x", "titulo": "X", "tipo": "android", "destino": "rm -rf /" }""", "nome de pacote")]
    [InlineData("""{ "id": "X Maiusculo", "titulo": "X", "tipo": "web", "destino": "https://a.b" }""", "id")]
    [InlineData("""{ "id": "x", "titulo": "X", "tipo": "shell", "destino": "cmd" }""", "desconhecido")]
    [InlineData("""{ "id": "x", "titulo": "", "tipo": "web", "destino": "https://a.b" }""", "título")]
    [InlineData("""{ "id": "x", "titulo": "X", "tipo": "web", "destino": "https://a.b", "cor": "laranja" }""", "cor")]
    [InlineData("""{ "id": "x", "titulo": "X", "tipo": "web", "destino": "https://a.b", "perfilEdge": "..\\..\\Windows" }""", "perfilEdge")]
    public void Item_invalido_e_descartado_com_problema_registrado(string item, string trechoDoProblema)
    {
        var json = $$"""{ "itens": [ {{item}}, { "id": "ok", "titulo": "Ok", "tipo": "web", "destino": "https://ok.example" } ] }""";

        var r = CatalogoLoader.Ler(json, "local");

        Assert.Equal(["ok"], r.Itens.Select(i => i.Id));
        var problema = Assert.Single(r.Problemas);
        Assert.Contains(trechoDoProblema, problema);
        Assert.StartsWith("local, item 1", problema);
    }

    [Fact]
    public void Id_repetido_fica_so_o_primeiro()
    {
        var json = """{ "itens": [ { "id": "a", "titulo": "A1", "tipo": "web", "destino": "https://a.example" }, { "id": "a", "titulo": "A2", "tipo": "web", "destino": "https://b.example" } ] }""";

        var r = CatalogoLoader.Ler(json, "local");

        Assert.Equal("A1", Assert.Single(r.Itens).Titulo);
        Assert.Contains("repetido", Assert.Single(r.Problemas));
    }

    [Fact]
    public void Json_quebrado_nao_lanca_e_vira_problema()
    {
        var r = CatalogoLoader.Ler("{ itens: [", "local");

        Assert.Empty(r.Itens);
        Assert.Contains("JSON inválido", Assert.Single(r.Problemas));
    }

    [Fact]
    public void Aceita_comentarios_e_virgula_sobrando()
    {
        var json = """
        {
          // catálogo da casa
          "itens": [ { "id": "a", "titulo": "A", "tipo": "web", "destino": "https://a.example" }, ],
        }
        """;

        Assert.Single(CatalogoLoader.Ler(json, "local").Itens);
    }

    [Fact]
    public void Local_substitui_pelo_id_e_acrescenta_novos_no_fim()
    {
        var padrao = CatalogoLoader.Ler(Padrao, "padrao");
        var local = CatalogoLoader.Ler("""
        { "itens": [
          { "id": "apps-android", "titulo": "Apps Android", "tipo": "android", "destino": "com.exemplo.loja", "habilitado": true },
          { "id": "tv-ao-vivo", "titulo": "TV ao vivo", "tipo": "android", "destino": "com.exemplo.tv" }
        ] }
        """, "local");

        var r = CatalogoLoader.Combinar(padrao, local);

        Assert.Equal(["youtube", "arquivos", "apps-android", "tv-ao-vivo"], r.Itens.Select(i => i.Id));
        Assert.True(r.Itens[2].Habilitado);
    }

    [Fact]
    public void Sem_catalogo_local_fica_o_padrao()
    {
        var padrao = CatalogoLoader.Ler(Padrao, "padrao");
        Assert.Same(padrao, CatalogoLoader.Combinar(padrao, null));
    }
}
