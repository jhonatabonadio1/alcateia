; Instalador do cliente do WolfLeague para Windows (NSIS).
;
; O cliente (cliente/Alcateia.Cliente, o WolfLeagueLauncher.exe) é quem baixa
; o jogo, mantém atualizado e abre: este instalador põe só ele no computador
; (uns 40 MB), com os atalhos e o desinstalador. O jogo vem depois, pelo
; cliente. É o que substitui o instalador do jogo inteiro (windows.nsi).
;
; É o pipeline (.github/workflows/lancamento.yml, job "lancador") quem roda
; isto, depois de publicar o cliente. À mão, com `makensis`:
;
;   dotnet publish cliente/Alcateia.Cliente -c Release -r win-x64 --self-contained \
;          -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
;          -p:PublishTrimmed=true -p:TrimMode=partial -p:DebugType=none \
;          -p:Version=0.5.0 -o build/lancador/windows
;   makensis -DVERSAO=0.5.0 -DVERSAO_NUM=0.5.0.0 \
;            -DORIGEM="$PWD/build/lancador/windows" \
;            -DSAIDA="$PWD/build/WolfLeague-Launcher-Setup.exe" "$PWD/instalador/cliente.nsi"
;
; Instala só para o usuário (%LOCALAPPDATA%\Programs\WolfLeague Launcher): não
; pede senha de administrador, e o cliente se atualiza sozinho nessa pasta.
; Silencioso: WolfLeague-Launcher-Setup.exe /S
;
; **Quem já tinha o jogo pelo instalador antigo** (%LOCALAPPDATA%\Programs\WolfLeague)
; não baixa tudo de novo: os arquivos ficam onde estão, o cliente os acha (a
; pasta está no registro, HKCU\Software\WolfLeague\InstallPath) e baixa só a
; diferença. Daqui sai só o que era do instalador antigo: o desinstalador
; dele e a entrada dele em "Aplicativos", para o jogo não aparecer duas vezes.
;
; O que o jogador salva (conta, ajustes) fica em %APPDATA% e o desinstalador
; não mexe. Os arquivos do jogo ele apaga se o jogador quiser (pergunta), pelo
; próprio cliente: WolfLeagueLauncher.exe --desinstalar-jogo.

Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"

!ifndef VERSAO
  !error "passe -DVERSAO=x.y.z"
!endif
!ifndef VERSAO_NUM
  !error "passe -DVERSAO_NUM=x.y.z.0 (quatro números)"
!endif
!ifndef ORIGEM
  !error "passe -DORIGEM=<pasta com o WolfLeagueLauncher.exe publicado>"
!endif
!ifndef SAIDA
  !define SAIDA "WolfLeague-Launcher-Setup.exe"
!endif

!define NOME      "WolfLeague"
!define EXE       "WolfLeagueLauncher.exe"
!define CHAVE_DES "Software\Microsoft\Windows\CurrentVersion\Uninstall\WolfLeagueLauncher"
; O instalador de antes (windows.nsi): a entrada dele em "Aplicativos" e a pasta do jogo.
!define CHAVE_DES_JOGO "Software\Microsoft\Windows\CurrentVersion\Uninstall\${NOME}"
!define CHAVE_RUN "Software\Microsoft\Windows\CurrentVersion\Run"
!define MEMORIA_GB 8
!define MEMORIA_MB 7000

; As cores do menu do jogo (src/scripts/ui/kit/UiTokens.cs, paleta Menu), RRGGBB.
!define COR_FUNDO  "0A1119"
!define COR_CAMPO  "16212F"
!define COR_TEXTO  "EDF2F7"
!define COR_TEXTO2 "9DB0C4"
!define COR_OURO   "F3B950"
!define BGR_FUNDO  0x19110A
!define BGR_OURO   0x50B9F3

