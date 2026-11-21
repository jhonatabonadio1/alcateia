; Instalador do WolfLeague para Windows (NSIS), com as cores do menu do jogo.
;
; É o pipeline (.github/workflows/lancamento.yml) quem roda isto, depois de
; exportar o jogo. À mão, com `makensis` instalado (Linux, macOS ou Windows):
;
;   python3 instalador/gerar_lista.py build/windows build/desinstalar.nsh
;   makensis -DVERSAO=0.4.0-alpha.1 -DVERSAO_NUM=0.4.0.0 \
;            -DORIGEM="$PWD/build/windows" -DLISTA="$PWD/build/desinstalar.nsh" \
;            -DSAIDA="$PWD/build/WolfLeague-Setup.exe" "$PWD/instalador/windows.nsi"
;
; O .nsi vai com caminho absoluto: o makensis entra na pasta dele antes de
; compilar, e a arte vem de ${__FILEDIR__} (relativo, não acha o ícone).
;
; A arte (painéis com a Eve e o Barut, cabeçalho, ícone) vem pronta de
; instalador/gerar_arte.py. Instala só para o usuário
; (%LOCALAPPDATA%\Programs\WolfLeague): não pede senha de administrador, e o
; jogo se atualiza rodando o instalador novo por cima. Silencioso:
; WolfLeague-Setup-x.exe /S  (e /D=pasta, por último).
; O que o jogador salva (conta, ajustes) fica em %APPDATA%\Godot\app_userdata
; e o desinstalador não mexe nisso.

Unicode true
!include "MUI2.nsh"

!ifndef VERSAO
  !error "passe -DVERSAO=x.y.z"
!endif
!ifndef VERSAO_NUM
  !error "passe -DVERSAO_NUM=x.y.z.0 (quatro números)"
!endif
!ifndef ORIGEM
  !error "passe -DORIGEM=<pasta do jogo exportado>"
!endif
!ifndef LISTA
  !error "passe -DLISTA=<saída de gerar_lista.py>"
!endif
!ifndef SAIDA
  !define SAIDA "WolfLeague-Setup.exe"
!endif

!define NOME      "WolfLeague"
!define EXE       "WolfLeague.exe"
!define CHAVE_DES "Software\Microsoft\Windows\CurrentVersion\Uninstall\${NOME}"

; As cores do menu do jogo (scripts/ui/kit/UiTokens.cs, paleta Menu), RRGGBB.
!define COR_FUNDO  "0A1119"   ; MenuBg, azul-noite
!define COR_CAMPO  "16212F"   ; MenuSurface2
!define COR_TEXTO  "EDF2F7"   ; MenuTextPrimary
!define COR_TEXTO2 "9DB0C4"   ; MenuTextSecondary
!define COR_OURO   "F3B950"   ; MenuAccent
; as mesmas, como COLORREF (BBGGRR) para as mensagens da barra de progresso
!define BGR_FUNDO  0x19110A
!define BGR_OURO   0x50B9F3

Name "${NOME}"
OutFile "${SAIDA}"
InstallDir "$LOCALAPPDATA\Programs\${NOME}"
InstallDirRegKey HKCU "Software\${NOME}" "Pasta"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
BrandingText "${NOME} ${VERSAO}"
InstallButtonText "Instalar"
ShowInstDetails nevershow
ShowUninstDetails nevershow

VIProductVersion "${VERSAO_NUM}"
VIAddVersionKey "ProductName" "${NOME}"
VIAddVersionKey "CompanyName" "${NOME}"
VIAddVersionKey "FileDescription" "Instalador do ${NOME}"
VIAddVersionKey "FileVersion" "${VERSAO}"
VIAddVersionKey "ProductVersion" "${VERSAO}"
VIAddVersionKey "LegalCopyright" "${NOME}"

