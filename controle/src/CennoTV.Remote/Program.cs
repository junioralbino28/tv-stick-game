using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using CennoTV.Core.Catalogo;
using CennoTV.Core.Controle;
using CennoTV.Core.Registro;
using CennoTV.Remote;

// Receptor do controle remoto da CENNO TV.
// Roda na sessão da TV (abre com o login) e escuta só a rede local (firewall + checagem de IP).
// Fluxo: o celular abre http://<dell>:8765, pede pareamento, a TV mostra um código, o celular
// manda o código e recebe um token; com o token abre o WebSocket /ws e manda os comandos.

const int Porta = 8765;
// O touchpad manda até ~60 movimentos/s; 40 descartava quase metade e o mouse andava aos trancos
const int MaxMensagensPorSegundo = 120;

using var unica = new Mutex(initiallyOwned: true, @"Local\CennoTV.Controle", out var primeira);
if (!primeira) return;

var registro = new RegistroJson(Pastas.Logs);
var catalogo = CarregarCatalogo(registro);
var pareamento = new Pareamento(Pastas.Controles);
var executor = new Executor(catalogo, registro);
var aviso = new AvisoTv();

var builder = WebApplication.CreateBuilder(args);
// --urls (ou ASPNETCORE_URLS) troca o endereço; sem isso escuta a rede local na porta padrão
if (string.IsNullOrEmpty(builder.Configuration["urls"])) builder.WebHost.UseUrls($"http://0.0.0.0:{Porta}");
builder.WebHost.ConfigureKestrel(k =>
{
    k.Limits.MaxRequestBodySize = 4 * 1024;
    k.Limits.MaxConcurrentConnections = 20;
    k.AddServerHeader = false;
});
builder.Logging.ClearProviders();
var app = builder.Build();

// Só rede local: loopback, privada (RFC 1918) e link-local. Reforça a regra do firewall.
app.Use(async (ctx, proximo) =>
{
    var ip = ctx.Connection.RemoteIpAddress;
    if (ip is null || !RedeLocal(ip)) { ctx.Response.StatusCode = 403; return; }
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    // ws:// explícito: navegador antigo não casa 'self' com o esquema ws
    ctx.Response.Headers["Content-Security-Policy"] = $"default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; connect-src 'self' ws://{ctx.Request.Host}; img-src 'self' data:";
    await proximo();
});
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });

app.MapGet("/", () => Recurso("web.index.html", "text/html; charset=utf-8"));
app.MapGet("/manifest.json", () => Recurso("web.manifest.json", "application/manifest+json"));
app.MapGet("/icone.svg", () => Recurso("web.icone.svg", "image/svg+xml"));

app.MapPost("/api/parear", () =>
{
    var codigo = pareamento.PedirCodigo();
    if (codigo is null) return Results.StatusCode(429);
    aviso.Mostrar(codigo, Pareamento.Validade);
    registro.Evento("controle_pareamento_pedido");
    return Results.Accepted();
});

app.MapPost("/api/confirmar", (PedidoConfirmacao pedido) =>
{
    var token = pareamento.Confirmar(pedido.Codigo ?? "", pedido.Nome ?? "");
    registro.Evento(token is null ? "controle_pareamento_recusado" : "controle_pareado", new { nome = pedido.Nome });
    if (token is null) return Results.StatusCode(403);
    aviso.Fechar();
    return Results.Ok(new { token });
});

app.MapGet("/api/apps", (HttpContext ctx) =>
{
    if (!pareamento.Valido(Token(ctx))) return Results.StatusCode(401);
    return Results.Ok(catalogo.Where(i => i.Habilitado).Select(i => new { i.Id, i.Titulo, i.Cor }));
});

