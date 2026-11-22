# Testa o iniciador do Windows (instalador/iniciador.nsi) num Windows de
# verdade, com um jogo de mentira (jogo_falso.nsi) que sai com erro nos modos
# de vídeo listados em FALHAM. Roda no CI (testes.yml, job "iniciador") e em
# qualquer Windows com o NSIS:
#   pwsh instalador/teste/testar_iniciador.ps1
$ErrorActionPreference = "Stop"

$makensis = (Get-Command makensis -ErrorAction SilentlyContinue).Source
if (-not $makensis) {
    $makensis = Join-Path ${env:ProgramFiles(x86)} "NSIS\makensis.exe"
    if (-not (Test-Path $makensis)) { choco install nsis -y --no-progress | Out-Null }
}

$raiz = Join-Path ([IO.Path]::GetTempPath()) "wolfleague-iniciador"
Remove-Item -Recurse -Force $raiz -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force (Join-Path $raiz "game") | Out-Null
$iniciador = Join-Path $raiz "WolfLeague.exe"
$chamadas = Join-Path $raiz "game\chamadas.txt"

& $makensis -V2 "-DSAIDA=$iniciador" (Resolve-Path "instalador/iniciador.nsi").Path
if ($LASTEXITCODE) { throw "makensis: iniciador" }
& $makensis -V2 "-DSAIDA=$(Join-Path $raiz 'game\WolfLeague.exe')" (Resolve-Path "instalador/teste/jogo_falso.nsi").Path
if ($LASTEXITCODE) { throw "makensis: jogo de mentira" }

$ini = Join-Path $env:APPDATA "WolfLeague\video.ini"
$chave = "HKCU:\Software\WolfLeague"
$script:falhas = 0

function Rodar([string]$falham, [string]$argumentos = "") {
    Remove-Item -Force $chamadas -ErrorAction SilentlyContinue
    $env:FALHAM = $falham
    if ($argumentos) { Start-Process $iniciador -ArgumentList $argumentos -Wait | Out-Null }
    else { Start-Process $iniciador -Wait | Out-Null }
    if (Test-Path $chamadas) { return (Get-Content $chamadas) -join "|" }
    return ""
}
function Guardado { [string](Get-ItemProperty $chave -Name Video -ErrorAction SilentlyContinue).Video }
function Confere([string]$nome, [string]$obtido, [string]$esperado) {
    if ($obtido -ceq $esperado) { Write-Host "ok     $nome" }
    else {
        Write-Host "FALHOU $nome`n       esperado: $esperado`n       veio:     $obtido"
        $script:falhas++
    }
}
function Limpar {
    Remove-Item -Recurse -Force $chave -ErrorAction SilentlyContinue
    Remove-Item -Force $ini -ErrorAction SilentlyContinue
}

$v = "--rendering-driver vulkan"; $d = "--rendering-driver d3d12"
$a = "--rendering-driver opengl3_angle"; $g = "--rendering-driver opengl3"

Limpar
Confere "tudo funciona: abre no Vulkan" (Rodar "") $v
Confere "  e guarda o Vulkan" (Guardado) "vulkan"

Limpar
Confere "Vulkan e DirectX 12 falham: chega na compatibilidade" (Rodar "vulkan d3d12") "$v|$d|$a"
Confere "  e guarda a compatibilidade" (Guardado) "opengl3_angle"
Confere "de novo: abre direto no que funcionou" (Rodar "vulkan d3d12") $a
Confere "modo pedido na mão: só repassa" (Rodar "vulkan d3d12" "--rendering-driver opengl3 --verbose") "--rendering-driver opengl3 --verbose"
Confere "os outros argumentos vão junto" (Rodar "" "--verbose") "$a --verbose"

New-Item -ItemType Directory -Force (Split-Path $ini) | Out-Null
Set-Content $ini "[video]`r`nmode=d3d12"
Confere "escolhido nas Opções falha: cai no que funcionou" (Rodar "d3d12") "$d|$a"
Confere "  e as Opções voltam para Automático" ((Get-Content $ini) -join " ") "[video] mode=auto"
Set-Content $ini "[video]`r`nmode=vulkan"
Confere "escolhido nas Opções funciona" (Rodar "") $v

# Nada funciona: tenta os quatro, esquece o guardado e para na mensagem
# (aqui ninguém clica em OK; o processo é encerrado no fim).
Remove-Item -Force $ini -ErrorAction SilentlyContinue
Remove-Item -Force $chamadas -ErrorAction SilentlyContinue
$env:FALHAM = "vulkan d3d12 opengl3_angle opengl3"
$p = Start-Process $iniciador -PassThru
$p | Wait-Process -Timeout 30 -ErrorAction SilentlyContinue
Confere "nada funciona: tenta os quatro modos" ((Get-Content $chamadas) -join "|") "$v|$d|$a|$g"
Confere "  e esquece o que tinha guardado" (Guardado) ""
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }

Limpar
Remove-Item -Recurse -Force $raiz -ErrorAction SilentlyContinue
if ($script:falhas) { Write-Host "$($script:falhas) verificação(ões) falharam"; exit 1 }
Write-Host "iniciador: tudo certo"
