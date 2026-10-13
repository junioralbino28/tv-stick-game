"""Testa o modo Jogo da página do controle num celular emulado, com servidor falso.

Nada vai para um receptor de verdade: /api/apps e o WebSocket são simulados pelo Playwright,
e as mensagens que a página manda ficam numa lista para conferir.
"""
import json
import sys
from playwright.sync_api import sync_playwright
sys.stdout.reconfigure(encoding="utf-8")

PAGINA = sys.argv[1]
SAIDA = sys.argv[2]
html = open(PAGINA, encoding='utf-8').read()
enviadas = []
falhas = []


def conferir(nome, cond, detalhe=''):
    print(('OK   ' if cond else 'FALHA') + ' ' + nome + (f'  ({detalhe})' if detalhe else ''))
    if not cond:
        falhas.append(nome)


with sync_playwright() as p:
    nav = p.chromium.launch()
    ctx = nav.new_context(**p.devices['Pixel 7'])
    ctx.add_init_script("localStorage.setItem('cenno-token','token-falso');"
                        "window.__telaCheia = 0; Element.prototype.requestFullscreen = function () { window.__telaCheia++; return Promise.resolve(); };")
    pg = ctx.new_page()
    pg.route('http://dell.local:8765/', lambda r: r.fulfill(status=200, content_type='text/html; charset=utf-8', body=html))
    pg.route('http://dell.local:8765/api/apps', lambda r: r.fulfill(status=200, content_type='application/json',
             body=json.dumps([{'id': 'youtube', 'titulo': 'YouTube', 'cor': '#C4302B'}, {'id': 'jogos', 'titulo': 'Jogos', 'cor': '#2E7D32'}])))
    pg.route('**/manifest.json', lambda r: r.fulfill(status=200, body='{}'))
    pg.route('**/icone.svg', lambda r: r.fulfill(status=200, content_type='image/svg+xml', body='<svg xmlns="http://www.w3.org/2000/svg"/>'))
    pg.route_web_socket('ws://dell.local:8765/ws**', lambda ws: ws.on_message(lambda m: enviadas.append(json.loads(m))))
    erros_js = []
    pg.on('pageerror', lambda e: erros_js.append(str(e)))

    pg.goto('http://dell.local:8765/')
    pg.wait_for_function("document.getElementById('estado').classList.contains('on')", timeout=5000)
    conferir('conectou e mostrou as abas', pg.is_visible('#abas'))
    conferir('botão Jogos veio da lista de apps', pg.locator('#apps button', has_text='Jogos').count() == 1)

    pg.click('[data-aba="jogo"]')
    conferir('aba Jogo abriu', pg.is_visible('#jogo') and not pg.is_visible('#controle'))
    conferir('pediu tela cheia ao abrir a aba Jogo', pg.evaluate('window.__telaCheia') >= 1)

    # Botão A: apertar e soltar
    enviadas.clear()
    a = pg.locator('.acoes .a').bounding_box()
    pg.mouse.move(a['x'] + a['width'] / 2, a['y'] + a['height'] / 2)
    pg.mouse.down(); pg.wait_for_timeout(150); pg.mouse.up(); pg.wait_for_timeout(100)
    conferir('A manda apertar e soltar', enviadas == [{'t': 'botao', 'k': 'A', 'p': True}, {'t': 'botao', 'k': 'A', 'p': False}], json.dumps(enviadas))

    # Direcional: dedo à direita, escorrega para a diagonal de cima, solta
    enviadas.clear()
    c = pg.locator('#cruz').bounding_box()
    cx, cy, w = c['x'] + c['width'] / 2, c['y'] + c['height'] / 2, c['width']
    pg.mouse.move(cx + w * .35, cy); pg.mouse.down(); pg.wait_for_timeout(80)
    pg.mouse.move(cx + w * .3, cy - w * .3, steps=4); pg.wait_for_timeout(80)
    pg.mouse.move(cx, cy - w * .35, steps=4); pg.wait_for_timeout(80)
    pg.mouse.up(); pg.wait_for_timeout(100)
    seq = [(m['k'], m['p']) for m in enviadas]
    conferir('direcional: Direita → diagonal (Direita+Cima) → Cima → solta',
             seq == [('Direita', True), ('Cima', True), ('Direita', False), ('Cima', False)], str(seq))

    # Segurar e sair da aba: tem que soltar
    enviadas.clear()
    b = pg.locator('.acoes .b').bounding_box()
    pg.mouse.move(b['x'] + b['width'] / 2, b['y'] + b['height'] / 2); pg.mouse.down(); pg.wait_for_timeout(80)
    pg.evaluate("document.querySelector('[data-aba-ir=controle]').dispatchEvent(new MouseEvent('click', {bubbles:true}))")
    pg.wait_for_timeout(100)
    conferir('trocar de aba com B apertado solta o B', {'t': 'botao', 'k': 'B', 'p': False} in enviadas, json.dumps(enviadas))
    pg.mouse.up()

    # L2 (Z do N64)
    pg.mouse.up(); pg.click('[data-aba="jogo"]'); pg.wait_for_timeout(100)
    enviadas.clear()
    l2 = pg.locator('[data-b="L2"]').bounding_box()
    pg.mouse.move(l2['x'] + l2['width'] / 2, l2['y'] + l2['height'] / 2); pg.mouse.down(); pg.wait_for_timeout(80); pg.mouse.up(); pg.wait_for_timeout(80)
    conferir('L2 manda apertar e soltar', [(m['k'], m['p']) for m in enviadas] == [('L2', True), ('L2', False)], json.dumps(enviadas))

    # Celular de lado: sem barra (412) e com a barra do navegador (300)
    pg.click('[data-aba="jogo"]')
    for alt in (412, 300):
        pg.set_viewport_size({'width': 915, 'height': alt})
        pg.wait_for_timeout(250)
        caixa = {s: pg.locator(s).bounding_box() for s in ['#cruz', '.acoes', '.jogo-topo', '#sairJogo', '[data-aba-ir=controle]']}
        dentro = all(v['y'] >= 0 and v['y'] + v['height'] <= alt + 1 and v['x'] >= 0 and v['x'] + v['width'] <= 915 for v in caixa.values())
        conferir(f'deitado 915x{alt}: tudo cabe', dentro, json.dumps({k: [round(v['x']), round(v['y']), round(v['width']), round(v['height'])] for k, v in caixa.items()}))
        conferir(f'deitado 915x{alt}: título e abas escondidos', not pg.is_visible('header') and not pg.is_visible('#abas'))
        pg.screenshot(path=SAIDA + f'-deitado-{alt}.png')
    pg.set_viewport_size({'width': 412, 'height': 915})
    pg.wait_for_timeout(200)
    pg.screenshot(path=SAIDA + '-em-pe.png')
    largura = pg.evaluate('document.documentElement.scrollWidth')
    conferir('em pé: sem rolagem horizontal', largura <= 412, f'scrollWidth={largura}')

    conferir('sem erro de JavaScript', not erros_js, '; '.join(erros_js))
    nav.close()

print('RESULTADO:', 'tudo certo' if not falhas else f'{len(falhas)} falha(s): {falhas}')
sys.exit(1 if falhas else 0)
