; ================================================================
; Inno Setup Script - Expresso Delivery Print Client v2.2.0
; ================================================================
; Instalador profissional para o cliente de impressão Windows
; O aplicativo é self-contained (.NET 6) - não requer runtime
; Data: 01/02/2026
; ================================================================

#define MyAppName "Expresso Delivery Print Client"
#define MyAppVersion "2.2.0"
#define MyAppPublisher "Agencia Expresso"
#define MyAppURL "https://delivery2.agenciaexpresso.com.br"
#define MyAppExeName "ExpressoDeliveryPrintClient.exe"
#define MyAppGuid "{{8B5D6C3E-9F2A-4B1C-A7E4-5D8F2C1B9E3A}"

[Setup]
; INFORMAÇÕES BÁSICAS
AppId={#MyAppGuid}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; CAMINHOS DE INSTALAÇÃO (pasta do usuário, sem precisar de admin)
DefaultDirName={localappdata}\DeliveryPrintClient
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; SAÍDA DO INSTALADOR
OutputDir=output
OutputBaseFilename=ExpressoDeliveryPrintClient-Setup-v{#MyAppVersion}
SetupIconFile=..\app-icon.ico
Compression=lzma2/max
SolidCompression=yes

; PRIVILÉGIOS (não precisa de admin - instala na pasta do usuário)
PrivilegesRequired=lowest

; INTERFACE
WizardStyle=modern
DisableWelcomePage=no

; OPÇÕES
AllowNoIcons=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

; ARQUITETURA
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; VERSÃO MÍNIMA DO WINDOWS
MinVersion=10.0.17763

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Area de Trabalho"; GroupDescription: "Atalhos:"
Name: "startmenu"; Description: "Criar atalho no Menu Iniciar"; GroupDescription: "Atalhos:"
Name: "autostart"; Description: "Iniciar automaticamente com Windows"; GroupDescription: "Inicializacao:"; Flags: checkedonce

[Files]
; EXECUTÁVEL PRINCIPAL (single-file, self-contained)
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; ÍCONE DA APLICAÇÃO
Source: "..\app-icon.ico"; DestDir: "{app}"; Flags: ignoreversion

; LOGOS PARA O HEADER DA APLICAÇÃO
Source: "..\logoparaapp\*"; DestDir: "{app}\logoparaapp"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; DOCUMENTAÇÃO
Source: "..\LEIA-ME.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
; ATALHO DESKTOP (com ícone personalizado)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app-icon.ico"; Tasks: desktopicon

; ATALHO MENU INICIAR
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app-icon.ico"; Tasks: startmenu

; ATALHO DESINSTALAR
Name: "{autoprograms}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"

[Registry]
; AUTO-START (se tarefa selecionada)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Delivery Print Client"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
; EXECUTAR APLICATIVO APÓS INSTALAÇÃO (OPCIONAL)
Filename: "{app}\{#MyAppExeName}"; Description: "Executar {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; PARAR PROCESSO ANTES DE DESINSTALAR
Filename: "taskkill"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "StopApp"

[UninstallDelete]
; REMOVER DADOS DO USUÁRIO (CONFIGS E LOGS)
Type: filesandordirs; Name: "{userappdata}\DeliveryPrintClient"

[InstallDelete]
; LIMPAR VERSÕES ANTIGAS ANTES DE INSTALAR
Type: filesandordirs; Name: "{app}\*"

[Code]
// ================================================================
// FUNÇÕES PASCAL SCRIPT
// ================================================================

function InitializeSetup(): Boolean;
begin
  Result := True;

  if not IsWin64 then
  begin
    MsgBox(
      'Este aplicativo requer Windows 10 ou 11 de 64 bits.' + #13#10 +
      'Seu sistema operacional nao e compativel.',
      mbCriticalError,
      MB_OK
    );
    Result := False;
    Exit;
  end;

  Log('Sistema operacional compativel detectado.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ConfigDir: String;
begin
  if CurStep = ssPostInstall then
  begin
    Log('Instalacao concluida com sucesso.');

    // Garantir que o diretório de config existe
    ConfigDir := ExpandConstant('{userappdata}\DeliveryPrintClient');
    if not DirExists(ConfigDir) then
    begin
      CreateDir(ConfigDir);
      Log('Diretorio de configuracao criado: ' + ConfigDir);
    end;
  end;
end;

[Messages]
WelcomeLabel1=Bem-vindo ao Instalador do {#MyAppName}
WelcomeLabel2=Este assistente instalara o {#MyAppName} v{#MyAppVersion} no seu computador.%n%nO instalador ira:%n  - Copiar o aplicativo para a pasta do usuario%n  - Criar atalhos na area de trabalho e menu iniciar%n  - Configurar inicio automatico (opcional)%n%nRecomenda-se fechar todos os programas antes de continuar.
ClickNext=Clique em Avancar para continuar.
ClickInstall=Clique em Instalar para iniciar a instalacao.
FinishedLabel=A instalacao do {#MyAppName} foi concluida com sucesso!%n%nO aplicativo esta pronto para uso. Configure a URL da API e credenciais no primeiro acesso.
ClickFinish=Clique em Concluir para sair do instalador.
