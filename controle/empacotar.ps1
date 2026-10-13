# Gera o .zip da versão a partir de publish\CennoTV.Controle.exe (dotnet publish -c Release -o publish)
param([string]$Raiz = $PSScriptRoot, [string]$Versao = '0.1.0')
$ErrorActionPreference = 'Stop'
$pac = Join-Path $Raiz 'publish\pacote'
New-Item -ItemType Directory -Force $pac | Out-Null
Copy-Item (Join-Path $Raiz 'publish\CennoTV.Controle.exe') $pac -Force
Copy-Item (Join-Path $Raiz 'instalar.ps1') $pac -Force
$leia = @"
Controle da TV pelo celular - versão $Versao

1. Extraia este .zip inteiro numa pasta.
2. Clique com o botão direito em instalar.ps1 > Executar com o PowerShell
   (ou, num terminal: powershell -ExecutionPolicy Bypass -File .\instalar.ps1).
3. Aceite o pedido de permissão de administrador.
4. No celular, na mesma Wi-Fi, abra o endereço que aparecer e toque em Parear.
   Um código aparece na TV: digite no celular.

Para remover: powershell -ExecutionPolicy Bypass -File .\instalar.ps1 -Remover

Código-fonte e guia completo: https://github.com/junioralbino28/tv-stick-game
"@
[IO.File]::WriteAllText((Join-Path $pac 'LEIA-ME.txt'), ($leia -replace "`r?`n", "`r`n"), [Text.UTF8Encoding]::new($true))
$zip = Join-Path $Raiz "publish\controle-tv-v$Versao-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $pac '*') -DestinationPath $zip -CompressionLevel Optimal
"{0}  {1:N1} MB  sha256 {2}" -f (Split-Path $zip -Leaf), ((Get-Item $zip).Length / 1MB), (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
