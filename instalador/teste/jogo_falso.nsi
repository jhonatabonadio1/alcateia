; Jogo de mentira para testar o iniciador (instalador/teste/testar_iniciador.ps1):
; grava os argumentos em chamadas.txt, ao lado dele, e sai com erro na hora
; se o modo de vídeo pedido estiver na variável FALHAM ("vulkan d3d12").
Unicode true
Target amd64-unicode
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "WordFunc.nsh"

!ifndef SAIDA
  !define SAIDA "game\WolfLeague.exe"
!endif
OutFile "${SAIDA}"
SilentInstall silent
RequestExecutionLevel user

Section
  ${GetParameters} $0
  FileOpen $2 "$EXEDIR\chamadas.txt" a
  FileSeek $2 0 END
  FileWrite $2 "$0$\r$\n"
  FileClose $2
  ReadEnvStr $1 FALHAM
  ${WordFind} "$0" " " "+2" $3      ; o modo, depois de --rendering-driver
  ${WordFind} " $1 " " $3 " "E#" $4
  ${IfNot} ${Errors}
    SetErrorLevel 1
    Quit
  ${EndIf}
  SetErrorLevel 0
SectionEnd
