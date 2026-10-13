namespace CennoTV.Core.Controle;

/// <summary>
/// Qual tecla do Windows cada botão do modo Jogo aperta. Depende do programa que está na frente,
/// porque cada emulador tem o seu teclado:
/// <list type="bullet">
/// <item>Padrão (ES-DE e RetroArch, configurados por scripts/dell/jogos-organizar.ps1): A=Enter,
/// B=Esc, X=Delete, Y=Insert, Start=Backspace, Select=F1, L/R=PageUp/PageDown, L2/R2=Home/End.</item>
/// <item>Play! (PS2, teclado padrão dele): os botões do celular viram os do PS2 na mesma
/// posição: A=Círculo (X), B=Xis (Z), X=Triângulo (S), Y=Quadrado (A), L1=1, R1=8, L2=2, R2=9,
/// Start=Enter, Select=Backspace.</item>
/// </list>
/// Os valores são códigos de tecla virtual do Windows (VK_*).
/// </summary>
public static class MapaBotoes
{
    private const ushort Backspace = 0x08, Enter = 0x0D, Esc = 0x1B, PageUp = 0x21, PageDown = 0x22, End = 0x23,
        Home = 0x24, Esquerda = 0x25, Cima = 0x26, Direita = 0x27, Baixo = 0x28, Insert = 0x2D, Delete = 0x2E, F1 = 0x70;

    /// <summary>Nome do processo do Play! (Play.exe).</summary>
    public const string ProcessoPlay = "Play";

    public static ushort Tecla(BotaoJogo botao, string? programaEmFoco)
    {
        if (string.Equals(programaEmFoco, ProcessoPlay, StringComparison.OrdinalIgnoreCase))
            return botao switch
            {
                BotaoJogo.A => 'X',
                BotaoJogo.B => 'Z',
                BotaoJogo.X => 'S',
                BotaoJogo.Y => 'A',
                BotaoJogo.L => '1',
                BotaoJogo.R => '8',
                BotaoJogo.L2 => '2',
                BotaoJogo.R2 => '9',
                BotaoJogo.Start => Enter,
                BotaoJogo.Select => Backspace,
                _ => Padrao(botao),
            };
        return Padrao(botao);
    }

    private static ushort Padrao(BotaoJogo botao) => botao switch
    {
        BotaoJogo.Cima => Cima,
        BotaoJogo.Baixo => Baixo,
        BotaoJogo.Esquerda => Esquerda,
        BotaoJogo.Direita => Direita,
        BotaoJogo.A => Enter,
        BotaoJogo.B => Esc,
        BotaoJogo.X => Delete,
        BotaoJogo.Y => Insert,
        BotaoJogo.L => PageUp,
        BotaoJogo.R => PageDown,
        BotaoJogo.Start => Backspace,
        BotaoJogo.Select => F1,
        BotaoJogo.L2 => Home,
        BotaoJogo.R2 => End,
        _ => throw new ArgumentOutOfRangeException(nameof(botao)),
    };
}
