using System.Drawing;
using System.Windows.Forms;

namespace CennoTV.Remote;

/// <summary>
/// Mostra o código de pareamento na TV, por cima de tudo (inclusive app em tela cheia), até o
/// celular confirmar ou o código vencer. Roda numa thread STA própria do WinForms.
/// </summary>
internal sealed class AvisoTv
{
    private readonly Lock _trava = new();
    private Form? _janela;

    public void Mostrar(string codigo, TimeSpan duracao)
    {
        Fechar();
        var pronta = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            var janela = Montar(codigo);
            var tempo = new System.Windows.Forms.Timer { Interval = (int)duracao.TotalMilliseconds };
            tempo.Tick += (_, _) => { tempo.Stop(); janela.Close(); };
            janela.Shown += (_, _) => { tempo.Start(); pronta.Set(); };
            janela.FormClosed += (_, _) => { tempo.Dispose(); lock (_trava) if (_janela == janela) _janela = null; };
            lock (_trava) _janela = janela;
            Application.Run(janela);
        }) { IsBackground = true, Name = "AvisoTv" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        pronta.Wait(TimeSpan.FromSeconds(5));
    }

    public void Fechar()
    {
        Form? janela;
        lock (_trava) janela = _janela;
        if (janela is { IsDisposed: false })
            try { janela.BeginInvoke(() => janela.Close()); } catch (InvalidOperationException) { }
    }

    private static Form Montar(string codigo)
    {
        var laranja = Color.FromArgb(0xF8, 0x70, 0x00);
        var janela = new JanelaSemFoco
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.CenterScreen,
            TopMost = true,
            ShowInTaskbar = false,
            BackColor = Color.FromArgb(0x14, 0x17, 0x0E),
            Size = new Size(620, 300),
            Padding = new Padding(24),
        };
        var titulo = new Label
        {
            Text = "Código do controle CENNO TV",
            ForeColor = Color.FromArgb(0xF2, 0xED, 0xE3),
            Font = new Font("Segoe UI", 20, FontStyle.Regular),
            Dock = DockStyle.Top,
            Height = 60,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        var numero = new Label
        {
            Text = $"{codigo[..3]} {codigo[3..]}",
            ForeColor = laranja,
            Font = new Font("Segoe UI", 64, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        var rodape = new Label
        {
            Text = "Digite no celular. Vale por 2 minutos.",
            ForeColor = Color.FromArgb(0xB8, 0xB2, 0xA6),
            Font = new Font("Segoe UI", 14),
            Dock = DockStyle.Bottom,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        janela.Controls.Add(numero);
        janela.Controls.Add(rodape);
        janela.Controls.Add(titulo);
        janela.Paint += (_, e) =>
        {
            using var borda = new Pen(laranja, 4);
            e.Graphics.DrawRectangle(borda, 2, 2, janela.Width - 4, janela.Height - 4);
        };
        return janela;
    }

    /// <summary>
    /// Aparece por cima sem tomar o foco: as teclas do controle continuam indo para o app que
    /// está na tela (um app Android no WSA, por exemplo).
    /// </summary>
    private sealed class JanelaSemFoco : Form
    {
        private const int WsExTopmost = 0x00000008, WsExToolWindow = 0x00000080, WsExNoActivate = 0x08000000;

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var p = base.CreateParams;
                p.ExStyle |= WsExTopmost | WsExToolWindow | WsExNoActivate;
                return p;
            }
        }
    }
}
