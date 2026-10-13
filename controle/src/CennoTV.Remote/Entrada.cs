using System.Runtime.InteropServices;

namespace CennoTV.Remote;

/// <summary>
/// Teclado e mouse sintéticos (SendInput) na sessão da TV. Cada tecla é apertada e solta na mesma
/// chamada: nenhuma tecla fica presa se a conexão do controle cair.
/// </summary>
internal static class Entrada
{
    public const ushort VkBackspace = 0x08, VkTab = 0x09, VkEnter = 0x0D, VkShift = 0x10, VkAlt = 0x12, VkCapsLock = 0x14, VkEsc = 0x1B,
        VkPageUp = 0x21, VkPageDown = 0x22, VkEnd = 0x23, VkHome = 0x24, VkEsquerda = 0x25, VkCima = 0x26, VkDireita = 0x27, VkBaixo = 0x28,
        VkInsert = 0x2D, VkDelete = 0x2E, VkD = 0x44, VkWin = 0x5B, VkF1 = 0x70, VkF4 = 0x73, VkF11 = 0x7A,
        VkMudo = 0xAD, VkVolumeMenos = 0xAE, VkVolumeMais = 0xAF, VkPlayPausa = 0xB3;

    private const uint TipoMouse = 0, TipoTeclado = 1;
    private const uint TeclaEstendida = 0x0001, TeclaSolta = 0x0002, TeclaUnicode = 0x0004;
    private const uint MouseMover = 0x0001, MouseEsqDesce = 0x0002, MouseEsqSobe = 0x0004,
        MouseDirDesce = 0x0008, MouseDirSobe = 0x0010, MouseRoda = 0x0800;

    private static readonly HashSet<ushort> Estendidas = [VkEsquerda, VkCima, VkDireita, VkBaixo, VkPageUp, VkPageDown, VkHome, VkEnd,
        VkInsert, VkDelete, VkWin, VkMudo, VkVolumeMenos, VkVolumeMais, VkPlayPausa];

    /// <summary>Aperta a combinação (ex.: Alt+Tab) e solta na ordem inversa.</summary>
    public static void Combinacao(params ushort[] teclas)
    {
        var eventos = new List<INPUT>();
        foreach (var vk in teclas) eventos.Add(Tecla(vk, solta: false));
        for (int i = teclas.Length - 1; i >= 0; i--) eventos.Add(Tecla(teclas[i], solta: true));
        Enviar(eventos);
    }

    /// <summary>
    /// Aperta OU solta uma tecla (modo Jogo). Quem chama é responsável por soltar: o receptor
    /// solta tudo que ficou apertado quando a conexão do controle cai.
    /// </summary>
    public static void ApertarOuSoltar(ushort vk, bool apertada) => Enviar([Tecla(vk, solta: !apertada)]);

    /// <summary>
    /// Digita o texto no app em foco. O caractere que existe no layout do teclado (letras, números,
    /// pontuação, ç) vai como tecla de verdade, com Shift quando precisa: é assim que as setas já
    /// chegam ao app Android do WSA. O resto (ã, é, emoji: tecla morta, AltGr ou fora do layout)
    /// vai como caractere Unicode.
    /// </summary>
    public static void Texto(string texto)
    {
        var layout = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero));
        var caps = (GetKeyState(VkCapsLock) & 1) != 0;
        var eventos = new List<INPUT>();
        foreach (var c in texto)
        {
            var r = VkKeyScanEx(c, layout);
            var vk = (ushort)(r & 0xFF);
            var estado = (r >> 8) & 0xFF;
            if (r != -1 && (estado & ~1) == 0)
            {
                // Caps Lock ligado inverte o Shift das letras
                var shift = ((estado & 1) != 0) ^ (caps && vk is >= 0x41 and <= 0x5A);
                if (shift) eventos.Add(Tecla(VkShift, solta: false));
                eventos.Add(Tecla(vk, solta: false));
                eventos.Add(Tecla(vk, solta: true));
                if (shift) eventos.Add(Tecla(VkShift, solta: true));
            }
            else
            {
                eventos.Add(Unicode(c, solta: false));
                eventos.Add(Unicode(c, solta: true));
            }
        }
        Enviar(eventos);
    }

    public static void MoverMouse(int dx, int dy) => Enviar([Mouse(MouseMover, dx, dy, 0)]);

    public static void Clique(bool direito) => Enviar(direito
        ? [Mouse(MouseDirDesce, 0, 0, 0), Mouse(MouseDirSobe, 0, 0, 0)]
        : [Mouse(MouseEsqDesce, 0, 0, 0), Mouse(MouseEsqSobe, 0, 0, 0)]);

    /// <summary>Rola a roda do mouse; passo positivo rola para cima.</summary>
    public static void Rolar(int passos) => Enviar([Mouse(MouseRoda, 0, 0, unchecked((uint)(passos * 120)))]);

    private static INPUT Tecla(ushort vk, bool solta) => new()
    {
        type = TipoTeclado,
        u = new() { ki = new() {
            wVk = vk,
            wScan = (ushort)MapVirtualKey(vk, 0),
            dwFlags = (solta ? TeclaSolta : 0) | (Estendidas.Contains(vk) ? TeclaEstendida : 0),
        } },
    };

    private static INPUT Unicode(char c, bool solta) => new()
    {
        type = TipoTeclado,
        u = new() { ki = new() { wVk = 0, wScan = c, dwFlags = TeclaUnicode | (solta ? TeclaSolta : 0) } },
    };

    private static INPUT Mouse(uint flags, int dx, int dy, uint dados) => new()
    {
        type = TipoMouse,
        u = new() { mi = new() { dx = dx, dy = dy, mouseData = dados, dwFlags = flags } },
    };

    private static void Enviar(IReadOnlyList<INPUT> eventos)
    {
        if (eventos.Count == 0) return;
        var vetor = eventos.ToArray();
        SendInput((uint)vetor.Length, vetor, Marshal.SizeOf<INPUT>());
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint quantidade, INPUT[] eventos, int tamanho);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint codigo, uint tipo);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern short VkKeyScanEx(char caractere, IntPtr layout);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint thread);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr janela, IntPtr pid);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int vk);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public UNIAO u; }

    [StructLayout(LayoutKind.Explicit)]
    private struct UNIAO
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
}
