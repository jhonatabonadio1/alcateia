; O iniciador do WolfLeague para Windows: é o WolfLeague.exe que o jogador
; abre (atalho, Menu Iniciar, .zip). O jogo de verdade fica em game\.
;
; Por que existe (21/11): quando o vídeo não sobe, o Godot fecha sem dizer
; nada e não tenta outro modo. Ele só troca de modo quando o driver nem
; existe; se a placa anuncia o Direct3D 12 e falha ao iniciar (o que
; aconteceu no Parallels: "No support for any of the suitable shader
; models"), o jogo some. Os motores grandes resolvem com uma lista em ordem:
; o primeiro que funciona vale (Unity), DirectX 12 e depois 11 (Unreal).
; Aqui é a mesma ideia:
;
;   1. o modo escolhido nas Opções do jogo (%APPDATA%\WolfLeague\video.ini);
;   2. o que funcionou da última vez (registro, HKCU\Software\WolfLeague);
;   3. Vulkan, Direct3D 12, compatibilidade pelo ANGLE (OpenGL traduzido
;      para Direct3D 11, o que o Chrome usa; vem embutido no jogo) e, por
;      último, OpenGL direto.
;
; "Não serviu" = o jogo fechou com erro em menos de 20 s. Fechou bem, ou
; passou de 20 s, o modo serve e fica guardado. Se nenhum serve, uma
; mensagem diz o que fazer e abre a pasta do log, em vez de nada acontecer.
;
; Quem passa --rendering-driver na mão manda: o iniciador só repassa.
;
; Compilado pelo pipeline (.github/workflows/lancamento.yml):
;   makensis -DVERSAO_NUM=0.4.0.0 -DSAIDA="$PWD/build/windows/WolfLeague.exe" \
;            "$PWD/instalador/iniciador.nsi"

Unicode true
Target amd64-unicode
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "WordFunc.nsh"

!ifndef SAIDA
  !define SAIDA "WolfLeague.exe"
!endif
!ifndef VERSAO_NUM
  !define VERSAO_NUM "0.0.0.0"
!endif

!define NOME   "WolfLeague"
!define CHAVE  "Software\${NOME}"
!define RAPIDO 20000   ; ms: fechou com erro antes disso, o modo de vídeo não serviu
!define MODOS  "vulkan d3d12 opengl3_angle opengl3"

Name "${NOME}"
OutFile "${SAIDA}"
Icon "${__FILEDIR__}\WolfLeague.ico"
RequestExecutionLevel user
SilentInstall silent
SetCompressor /SOLID lzma
VIProductVersion "${VERSAO_NUM}"
VIAddVersionKey "ProductName" "${NOME}"
VIAddVersionKey "FileDescription" "${NOME}"
VIAddVersionKey "FileVersion" "${VERSAO_NUM}"
VIAddVersionKey "ProductVersion" "${VERSAO_NUM}"
VIAddVersionKey "CompanyName" "${NOME}"
VIAddVersionKey "LegalCopyright" ""

; Num iniciador silencioso o MessageBox do NSIS é pulado; a mensagem vai
; direto pela API do Windows (MB_ICONERROR | MB_SETFOREGROUND | MB_TOPMOST).
!macro Aviso TEXTO
  StrCpy $9 "${TEXTO}"
  System::Call 'user32::MessageBoxW(p 0, w r9, w "${NOME}", i 0x50010) i .r8'
!macroend

Var jogo
Var args
Var lista
Var tentados

; $R0 = modo. Roda o jogo nele; $R1 = 1 se serviu.
Function Tentar
  StrCpy $R1 0
  ${WordFind} "$tentados" " $R0 " "E#" $R2
  ${IfNot} ${Errors}
    Return            ; já tentado
  ${EndIf}
  StrCpy $tentados "$tentados $R0 "

  System::Call "kernel32::GetTickCount() i .R3"
  ClearErrors
  ExecWait '"$jogo" --rendering-driver $R0 $args' $R4
  ${If} ${Errors}
    Return            ; nem abriu (arquivo bloqueado, antivírus)
  ${EndIf}
  System::Call "kernel32::GetTickCount() i .R5"
  IntOp $R6 $R5 - $R3

  ${If} $R4 == 0
  ${OrIf} $R6 >= ${RAPIDO}
    StrCpy $R1 1
  ${EndIf}
FunctionEnd

Section
  StrCpy $jogo "$EXEDIR\game\${NOME}.exe"
  ${IfNot} ${FileExists} "$jogo"
    !insertmacro Aviso "Some ${NOME} files are missing:$\r$\n$jogo$\r$\n$\r$\nPlease reinstall the game."
    SetErrorLevel 1
    Quit
  ${EndIf}

  ${GetParameters} $args

  ; Modo pedido na linha de comando: só repassa.
  ${WordFind} "$args" "--rendering-driver" "E#" $0
  ${IfNot} ${Errors}
    ExecWait '"$jogo" $args' $0
    SetErrorLevel $0
    Quit
  ${EndIf}

  ; A ordem: escolhido nas Opções, o que funcionou da última vez, a lista.
  StrCpy $lista ""
  ReadINIStr $1 "$APPDATA\${NOME}\video.ini" "video" "mode"
  ${If} $1 == ""
    ReadINIStr $1 "$APPDATA\${NOME}\video.ini" "video" "modo"   ; até 21/11
  ${EndIf}
  ${WordFind} " ${MODOS} " " $1 " "E#" $0
  ${IfNot} ${Errors}
    StrCpy $lista "$1"
  ${EndIf}
  ReadRegStr $2 HKCU "${CHAVE}" "Video"
  ${WordFind} " ${MODOS} " " $2 " "E#" $0
  ${IfNot} ${Errors}
    StrCpy $lista "$lista $2"
  ${EndIf}
  StrCpy $lista "$lista ${MODOS}"

  StrCpy $tentados " "
  ${WordFind} "$lista" " " "#" $3    ; quantos
  StrCpy $4 1
  ${DoWhile} $4 <= $3
    ${WordFind} "$lista" " " "+$4" $R0
    Call Tentar
    ${If} $R1 == 1
      WriteRegStr HKCU "${CHAVE}" "Video" "$R0"
      ; O escolhido nas Opções não serviu: volta para Automático.
      ${If} $1 != ""
      ${AndIf} $1 != "auto"
      ${AndIf} $1 != "$R0"
        WriteINIStr "$APPDATA\${NOME}\video.ini" "video" "mode" "auto"
        DeleteINIStr "$APPDATA\${NOME}\video.ini" "video" "modo"
      ${EndIf}
      SetErrorLevel 0
      Quit
    ${EndIf}
    IntOp $4 $4 + 1
  ${Loop}

  ; Nenhum serviu.
  DeleteRegValue HKCU "${CHAVE}" "Video"
  !insertmacro Aviso "${NOME} could not start the video on this computer.$\r$\n$\r$\nWe tried Vulkan, DirectX 12 and compatibility mode (DirectX 11). Update your graphics card driver (from the NVIDIA, AMD or Intel website) and open the game again.$\r$\n$\r$\nIf it keeps happening, send us on Discord the godot.log file from the folder that opens now."
  CreateDirectory "$APPDATA\${NOME}\logs"
  ExecShell "open" "$APPDATA\${NOME}\logs"
  SetErrorLevel 1
SectionEnd