; ---- aparência -----------------------------------------------------------
!define MUI_ICON   "${__FILEDIR__}\WolfLeague.ico"
!define MUI_UNICON "${__FILEDIR__}\WolfLeague.ico"
!define MUI_BGCOLOR   "${COR_FUNDO}"
!define MUI_TEXTCOLOR "${COR_TEXTO}"
!define MUI_ABORTWARNING
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_RIGHT
!define MUI_HEADERIMAGE_BITMAP "${__FILEDIR__}\cabecalho.bmp"
!define MUI_UNHEADERIMAGE_BITMAP "${__FILEDIR__}\cabecalho.bmp"

; Pinta o que o MUI não pinta sozinho: a janela de fora, a página de dentro,
; os textos, o campo da pasta e a barra de progresso. Roda no "Show" de cada
; página; o prefixo ${P} é "" no instalador e "un." no desinstalador.
!macro Estilo P
  Function ${P}EstiloJanela
    SetCtlColors $HWNDPARENT "" "${COR_FUNDO}"
    GetDlgItem $0 $HWNDPARENT 1028            ; texto "WolfLeague x.y.z" embaixo
    SetCtlColors $0 "${COR_TEXTO2}" "${COR_FUNDO}"
    GetDlgItem $0 $HWNDPARENT 1256            ; texto de marca (NSIS 3)
    SetCtlColors $0 "${COR_TEXTO2}" "${COR_FUNDO}"
    GetDlgItem $0 $HWNDPARENT 1035            ; filete acima dos botões
    ShowWindow $0 ${SW_HIDE}
  FunctionEnd

  Function ${P}EstiloMeio
    Call ${P}EstiloJanela
    FindWindow $R0 "#32770" "" $HWNDPARENT
    SetCtlColors $R0 "" "${COR_FUNDO}"
    ; o texto e os campos de qualquer página de meio (ids do NSIS)
    StrCpy $R1 1000
    ${P}laco:
      GetDlgItem $0 $R0 $R1
      ${If} $0 <> 0
        SetCtlColors $0 "${COR_TEXTO}" "${COR_FUNDO}"
      ${EndIf}
      IntOp $R1 $R1 + 1
    StrCmp $R1 1040 0 ${P}laco
    ; o campo da pasta
    GetDlgItem $0 $R0 1019
    SetCtlColors $0 "${COR_TEXTO}" "${COR_CAMPO}"
    ; a barra de progresso, em ouro (o tema do Windows ignora a cor: tira o tema)
    GetDlgItem $0 $R0 1004
    ${If} $0 <> 0
      System::Call 'uxtheme::SetWindowTheme(p $0, w " ", w " ")'
      SendMessage $0 0x2001 0 ${BGR_FUNDO}    ; PBM/CCM_SETBKCOLOR
      SendMessage $0 0x0409 0 ${BGR_OURO}     ; PBM_SETBARCOLOR
    ${EndIf}
  FunctionEnd
!macroend
!insertmacro Estilo ""
!insertmacro Estilo "un."

; ---- páginas -------------------------------------------------------------
; Boas-vindas: a Eve, que é a capa do jogo.
!define MUI_WELCOMEFINISHPAGE_BITMAP "${__FILEDIR__}\lateral_inicio.bmp"
!define MUI_WELCOMEPAGE_TITLE "Bem-vindo ao WolfLeague"
!define MUI_WELCOMEPAGE_TEXT "Este assistente instala o WolfLeague, o FPS online de humanos contra lobos, no seu computador.$\r$\n$\r$\nPara entrar nas salas é preciso estar conectado à internet.$\r$\n$\r$\nClique em Avançar para continuar."
!define MUI_PAGE_CUSTOMFUNCTION_SHOW EstiloJanela
!insertmacro MUI_PAGE_WELCOME

!define MUI_PAGE_HEADER_TEXT "Onde instalar"
!define MUI_PAGE_HEADER_SUBTEXT "Escolha a pasta do WolfLeague."
!define MUI_PAGE_CUSTOMFUNCTION_SHOW EstiloMeio
!insertmacro MUI_PAGE_DIRECTORY

