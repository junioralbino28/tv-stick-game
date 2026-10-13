"""Testa a aba Teclado da página do controle num celular emulado, com servidor falso.

Nada vai para um receptor de verdade: /api/apps e o WebSocket são simulados pelo Playwright,
e as mensagens que a página manda ficam numa lista para conferir.
"""
import json
import sys
from playwright.sync_api import sync_playwright
sys.stdout.reconfigure(encoding="utf-8")

PAGINA = sys.argv[1]
html = open(PAGINA, encoding='utf-8').read()
enviadas = []
falhas = []


def conferir(nome, cond, detalhe=''):
    print(('OK   ' if cond else 'FALHA') + ' ' + nome + (f'  ({detalhe})' if detalhe else ''))
    if not cond:
        falhas.append(nome)


def tela_tv():
    """Reproduz o que a TV mostraria: texto acumulado, com Apagar tirando o último caractere."""
    s = ''
    for m in enviadas:
        if m == {'t': 'tecla', 'k': 'Apagar'}:
            s = s[:-1]
        elif m.get('t') == 'texto':
            s += m['s']
    return s


with sync_playwright() as p:
    nav = p.chromium.launch()
    ctx = nav.new_context(**p.devices['Pixel 7'])
    ctx.add_init_script("localStorage.setItem('cenno-token','token-falso'); if (!localStorage.getItem('cenno-aba')) localStorage.setItem('cenno-aba','controle');")
    pg = ctx.new_page()
    pg.route('http://dell.local:8765/', lambda r: r.fulfill(status=200, content_type='text/html; charset=utf-8', body=html))
    pg.route('http://dell.local:8765/api/apps', lambda r: r.fulfill(status=200, content_type='application/json', body='[]'))
    pg.route('**/manifest.json', lambda r: r.fulfill(status=200, body='{}'))
    pg.route('**/icone.svg', lambda r: r.fulfill(status=200, content_type='image/svg+xml', body='<svg xmlns="http://www.w3.org/2000/svg"/>'))
    pg.route_web_socket('ws://dell.local:8765/ws**', lambda ws: ws.on_message(lambda m: enviadas.append(json.loads(m))))
    erros_js = []
    pg.on('pageerror', lambda e: erros_js.append(str(e)))

    pg.goto('http://dell.local:8765/')
    pg.wait_for_function("document.getElementById('estado').classList.contains('on')", timeout=5000)
    conferir('três abas visíveis', pg.locator('#abas button').count() == 3)

    # Botão "Abrir teclado" do Controle leva à aba e põe o foco no campo
    pg.click('[data-aba-ir="teclado"]')
    conferir('aba Teclado abriu', pg.is_visible('#teclado') and not pg.is_visible('#controle'))
    conferir('campo com foco (teclado do celular abre)', pg.evaluate("document.activeElement.id") == 'aoVivo')

    # Digitar letra a letra: cada uma vai na hora
    enviadas.clear()
    pg.keyboard.type('Novela 2', delay=30)
    pg.wait_for_timeout(150)
    conferir('cada letra foi mandada na hora', len([m for m in enviadas if m.get('t') == 'texto']) == 8, json.dumps(enviadas))
    conferir('TV mostra "Novela 2"', tela_tv() == 'Novela 2', tela_tv())

    # Apagar no teclado do celular: manda Apagar
    pg.keyboard.press('Backspace'); pg.keyboard.press('Backspace')
    pg.wait_for_timeout(100)
    conferir('Backspace do celular apaga na TV', tela_tv() == 'Novela', tela_tv())

    # Correção automática: o celular troca a palavra inteira (simula trocando o valor do campo)
    pg.evaluate("""() => { const v = document.getElementById('aoVivo'); v.value = 'Novela das nove'; v.dispatchEvent(new Event('input')); }""")
    pg.wait_for_timeout(100)
    conferir('troca de palavra chega certa na TV', tela_tv() == 'Novela das nove', tela_tv())
    pg.evaluate("""() => { const v = document.getElementById('aoVivo'); v.value = 'Novelá das nove'; v.dispatchEvent(new Event('input')); }""")
    pg.wait_for_timeout(100)
    conferir('acento no meio: apaga do ponto da mudança e redigita', tela_tv() == 'Novelá das nove', tela_tv())

    # Enter: manda OK e limpa o campo do celular
    n = len(enviadas)
    pg.keyboard.press('Enter'); pg.wait_for_timeout(100)
    conferir('Enter manda OK', enviadas[n:] == [{'t': 'tecla', 'k': 'Ok'}], json.dumps(enviadas[n:]))
    conferir('campo limpo depois do Enter', pg.input_value('#aoVivo') == '')

    # Depois do Enter, digitar de novo não apaga nada antigo na TV
    n = len(enviadas)
    pg.keyboard.type('ab', delay=30); pg.wait_for_timeout(100)
    conferir('nova busca começa sem apagar', enviadas[n:] == [{'t': 'texto', 's': 'a'}, {'t': 'texto', 's': 'b'}], json.dumps(enviadas[n:]))

    # Botão ⌫ da tela
    n = len(enviadas)
    pg.click('#teclado [data-k="Apagar"]'); pg.wait_for_timeout(100)
    conferir('botão ⌫ manda Apagar', enviadas[n:] == [{'t': 'tecla', 'k': 'Apagar'}], json.dumps(enviadas[n:]))

    # Campo antigo removido e nenhum erro de JavaScript
    conferir('campo antigo saiu', pg.locator('#digitar').count() == 0)
    conferir('sem erro de JavaScript', not erros_js, '; '.join(erros_js))

    # A aba fica salva
    pg.reload(); pg.wait_for_function("document.getElementById('estado').classList.contains('on')", timeout=5000)
    conferir('aba Teclado lembrada ao reabrir', pg.is_visible('#teclado'))
    nav.close()

print('\nRESULTADO:', 'TUDO OK' if not falhas else f'{len(falhas)} falha(s): ' + ', '.join(falhas))
sys.exit(1 if falhas else 0)
