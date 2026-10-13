<#
.SYNOPSIS
    Instala (ou remove) o controle da TV pelo celular neste computador.

.DESCRIPTION
    Rode na conta do Windows que fica ligada na TV (uma conta de administrador). Pede permissão
    de administrador (para a regra do firewall) e faz:
      - copia o CennoTV.Controle.exe (que vem junto deste script) para
        %LOCALAPPDATA%\CennoTV\app;
      - libera no firewall só a porta 8765, só para este programa e só para a rede local;
      - cria a tarefa "Controle da TV", que abre o controle quando você entra no Windows e
        reabre em até 5 minutos se ele fechar;
      - mostra o endereço para abrir no celular.
    O resultado também fica em %LOCALAPPDATA%\CennoTV\instalacao.txt.

    -Remover desfaz tudo. Os celulares pareados ficam guardados em
    %LOCALAPPDATA%\CennoTV\controles.json; com -EsquecerControles são apagados também.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\instalar.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\instalar.ps1 -Remover -EsquecerControles
#>
param([switch]$Remover, [switch]$EsquecerControles, [string]$Usuario, [switch]$Janela)

$ErrorActionPreference = 'Stop'
$raiz = Join-Path $env:LOCALAPPDATA 'CennoTV'
$resultado = Join-Path $raiz 'instalacao.txt'

# Sem administrador: abre de novo pedindo permissão (mesma conta) e mostra o resultado aqui
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
    $argumentos = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-Janela',
        '-Usuario', "`"$([Security.Principal.WindowsIdentity]::GetCurrent().Name)`"")
    if ($Remover) { $argumentos += '-Remover' }
    if ($EsquecerControles) { $argumentos += '-EsquecerControles' }
    Remove-Item $resultado -Force -ErrorAction SilentlyContinue
    Start-Process powershell.exe -Verb RunAs -ArgumentList $argumentos -Wait
    if (Test-Path $resultado) { Get-Content $resultado -Encoding UTF8 } else { 'A janela de administrador não rodou (permissão negada?).' }
    return
}
if (-not $Usuario) { $Usuario = [Security.Principal.WindowsIdentity]::GetCurrent().Name }

New-Item -ItemType Directory -Force -Path $raiz | Out-Null
Set-Content $resultado '' -Encoding UTF8
function Dizer([string]$t) { [Console]::Out.WriteLine($t); Add-Content -Path $resultado -Value $t -Encoding UTF8 }

$pasta = Join-Path $raiz 'app'
$exe = Join-Path $pasta 'CennoTV.Controle.exe'
$origem = Join-Path $PSScriptRoot 'CennoTV.Controle.exe'
$tarefa = 'Controle da TV'
$regra = 'Controle da TV (rede local)'
$porta = 8765

function Parar {
    if (Get-ScheduledTask -TaskName $tarefa -ErrorAction SilentlyContinue) { Stop-ScheduledTask -TaskName $tarefa -ErrorAction SilentlyContinue }
    Get-Process -Name 'CennoTV.Controle' -ErrorAction SilentlyContinue | Stop-Process -Force
    foreach ($i in 1..20) { if (-not (Get-Process -Name 'CennoTV.Controle' -ErrorAction SilentlyContinue)) { return }; Start-Sleep -Milliseconds 250 }
    throw 'O controle não fechou. Feche pelo Gerenciador de Tarefas e rode de novo.'
}

try {
    if ($Remover) {
        Parar
        Unregister-ScheduledTask -TaskName $tarefa -Confirm:$false -ErrorAction SilentlyContinue
        Get-NetFirewallRule -DisplayName $regra -ErrorAction SilentlyContinue | Remove-NetFirewallRule
        if (Test-Path $pasta) { Remove-Item $pasta -Recurse -Force }
        if ($EsquecerControles) { Remove-Item (Join-Path $raiz 'controles.json') -Force -ErrorAction SilentlyContinue }
        Dizer ("Removido: tarefa, regra do firewall e $pasta." + $(if ($EsquecerControles) { ' Celulares pareados apagados.' } else { ' Celulares pareados mantidos.' }))
        return
    }

    if (-not (Test-Path $origem)) { throw "Não achei $origem. Extraia o .zip inteiro numa pasta e rode o instalar.ps1 de lá." }
    New-Item -ItemType Directory -Force -Path $pasta | Out-Null
    Parar
    Copy-Item $origem $exe -Force
    # O arquivo veio da internet dentro do .zip: tira a marca de "baixado" para a tarefa conseguir abrir
    Unblock-File $exe

    # Firewall: só a nossa regra. Uma regra de bloqueio criada por um aviso do Windows (alguém
    # clicou em Cancelar) impediria o celular de conectar; essas são só deste programa.
    Get-NetFirewallRule -DisplayName $regra -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    Get-NetFirewallApplicationFilter -Program $exe -ErrorAction SilentlyContinue | Get-NetFirewallRule |
        Where-Object { $_.Action -eq 'Block' } | ForEach-Object { Dizer "Removendo bloqueio antigo do firewall: $($_.DisplayName)"; $_ | Remove-NetFirewallRule }
    New-NetFirewallRule -DisplayName $regra -Description 'Controle da TV pelo celular. Só a rede local.' `
        -Direction Inbound -Action Allow -Program $exe -Protocol TCP -LocalPort $porta -RemoteAddress LocalSubnet -Profile Any | Out-Null

    # Tarefa: ao entrar no Windows + a cada 5 min (se já estiver aberto, não abre outro)
    $acao = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $pasta
    $gatilhos = @(
        (New-ScheduledTaskTrigger -AtLogOn -User $Usuario),
        (New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 5))
    )
    $principal = New-ScheduledTaskPrincipal -UserId $Usuario -LogonType Interactive -RunLevel Limited
    # Prioridade 5 = normal (o padrão das tarefas, 7, é abaixo do normal e atrasa o controle)
    $config = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable `
        -ExecutionTimeLimit (New-TimeSpan -Hours 0) -MultipleInstances IgnoreNew -Priority 5
    Register-ScheduledTask -TaskName $tarefa -Action $acao -Trigger $gatilhos -Principal $principal -Settings $config -Force | Out-Null

    Start-ScheduledTask -TaskName $tarefa
    $ok = $false
    foreach ($i in 1..40) {
        try { if ((Invoke-WebRequest "http://127.0.0.1:$porta/manifest.json" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { $ok = $true; break } } catch { }
        Start-Sleep -Milliseconds 500
    }
    if (-not $ok) { throw "O controle não respondeu na porta $porta. Veja os registros em $raiz\logs." }

    $enderecos = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notmatch '^(127\.|169\.254\.|100\.)' -and $_.InterfaceAlias -notmatch '^vEthernet' }
    Dizer 'Controle instalado. No celular, na mesma Wi-Fi, abra:'
    $enderecos | ForEach-Object { Dizer "  http://$($_.IPAddress):$porta   ($($_.InterfaceAlias))" }
    Dizer 'Toque em Parear: um código aparece na TV. Digite o código no celular.'
    Dizer 'Depois, reserve esse endereço no app do roteador ("Reserva de endereço" ou "DHCP estático"), para ele não mudar.'
} catch {
    Dizer "ERRO: $($_.Exception.Message)"
} finally {
    if ($Janela) { Read-Host 'Pressione Enter para fechar' | Out-Null }
}
