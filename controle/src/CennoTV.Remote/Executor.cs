using System.Diagnostics;
using CennoTV.Core;
using CennoTV.Core.Catalogo;
using CennoTV.Core.Controle;
using CennoTV.Core.Lancamento;
using CennoTV.Core.Registro;

namespace CennoTV.Remote;

/// <summary>
/// Executa uma mensagem do controle já validada. Abrir só abre item do catálogo (pelo id);
/// o destino vem do catálogo do Dell, nunca do celular.
/// </summary>
internal sealed class Executor(IReadOnlyList<ItemCatalogo> catalogo, RegistroJson registro)
{
    public string? Executar(MensagemControle mensagem)
    {
        switch (mensagem)
        {
            case MensagemControle.Tecla t: Tecla(t.Comando); return null;
            case MensagemControle.MoverMouse m: Entrada.MoverMouse(m.Dx, m.Dy); return null;
            case MensagemControle.Clique c: Entrada.Clique(c.Direito); return null;
            case MensagemControle.Rolar r: Entrada.Rolar(r.Passos); return null;
            case MensagemControle.Texto x: Entrada.Texto(x.Conteudo); return null;
            case MensagemControle.AbrirItem a: return Abrir(a.Id);
            case MensagemControle.Botao b: Botao(b.Qual, b.Apertado); return null;
            default: return "mensagem desconhecida";
        }
    }

    // Tecla usada ao apertar cada botão: soltar solta a mesma, mesmo que o programa da frente
    // tenha mudado no meio (senão a tecla antiga ficaria presa)
    private readonly Dictionary<BotaoJogo, ushort> _apertadas = [];
    private readonly Lock _trava = new();

    private void Botao(BotaoJogo botao, bool apertado)
    {
        ushort vk;
        lock (_trava)
        {
            if (apertado) _apertadas[botao] = vk = MapaBotoes.Tecla(botao, ProgramaEmFoco());
            else if (!_apertadas.Remove(botao, out vk)) vk = MapaBotoes.Tecla(botao, ProgramaEmFoco());
        }
        Entrada.ApertarOuSoltar(vk, apertado);
    }

    /// <summary>Nome do processo da janela em foco (ex.: "Play", "retroarch"), ou null.</summary>
    private static string? ProgramaEmFoco()
    {
        var janela = GetForegroundWindow();
        if (janela == IntPtr.Zero || GetWindowThreadProcessId(janela, out var pid) == 0) return null;
        try { using var p = Process.GetProcessById((int)pid); return p.ProcessName; }
        catch (ArgumentException) { return null; }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr janela, out uint pid);

    private static void Tecla(ComandoTv comando)
    {
        switch (comando)
        {
            case ComandoTv.Cima: Entrada.Combinacao(Entrada.VkCima); break;
            case ComandoTv.Baixo: Entrada.Combinacao(Entrada.VkBaixo); break;
            case ComandoTv.Esquerda: Entrada.Combinacao(Entrada.VkEsquerda); break;
            case ComandoTv.Direita: Entrada.Combinacao(Entrada.VkDireita); break;
            case ComandoTv.Ok: Entrada.Combinacao(Entrada.VkEnter); break;
            // No WSA o Esc vira o "voltar" do Android; no YouTube TV e no Edge também volta
            case ComandoTv.Voltar: Entrada.Combinacao(Entrada.VkEsc); break;
            case ComandoTv.Inicio: Entrada.Combinacao(Entrada.VkWin, Entrada.VkD); break;
            case ComandoTv.VolumeMais: Entrada.Combinacao(Entrada.VkVolumeMais); break;
            case ComandoTv.VolumeMenos: Entrada.Combinacao(Entrada.VkVolumeMenos); break;
            case ComandoTv.Mudo: Entrada.Combinacao(Entrada.VkMudo); break;
            case ComandoTv.PlayPausa: Entrada.Combinacao(Entrada.VkPlayPausa); break;
            case ComandoTv.TelaCheia: Entrada.Combinacao(Entrada.VkF11); break;
            case ComandoTv.TrocarJanela: Entrada.Combinacao(Entrada.VkAlt, Entrada.VkTab); break;
            case ComandoTv.Fechar: Entrada.Combinacao(Entrada.VkAlt, Entrada.VkF4); break;
            case ComandoTv.Apagar: Entrada.Combinacao(Entrada.VkBackspace); break;
        }
    }

    private string? Abrir(string id)
    {
        var item = catalogo.FirstOrDefault(i => i.Id == id);
        if (item is null || !item.Habilitado) return "item indisponível";
        try
        {
            switch (item.Tipo)
            {
                case TipoItem.Android:
                    var android = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
                    android.ArgumentList.Add(EnderecoAndroid.Wsa(item.Destino));
                    Process.Start(android);
                    break;
                case TipoItem.Web:
                    var edge = LinhaDeComandoEdge.CaminhosProvaveis().FirstOrDefault(File.Exists);
                    if (edge is null) return "Edge não encontrado";
                    var perfil = Path.Combine(Pastas.PerfisEdge, item.PerfilEdge ?? "tv");
                    Directory.CreateDirectory(perfil);
                    var web = new ProcessStartInfo(edge) { UseShellExecute = false };
                    foreach (var a in LinhaDeComandoEdge.Argumentos(new Uri(item.Destino), perfil, item.UserAgent)) web.ArgumentList.Add(a);
                    Process.Start(web);
                    break;
                case TipoItem.Programa:
                    // Programa com caminho completo (ex.: ES-DE) abre uma vez só: tocar de novo traz a
                    // janela para a frente. Sem isso, cada toque abria mais uma cópia.
                    if (Path.IsPathFullyQualified(item.Destino) && TrazerParaFrente(item.Destino))
                    {
                        registro.Evento("controle_abrir", new { item.Id, jaAberto = true });
                        return null;
                    }
                    var programa = new ProcessStartInfo(item.Destino) { UseShellExecute = true };
                    if (!string.IsNullOrWhiteSpace(item.Argumentos)) programa.Arguments = item.Argumentos;
                    Process.Start(programa);
                    break;
            }
            registro.Evento("controle_abrir", new { item.Id });
            return null;
        }
        catch (Exception e)
        {
            registro.Evento("controle_abrir_falhou", new { item.Id, erro = e.GetType().Name, e.Message });
            return "não consegui abrir";
        }
    }

    /// <summary>Se o executável já está rodando, mostra a janela dele e devolve true.</summary>
    private static bool TrazerParaFrente(string exe)
    {
        var achou = false;
        foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))
        {
            using (p)
            {
                try { if (!string.Equals(p.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase)) continue; }
                catch (Exception) { continue; }
                achou = true;
                var janela = p.MainWindowHandle;
                if (janela == IntPtr.Zero) continue;
                ShowWindow(janela, MostrarRestaurado);
                SetForegroundWindow(janela);
                return true;
            }
        }
        return achou;
    }

    private const int MostrarRestaurado = 9;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr janela);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr janela, int comando);
}

/// <summary>Pastas por usuário, as mesmas da tela da TV (CennoTV.App).</summary>
internal static class Pastas
{
    public static string Raiz { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CennoTV");
    public static string Logs => Path.Combine(Raiz, "logs");
    public static string CatalogoLocal => Path.Combine(Raiz, "catalogo.json");
    public static string PerfisEdge => Path.Combine(Raiz, "edge");
    public static string Controles => Path.Combine(Raiz, "controles.json");
}