app.Map("/ws", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    // Origem: só a própria página do Dell (evita outra página da rede abrir o socket)
    var origem = ctx.Request.Headers.Origin.ToString();
    if (origem.Length > 0 && (!Uri.TryCreate(origem, UriKind.Absolute, out var o) || o.Authority != ctx.Request.Host.Value))
    { ctx.Response.StatusCode = 403; return; }
    if (!pareamento.Valido(ctx.Request.Query["token"])) { ctx.Response.StatusCode = 401; return; }

    using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
    var buffer = new byte[LeitorControle.MaxBytesMensagem];
    var janela = DateTime.UtcNow;
    int naJanela = 0, total = 0, recusadas = 0;
    // Botões do modo Jogo apertados por esta conexão: soltos no fim, mesmo se ela cair no meio
    var apertados = new HashSet<BotaoJogo>();
    registro.Evento("controle_conectado", new { ip = ctx.Connection.RemoteIpAddress?.ToString() });

    try
    {
        while (socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult r;
            try { r = await socket.ReceiveAsync(buffer, ctx.RequestAborted); }
            catch (Exception e)
            {
                if (!ctx.RequestAborted.IsCancellationRequested) registro.Evento("controle_erro_leitura", new { erro = e.GetType().Name, e.Message });
                break;
            }
            if (r.MessageType == WebSocketMessageType.Close)
            {
                try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); } catch (WebSocketException) { }
                break;
            }
            if (!r.EndOfMessage || r.MessageType != WebSocketMessageType.Text)
            {
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "mensagem", CancellationToken.None);
                break;
            }

            var mensagem = LeitorControle.Ler(Encoding.UTF8.GetString(buffer, 0, r.Count), out var erro);
            if (mensagem is null) { recusadas++; continue; }

            // O limite nunca descarta um "soltar": a tecla ficaria presa até a conexão cair
            var agora = DateTime.UtcNow;
            if ((agora - janela).TotalSeconds >= 1) { janela = agora; naJanela = 0; }
            if (++naJanela > MaxMensagensPorSegundo && mensagem is not MensagemControle.Botao { Apertado: false }) { recusadas++; continue; }
            total++;
            if (mensagem is MensagemControle.Botao b) { if (b.Apertado) apertados.Add(b.Qual); else apertados.Remove(b.Qual); }
            var resposta = executor.Executar(mensagem);
            if (resposta is not null)
                await socket.SendAsync(Encoding.UTF8.GetBytes($$"""{"erro":"{{resposta}}"}"""), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
    finally
    {
        foreach (var b in apertados) executor.Executar(new MensagemControle.Botao(b, false));
        registro.Evento("controle_desconectado", new { total, recusadas, soltos = apertados.Count });
    }
});

registro.Evento("controle_inicio", new { porta = Porta, itens = catalogo.Count, controles = pareamento.Controles.Count });
app.Run();

static string? Token(HttpContext ctx)
{
    var cabecalho = ctx.Request.Headers.Authorization.ToString();
    return cabecalho.StartsWith("Bearer ", StringComparison.Ordinal) ? cabecalho[7..] : null;
}

static IResult Recurso(string nome, string tipo)
{
    using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream(nome);
    if (fluxo is null) return Results.NotFound();
    using var leitor = new StreamReader(fluxo, Encoding.UTF8);
    return Results.Text(leitor.ReadToEnd(), tipo);
}

static bool RedeLocal(IPAddress ip)
{
    if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
    if (IPAddress.IsLoopback(ip)) return true;
    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        return ip.IsIPv6LinkLocal || ip.IsIPv6UniqueLocal;
    var b = ip.GetAddressBytes();
    return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254);
}

static IReadOnlyList<ItemCatalogo> CarregarCatalogo(RegistroJson registro)
{
    using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream("CennoTV.catalogo.padrao.json")!;
    using var leitor = new StreamReader(fluxo, Encoding.UTF8);
    var padrao = CatalogoLoader.Ler(leitor.ReadToEnd(), "catálogo padrão");
    ResultadoCatalogo? local = File.Exists(Pastas.CatalogoLocal)
        ? CatalogoLoader.Ler(File.ReadAllText(Pastas.CatalogoLocal), Pastas.CatalogoLocal)
        : null;
    var final = CatalogoLoader.Combinar(padrao, local);
    foreach (var p in final.Problemas) registro.Evento("catalogo_problema", new { problema = p });
    return final.Itens;
}

internal sealed record PedidoConfirmacao(string? Codigo, string? Nome);
