using CennoTV.Core.Controle;

namespace CennoTV.Core.Tests;

public class MapaBotoesTests
{
    [Fact]
    public void Todo_botao_tem_tecla_em_todo_perfil()
    {
        foreach (var programa in new string?[] { null, "ES-DE", "retroarch", "Play" })
            foreach (var botao in Enum.GetValues<BotaoJogo>())
                Assert.NotEqual(0, MapaBotoes.Tecla(botao, programa));
    }

    [Theory]
    [InlineData(BotaoJogo.A, 0x0D)]      // Enter: o mesmo OK do modo Controle
    [InlineData(BotaoJogo.B, 0x1B)]      // Esc: o mesmo Voltar
    [InlineData(BotaoJogo.Start, 0x08)]
    [InlineData(BotaoJogo.L2, 0x24)]
    public void Padrao_bate_com_o_teclado_do_ESDE_e_do_RetroArch(BotaoJogo botao, int vk)
    {
        Assert.Equal(vk, MapaBotoes.Tecla(botao, null));
        Assert.Equal(vk, MapaBotoes.Tecla(botao, "retroarch"));
    }

    [Theory]
    [InlineData(BotaoJogo.A, 'X')]       // Círculo, à direita como o A do celular
    [InlineData(BotaoJogo.B, 'Z')]       // Xis, embaixo
    [InlineData(BotaoJogo.X, 'S')]       // Triângulo, em cima
    [InlineData(BotaoJogo.Y, 'A')]       // Quadrado, à esquerda
    [InlineData(BotaoJogo.L, '1')]
    [InlineData(BotaoJogo.R2, '9')]
    [InlineData(BotaoJogo.Start, 0x0D)]
    [InlineData(BotaoJogo.Select, 0x08)]
    [InlineData(BotaoJogo.Cima, 0x26)]
    public void Play_usa_o_teclado_padrao_dele(BotaoJogo botao, int vk)
    {
        Assert.Equal(vk, MapaBotoes.Tecla(botao, "Play"));
        Assert.Equal(vk, MapaBotoes.Tecla(botao, "play"));
    }

    [Fact]
    public void No_Play_nenhum_botao_cai_em_tecla_que_ele_nao_conhece()
    {
        // Teclas do perfil padrão que o Play! ignora: se aparecerem aqui, o botão "morre" no PS2
        ushort[] ignoradas = [0x1B, 0x2D, 0x2E, 0x70, 0x21, 0x22, 0x23, 0x24];
        foreach (var botao in Enum.GetValues<BotaoJogo>())
            Assert.DoesNotContain(MapaBotoes.Tecla(botao, "Play"), ignoradas);
    }
}
