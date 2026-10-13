# TV Stick Game

Transforme um notebook ou PC com Windows na central da sua TV: streaming em tela cheia, apps de
Android, jogos do PS1 ao PS3 e um controle pelo celular, que abre no navegador, sem instalar app.

O computador vai no HDMI da TV. O celular, na mesma Wi-Fi, vira o controle: setas, OK, mouse,
teclado e um modo Jogo com os botões de videogame.

## Como instalar

- **Com IA (recomendado):** abra o Claude Code ou o Codex no computador da TV e cole:

  ```
  Leia o guia em https://raw.githubusercontent.com/junioralbino28/tv-stick-game/main/INSTALAR-COM-IA.md e siga as instruções dele para configurar este computador. Fale comigo em português.
  ```

- **Sem IA:** baixe o controle em [Releases](https://github.com/junioralbino28/tv-stick-game/releases),
  extraia e rode o `instalar.ps1`. O resto (jogos, Android, energia) está explicado passo a passo
  no [guia](INSTALAR-COM-IA.md), que também serve para fazer à mão.

## O que tem aqui

| Pasta | O que é |
|---|---|
| [INSTALAR-COM-IA.md](INSTALAR-COM-IA.md) | o guia completo, com os links diretos e os códigos de conferência de cada programa |
| [controle/](controle/) | código do controle pelo celular (C#, .NET 10) e o `instalar.ps1` |

O controle só aceita aparelhos da rede local (regra no firewall e checagem no próprio programa) e
cada celular precisa ser pareado com um código que aparece na TV.

## Aviso

Este projeto não fornece jogos, BIOS, sistema de console, apps nem listas de IPTV. Ele mostra os
programas gratuitos usados e como configurar cada um. O que cada pessoa instala no próprio
computador é responsabilidade dela.

Licença: MIT.