Name "${NOME}"
OutFile "${SAIDA}"
InstallDir "$LOCALAPPDATA\Programs\${NOME} Launcher"
InstallDirRegKey HKCU "Software\${NOME} Launcher" "InstallPath"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "${NOME} ${VERSAO}"
InstallButtonText "Install"
ShowInstDetails nevershow
ShowUninstDetails nevershow

VIProductVersion "${VERSAO_NUM}"
VIAddVersionKey "ProductName" "${NOME}"
VIAddVersionKey "CompanyName" "${NOME}"
VIAddVersionKey "FileDescription" "${NOME} Setup"
VIAddVersionKey "FileVersion" "${VERSAO}"
VIAddVersionKey "ProductVersion" "${VERSAO}"
VIAddVersionKey "LegalCopyright" "${NOME}"

; ---- requisitos mínimos (os do jogo: é para ele que o cliente serve) -------
Function .onInit
  ${IfNot} ${IsNativeAMD64}
  ${AndIfNot} ${IsNativeARM64}
    MessageBox MB_ICONSTOP|MB_OK "${NOME} requires 64-bit Windows, and this Windows is 32-bit." /SD IDOK
    Abort
  ${EndIf}
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_ICONSTOP|MB_OK "${NOME} requires Windows 10 or Windows 11." /SD IDOK
    Abort
  ${EndIf}
  ${If} ${IsNativeARM64}
    MessageBox MB_ICONEXCLAMATION|MB_YESNO "This Windows is for ARM processors. ${NOME} runs on it through emulation and is much slower than on a PC with an Intel or AMD processor.$\r$\n$\r$\nInstall anyway?" /SD IDYES IDYES +2
    Abort
  ${EndIf}

  System::Call "*(i 64, i 0, l 0, l 0, l 0, l 0, l 0, l 0, l 0) p .r0"
  System::Call "kernel32::GlobalMemoryStatusEx(p r0) i .r1"
  System::Call "*$0(i, i, l .r2)"
  System::Free $0
  ${If} $1 <> 0
    System::Int64Op $2 / 1048576
    Pop $2
    ${If} $2 < ${MEMORIA_MB}
      IntOp $3 $2 + 512
      IntOp $3 $3 / 1024
      MessageBox MB_ICONEXCLAMATION|MB_YESNO "This computer has $3 GB of memory, and ${NOME} needs ${MEMORIA_GB} GB. The game may run slowly or close in the middle of a match.$\r$\n$\r$\nInstall anyway?" /SD IDYES IDYES +2
      Abort
    ${EndIf}
  ${EndIf}
FunctionEnd

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
!define MUI_WELCOMEFINISHPAGE_BITMAP "${__FILEDIR__}\lateral_inicio.bmp"
!define MUI_WELCOMEPAGE_TITLE "Welcome to WolfLeague"
!define MUI_WELCOMEPAGE_TEXT "This wizard installs the WolfLeague app: it downloads the game, keeps it up to date and shows your friends.$\r$\n$\r$\nThe game itself is downloaded by the app after you sign in. An internet connection is required.$\r$\n$\r$\nClick Next to continue."
!define MUI_PAGE_CUSTOMFUNCTION_SHOW EstiloJanela
!insertmacro MUI_PAGE_WELCOME

!define MUI_PAGE_HEADER_TEXT "Installing"
!define MUI_PAGE_HEADER_SUBTEXT "Copying the WolfLeague app."
!define MUI_PAGE_CUSTOMFUNCTION_SHOW EstiloMeio
!insertmacro MUI_PAGE_INSTFILES

!define MUI_FINISHPAGE_TITLE "WolfLeague installed"
!define MUI_FINISHPAGE_TEXT "All set. The shortcuts are on the Desktop and in the Start Menu.$\r$\n$\r$\nOpen WolfLeague, sign in and click Install to download the game."
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Open WolfLeague"
!define MUI_PAGE_CUSTOMFUNCTION_SHOW EstiloJanela
!insertmacro MUI_PAGE_FINISH