!define MUI_PAGE_HEADER_TEXT "Instalando"
!define MUI_PAGE_HEADER_SUBTEXT "Copiando os arquivos do jogo."
!define MUI_PAGE_CUSTOMFUNCTION_SHOW EstiloMeio
!insertmacro MUI_PAGE_INSTFILES

; Fim: o Barut e o lobo.
!undef MUI_WELCOMEFINISHPAGE_BITMAP
!define MUI_WELCOMEFINISHPAGE_BITMAP "${__FILEDIR__}\lateral_fim.bmp"
!define MUI_FINISHPAGE_TITLE "WolfLeague instalado"
!define MUI_FINISHPAGE_TEXT "Tudo pronto. Os atalhos estão na Área de Trabalho e no Menu Iniciar.$\r$\n$\r$\nBoa partida!"
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Jogar agora"
!define MUI_PAGE_CUSTOMFUNCTION_SHOW EstiloJanela
!insertmacro MUI_PAGE_FINISH

!define MUI_PAGE_HEADER_TEXT "Desinstalar o WolfLeague"
!define MUI_PAGE_HEADER_SUBTEXT "Remove o jogo deste computador."
!define MUI_PAGE_CUSTOMFUNCTION_SHOW un.EstiloMeio
!insertmacro MUI_UNPAGE_CONFIRM
!define MUI_PAGE_CUSTOMFUNCTION_SHOW un.EstiloMeio
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "PortugueseBR"

; ---- instalação ----------------------------------------------------------
Section "Jogo"
  SetOutPath "$INSTDIR"

  ; Versão antiga: a pasta de dados do .NET pode ter DLLs que a nova não tem.
  ; Só apagamos a pasta que o próprio jogo cria, nunca o $INSTDIR inteiro.
  RMDir /r "$INSTDIR\data_WolfLeague_windows_x86_64"

  File /r "${ORIGEM}\*.*"
  File "${__FILEDIR__}\WolfLeague.ico"

  WriteUninstaller "$INSTDIR\Desinstalar.exe"

  CreateShortcut "$SMPROGRAMS\${NOME}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\WolfLeague.ico"
  CreateShortcut "$DESKTOP\${NOME}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\WolfLeague.ico"

  WriteRegStr HKCU "Software\${NOME}" "Pasta" "$INSTDIR"
  WriteRegStr HKCU "${CHAVE_DES}" "DisplayName" "${NOME}"
  WriteRegStr HKCU "${CHAVE_DES}" "DisplayVersion" "${VERSAO}"
  WriteRegStr HKCU "${CHAVE_DES}" "Publisher" "${NOME}"
  WriteRegStr HKCU "${CHAVE_DES}" "DisplayIcon" "$INSTDIR\WolfLeague.ico"
  WriteRegStr HKCU "${CHAVE_DES}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${CHAVE_DES}" "UninstallString" '"$INSTDIR\Desinstalar.exe"'
  WriteRegStr HKCU "${CHAVE_DES}" "QuietUninstallString" '"$INSTDIR\Desinstalar.exe" /S'
  WriteRegDWORD HKCU "${CHAVE_DES}" "NoModify" 1
  WriteRegDWORD HKCU "${CHAVE_DES}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  ; a lista vem do mesmo diretório que o instalador copiou
  !include "${LISTA}"
  Delete "$INSTDIR\WolfLeague.ico"
  Delete "$INSTDIR\Desinstalar.exe"
  RMDir "$INSTDIR"   ; só some se ficou vazia

  Delete "$SMPROGRAMS\${NOME}.lnk"
  Delete "$DESKTOP\${NOME}.lnk"
  DeleteRegKey HKCU "${CHAVE_DES}"
  DeleteRegKey HKCU "Software\${NOME}"
SectionEnd
