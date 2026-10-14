"""Testa a aba Botões da página do controle num celular emulado, com servidor falso.

Uso: python -I teste_botoes.py <caminho do index.html>
Confere as 4 abas, os 10 consoles, linhas-chave do N64 e do Wii, o console lembrado ao reabrir e
que nada passa da borda da tela (borda do elemento contra a do corpo, não pela rolagem).
"""
import sys
from playwright.sync_api import sync_playwright
sys.stdout.reconfigure(encoding="utf-8")

PAGINA = sys.argv[1]
html = open(PAGINA, encoding='utf-8').read()
falhas = []


def conferir(nome, cond, detalhe=''):
    print(('OK   ' if cond else 'FALHA') + ' ' + nome + (f'  ({detalhe})' if detalhe else ''))
    if not cond:
        falhas.append(nome)


def preparar(pg):
    pg.route('http://dell.local:8765/', lambda r: r.fulfill(status=200, content_type='text/html; charset=utf-8', body=html))
    pg.route('http://dell.local:8765/api/apps', lambda r: r.fulfill(status=200, content_type='application/json', body='[]'))
    pg.route('**/manifest.json', lambda r: r.fulfill(status=200, body='{}'))
    pg.route('**/icone.svg', lambda r: r.fulfill(status=200, content_type='image/svg+xml', body='<svg xmlns="http://www.w3.org/2000/svg"/>'))
    pg.route_web_socket('ws://dell.local:8765/ws**', lambda ws: None)


def tabela(pg):
    return pg.evaluate("[...document.querySelectorAll('#mapa tr')].map(tr => [...tr.children].map(td => td.textContent))")


with sync_playwright() as p:
    nav = p.chromium.launch()
    ctx = nav.new_context(**p.devices['Pixel 7'])
    ctx.add_init_script("localStorage.setItem('cenno-token','token-falso'); if (!localStorage.getItem('cenno-aba')) localStorage.setItem('cenno-aba','controle');")
    pg = ctx.new_page()
    preparar(pg)
    erros_js = []
    pg.on('pageerror', lambda e: erros_js.append(str(e)))
    pg.goto('http://dell.local:8765/')
    pg.wait_for_function("document.getElementById('estado').classList.contains('on')", timeout=5000)

    conferir('quatro abas', pg.locator('#abas button').count() == 4)
    pg.click('[data-aba="botoes"]')
    conferir('aba Botões abriu', pg.is_visible('#botoes') and not pg.is_visible('#controle'))
    nomes = pg.eval_on_selector_all('#consoles button', 'bs => bs.map(b => b.textContent)')
    conferir('10 consoles', len(nomes) == 10, ', '.join(nomes))
    conferir('abre no 1º console quando não há escolha salva', tabela(pg)[0] == ['✕', 'B', 'B'], str(tabela(pg)[0]))

    pg.click('#consoles button[data-console="n64"]')
    t = tabela(pg)
    conferir('N64: botões C segurando R2', ['Segure R2 + △ ○ ✕ □', 'Segure R2 + X A B Y', 'C cima, direita, baixo, esquerda'] in t)
    conferir('N64: Z no L2', ['L2', 'L2', 'Z'] in t)
    conferir('N64: botão marcado', pg.get_attribute('#consoles button[data-console="n64"]', 'class') == 'sel')
    conferir('nota de sair preenchida', 'Create + Options' in pg.text_content('#notaSair'))
    conferir('nota de trocar fala do menu do RetroArch', 'Menu rápido' in pg.text_content('#notaTrocar'))

    pg.click('#consoles button[data-console="wii"]')
    t = tabela(pg)
    conferir('Wii: R3 recentraliza a mira', ['R3', '—', 'Recentraliza a mira'] in t)
    conferir('Wii: sair com Select + Start', 'Select + Start' in pg.text_content('#notaSair'))
    pg.click('#consoles button[data-console="ps3"]')
    conferir('PS3: sem citar os botões "PS3 no celular/controle" (o público não tem)', 'PS3 no celular' not in pg.text_content('#notaTrocar'))
    conferir('cada linha tem 3 colunas em todos os consoles', pg.evaluate(
        "[...document.querySelectorAll('#consoles button')].every(b => { b.click(); return [...document.querySelectorAll('#mapa tr')].every(tr => tr.children.length === 3); })"))

    # Lembra o console e a aba ao reabrir
    pg.click('#consoles button[data-console="ps2"]')
    pg.reload()
    pg.wait_for_function("document.getElementById('estado').classList.contains('on')", timeout=5000)
    conferir('reabre na aba Botões', pg.is_visible('#botoes'))
    conferir('reabre no PS2', pg.get_attribute('#consoles button[data-console="ps2"]', 'class') == 'sel')

    # Nada passa da borda (a fileira de consoles rola por dentro, de propósito)
    for largura in (320, 360, 412):
        pg.set_viewport_size({'width': largura, 'height': 800})
        pg.reload()
        pg.wait_for_function("document.getElementById('estado').classList.contains('on')", timeout=5000)
        fora = pg.evaluate("""() => {
            const limite = document.body.getBoundingClientRect().right + 0.5;
            return [...document.querySelectorAll('#abas, #abas button, .mapa, .mapa td, .nota, #consoles')]
              .filter(e => e.getBoundingClientRect().right > limite)
              .map(e => (e.id || e.className || e.tagName) + ':' + Math.round(e.getBoundingClientRect().right - limite));
        }""")
        conferir(f'{largura}px: nada passa da borda', not fora, ', '.join(fora))

    conferir('sem erro de JavaScript', not erros_js, '; '.join(erros_js))
    nav.close()

print('\nRESULTADO: ' + ('TUDO OK' if not falhas else f'{len(falhas)} FALHA(S): ' + ', '.join(falhas)))
sys.exit(1 if falhas else 0)