!define MUI_PAGE_HEADER_TEXT "Uninstall WolfLeague"
!define MUI_PAGE_HEADER_SUBTEXT "Removes the app from this computer."
!define MUI_PAGE_CUSTOMFUNCTION_SHOW un.EstiloMeio
!insertmacro MUI_UNPAGE_CONFIRM
!define MUI_PAGE_CUSTOMFUNCTION_SHOW un.EstiloMeio
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; ---- instalação ----------------------------------------------------------
; Fecha o cliente, se estiver aberto (na bandeja, por exemplo): um programa
; aberto não se sobrescreve nem se apaga.
!macro FecharCliente
  nsExec::Exec 'taskkill /IM ${EXE} /F'
  Pop $0
  Sleep 600
!macroend

Section "App"
  !insertmacro FecharCliente
  SetOutPath "$INSTDIR"

  File "${ORIGEM}\${EXE}"
  File "${__FILEDIR__}\WolfLeague.ico"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; Os atalhos têm o nome do jogo: é por eles que se joga. Os do instalador
  ; antigo (mesmo nome) passam a abrir o cliente.
  CreateShortcut "$SMPROGRAMS\${NOME}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\WolfLeague.ico"
  CreateShortcut "$DESKTOP\${NOME}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\WolfLeague.ico"

  WriteRegStr HKCU "Software\${NOME} Launcher" "InstallPath" "$INSTDIR"
  WriteRegStr HKCU "${CHAVE_DES}" "DisplayName" "${NOME}"
  WriteRegStr HKCU "${CHAVE_DES}" "DisplayVersion" "${VERSAO}"
  WriteRegStr HKCU "${CHAVE_DES}" "Publisher" "${NOME}"
  WriteRegStr HKCU "${CHAVE_DES}" "DisplayIcon" "$INSTDIR\WolfLeague.ico"
  WriteRegStr HKCU "${CHAVE_DES}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${CHAVE_DES}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${CHAVE_DES}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${CHAVE_DES}" "NoModify" 1
  WriteRegDWORD HKCU "${CHAVE_DES}" "NoRepair" 1

  ; O jogo instalado pelo instalador antigo: os arquivos ficam (o cliente os
  ; aproveita); saem o desinstalador antigo e a entrada dele em "Aplicativos".
  ReadRegStr $1 HKCU "Software\${NOME}" "InstallPath"
  ${If} $1 != ""
    Delete "$1\Uninstall.exe"
    Delete "$1\Desinstalar.exe"
  ${EndIf}
  DeleteRegKey HKCU "${CHAVE_DES_JOGO}"
SectionEnd

Section "Uninstall"
  !insertmacro FecharCliente

  ; Os arquivos do jogo: quem apaga é o cliente, que sabe o que instalou e onde.
  MessageBox MB_ICONQUESTION|MB_YESNO "Also remove the game files?$\r$\n$\r$\nYour account and settings are kept either way." /SD IDYES IDNO manter
    ExecWait '"$INSTDIR\${EXE}" --desinstalar-jogo'
  manter:

  Delete "$INSTDIR\${EXE}"
  ; O que a atualização automática do cliente deixa na pasta: o estado dela e
  ; o executável antigo que saiu da frente (cliente/Alcateia.Pacote/Installer.cs).
  RMDir /r "$INSTDIR\.pacote"
  Delete "$INSTDIR\*.antigo-*"
  Delete "$INSTDIR\*.baixando"
  Delete "$INSTDIR\WolfLeague.ico"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"   ; só some se ficou vazia

  Delete "$SMPROGRAMS\${NOME}.lnk"
  Delete "$DESKTOP\${NOME}.lnk"
  DeleteRegValue HKCU "${CHAVE_RUN}" "WolfLeagueLauncher"
  DeleteRegKey HKCU "${CHAVE_DES}"
  DeleteRegKey HKCU "Software\${NOME} Launcher"
SectionEnd
