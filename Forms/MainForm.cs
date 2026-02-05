using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DeliveryPrintClient.Models;
using DeliveryPrintClient.Services;

namespace DeliveryPrintClient.Forms
{
    public partial class MainForm : Form
    {
        private readonly AppConfig _config;
        private readonly ApiService _apiService;
        private HealthCheckService _healthCheckService;
        private readonly UpdateService _updateService;
        private PrinterService? _printerService;
        private System.Threading.Timer? _pollingTimer;
        private System.Threading.Timer? _updateCheckTimer;
        private System.Threading.Timer? _remoteLogTimer;
        private readonly SemaphoreSlim _processingSemaphore = new SemaphoreSlim(1, 1);

        // v2.0.0: Contador de jobs processados
        private int _jobsProcessed = 0;

        // v2.3.0: Versao recusada pelo usuario (evita perguntar de novo)
        private string? _updateDeclinedVersion = null;

        // v2.0.0: P/Invoke para liberar HICON handles
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private NotifyIcon? _trayIcon;
        private ContextMenuStrip? _trayMenu;

        // Cores modernas
        private static readonly Color PrimaryColor = Color.FromArgb(0, 120, 215);
        private static readonly Color SuccessColor = Color.FromArgb(16, 185, 129);
        private static readonly Color DangerColor = Color.FromArgb(239, 68, 68);
        private static readonly Color BackgroundColor = Color.FromArgb(248, 250, 252);
        private static readonly Color CardBackground = Color.White;
        private static readonly Color TextPrimary = Color.FromArgb(30, 41, 59);
        private static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);
        private static readonly Color BorderColor = Color.FromArgb(226, 232, 240);

        public MainForm()
        {
            InitializeComponent();

            LogService.Initialize();
            LogService.LogInfo($"Inicializando Expresso Delivery Print Client v{UpdateService.CurrentVersion}");

            _config = ConfigService.Load();
            _apiService = new ApiService(_config);
            _healthCheckService = new HealthCheckService(_config.ApiUrl);
            _updateService = new UpdateService(_config, _apiService);

            InitializeTrayIcon();

            this.Shown += async (s, e) =>
            {
                try
                {
                    LoadConfiguration();
                }
                catch (Exception ex)
                {
                    LogService.LogError("Erro ao carregar configuracao", ex);
                    MessageBox.Show(
                        $"Erro ao carregar configuracao:\n\n{ex.Message}\n\nO aplicativo continuara, mas voce precisara configurar manualmente.",
                        "Aviso",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }

                // v2.1.0: Wizard de primeira execução
                if (!_config.SetupCompleted)
                {
                    ShowFirstRunWizard();
                }

                // Nota: Atalhos e auto-start são configurados pelo instalador (Inno Setup)

                // Verificar se credenciais estão configuradas antes de qualquer operação de rede
                bool hasCredentials = !string.IsNullOrEmpty(_config.ApiKey) && !string.IsNullOrEmpty(_config.SecretKey);

                if (!hasCredentials)
                {
                    LogMessage("Credenciais nao configuradas. Configure API Key e Secret Key para iniciar.");
                    LogMessage("Preencha os campos acima e clique em 'Salvar' para conectar ao servidor.");
                    UpdateStatusDisplay("Status: Aguardando Credenciais", Color.FromArgb(234, 179, 8));
                    return; // Não iniciar polling, update checker, nem log sender sem credenciais
                }

                // Verificar saúde da API
                LogMessage("Verificando saude da API...");
                var (isHealthy, healthMessage) = await _healthCheckService.CheckApiHealthAsync();

                if (!isHealthy)
                {
                    LogMessage($"API nao acessivel: {healthMessage}");
                    LogMessage("O sistema continuara tentando conectar.");

                    MessageBox.Show(
                        $"A API nao esta acessivel:\n\n{healthMessage}\n\nO aplicativo continuara rodando, mas pode nao funcionar corretamente ate que a API esteja online.",
                        "Aviso - API Indisponivel",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
                else
                {
                    LogMessage($"Saude da API: {healthMessage}");
                }

                // Autenticar com o servidor
                await AuthenticateAsync();

                // Iniciar polling apenas com credenciais válidas
                InitializePolling();

                // v2.1.0: Iniciar timers de update e logs remotos
                InitializeUpdateChecker();
                InitializeRemoteLogSender();
            };
        }

        /// <summary>
        /// v2.1.0: Wizard de primeira execução com teste de conexão
        /// </summary>
        private void ShowFirstRunWizard()
        {
            LogMessage("Primeira execucao detectada. Abrindo wizard de configuracao...");

            var result = MessageBox.Show(
                "Bem-vindo ao Expresso Delivery Print Client!\n\n" +
                "Para configurar o sistema, voce precisa de:\n\n" +
                "1. URL da API (fornecida pelo administrador)\n" +
                "2. API Key (gerada no painel admin)\n" +
                "3. Secret Key (gerada no painel admin)\n\n" +
                "Essas credenciais estao disponiveis no painel administrativo,\n" +
                "na secao Impressoras > Aba Windows.\n\n" +
                "As configuracoes serao salvas em:\n" +
                $"{ConfigService.GetConfigPath()}\n\n" +
                "Deseja configurar agora?",
                "Configuracao Inicial",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information
            );

            if (result == DialogResult.Yes)
            {
                // Focar no campo API URL
                var txtApiUrl = FindControl<TextBox>("txtApiUrl");
                txtApiUrl?.Focus();
            }

            // Marcar como setup concluído para não mostrar wizard novamente
            _config.SetupCompleted = true;
            ConfigService.Save(_config);
        }

        /// <summary>
        /// v2.1.0: Timer de verificação de atualização (a cada 30 min)
        /// </summary>
        private void InitializeUpdateChecker()
        {
            _updateCheckTimer?.Dispose();
            _updateCheckTimer = new System.Threading.Timer(
                async _ => await CheckForUpdateAsync(),
                null,
                TimeSpan.FromMinutes(2), // Primeira verificação após 2 min
                TimeSpan.FromMinutes(30)  // Depois a cada 30 min
            );
        }

        /// <summary>
        /// v2.1.0: Timer de envio de logs remotos (a cada 5 min)
        /// </summary>
        private void InitializeRemoteLogSender()
        {
            _remoteLogTimer?.Dispose();
            _remoteLogTimer = new System.Threading.Timer(
                async _ => await SendRemoteLogsAsync(),
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5)
            );
        }

        /// <summary>
        /// v2.1.0: Verifica atualizacao no servidor
        /// v2.3.0: Suporte a auto-download e declined version
        /// </summary>
        private async Task CheckForUpdateAsync()
        {
            try
            {
                var updateInfo = await _updateService.CheckForUpdateAsync();

                if (updateInfo != null && updateInfo.UpdateAvailable)
                {
                    // v2.3.0: Nao perguntar de novo se usuario ja recusou esta versao
                    if (updateInfo.LatestVersion == _updateDeclinedVersion && !updateInfo.ForceUpdate)
                        return;

                    ShowUpdateNotification(updateInfo);
                }
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"Erro no check de atualizacao: {ex.Message}");
            }
        }

        /// <summary>
        /// v2.3.0: Mostra dialog de atualizacao com opcao de auto-download
        /// </summary>
        private void ShowUpdateNotification(UpdateCheckResponse updateInfo)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowUpdateNotification(updateInfo)));
                return;
            }

            _trayIcon?.ShowBalloonTip(
                10000,
                "Atualizacao Disponivel",
                $"Nova versao v{updateInfo.LatestVersion} disponivel.\n{updateInfo.ReleaseNotes}",
                ToolTipIcon.Info
            );

            LogMessage($"Atualizacao disponivel: v{updateInfo.LatestVersion} - {updateInfo.ReleaseNotes}");

            string downloadUrl = updateInfo.DownloadUrl;
            if (!downloadUrl.StartsWith("http"))
            {
                downloadUrl = $"{_config.ApiUrl}{downloadUrl}";
            }

            // Force update: nao da opcao de recusar
            if (updateInfo.ForceUpdate)
            {
                MessageBox.Show(
                    $"Uma atualizacao obrigatoria sera instalada.\n\n" +
                    $"Versao atual: v{UpdateService.CurrentVersion}\n" +
                    $"Nova versao: v{updateInfo.LatestVersion}\n\n" +
                    $"{updateInfo.ReleaseNotes}",
                    "Atualizacao Obrigatoria",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                _ = PerformAutoUpdateAsync(updateInfo, downloadUrl);
                return;
            }

            var result = MessageBox.Show(
                $"Nova versao disponivel!\n\n" +
                $"Versao atual: v{UpdateService.CurrentVersion}\n" +
                $"Nova versao: v{updateInfo.LatestVersion}\n\n" +
                (!string.IsNullOrEmpty(updateInfo.ReleaseNotes) ? $"{updateInfo.ReleaseNotes}\n\n" : "") +
                "Deseja atualizar automaticamente agora?\n\n" +
                "(O aplicativo sera encerrado durante a atualizacao\n" +
                "e reiniciado automaticamente.)",
                "Atualizacao Disponivel",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                _ = PerformAutoUpdateAsync(updateInfo, downloadUrl);
            }
            else
            {
                _updateDeclinedVersion = updateInfo.LatestVersion;
                LogMessage($"Atualizacao v{updateInfo.LatestVersion} recusada pelo usuario");
            }
        }

        /// <summary>
        /// v2.3.0: Baixa atualizacao com barra de progresso e aplica
        /// </summary>
        private async Task PerformAutoUpdateAsync(UpdateCheckResponse updateInfo, string downloadUrl)
        {
            Form? progressForm = null;
            ProgressBar? progressBar = null;
            Label? lblProgress = null;
            Label? lblBytes = null;

            try
            {
                progressForm = new Form
                {
                    Text = "Atualizando...",
                    Size = new Size(450, 180),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    ControlBox = false,
                    BackColor = Color.White
                };

                lblProgress = new Label
                {
                    Text = "Baixando atualizacao...",
                    Location = new Point(20, 20),
                    Size = new Size(400, 30),
                    Font = new Font("Segoe UI", 10F)
                };

                progressBar = new ProgressBar
                {
                    Location = new Point(20, 60),
                    Size = new Size(400, 30),
                    Style = ProgressBarStyle.Continuous,
                    Minimum = 0,
                    Maximum = 100
                };

                lblBytes = new Label
                {
                    Text = "",
                    Location = new Point(20, 100),
                    Size = new Size(400, 25),
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = Color.Gray
                };

                progressForm.Controls.Add(lblProgress);
                progressForm.Controls.Add(progressBar);
                progressForm.Controls.Add(lblBytes);

                progressForm.Show(this);
                this.Enabled = false;

                var lblProgressRef = lblProgress;
                var progressBarRef = progressBar;
                var lblBytesRef = lblBytes;
                var formRef = progressForm;

                var progress = new Progress<(long received, long total)>(p =>
                {
                    if (formRef.IsDisposed) return;

                    if (p.total > 0)
                    {
                        int percent = (int)((p.received * 100) / p.total);
                        progressBarRef.Value = Math.Min(percent, 100);
                        lblProgressRef.Text = $"Baixando atualizacao... {percent}%";
                        lblBytesRef.Text = $"{p.received / 1024 / 1024}MB de {p.total / 1024 / 1024}MB";
                    }
                    else
                    {
                        progressBarRef.Style = ProgressBarStyle.Marquee;
                        lblProgressRef.Text = $"Baixando... ({p.received / 1024 / 1024}MB)";
                    }
                });

                LogMessage("Iniciando download da atualizacao...");

                string? downloadedPath = await _updateService.DownloadUpdateAsync(
                    downloadUrl,
                    updateInfo.LatestVersion,
                    progress
                );

                if (downloadedPath == null)
                    throw new Exception("Download retornou caminho nulo");

                LogMessage($"Download concluido: {downloadedPath}");
                lblProgress.Text = "Aplicando atualizacao...";
                progressBar.Value = 100;

                await Task.Delay(500);

                progressForm.Close();
                progressForm.Dispose();
                progressForm = null;

                UpdateService.ApplyUpdate(downloadedPath);
            }
            catch (Exception ex)
            {
                this.Enabled = true;
                if (progressForm != null && !progressForm.IsDisposed)
                {
                    progressForm.Close();
                    progressForm.Dispose();
                }

                LogMessage($"Erro na atualizacao automatica: {ex.Message}");
                LogService.LogError("Erro na atualizacao automatica", ex);

                MessageBox.Show(
                    $"Erro ao atualizar:\n\n{ex.Message}\n\n" +
                    "Voce pode baixar manualmente no painel administrativo.",
                    "Erro na Atualizacao",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// v2.1.0: Envia logs e stats para o servidor
        /// </summary>
        private async Task SendRemoteLogsAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_config.ApiKey) || string.IsNullOrEmpty(_config.SecretKey))
                    return;

                var logs = LogService.FlushRemoteBuffer();
                var (jobsProcessados, jobsErro, uptimeMinutes) = LogService.GetStats();

                // Enviar apenas se há logs ou periodicamente para stats
                if (logs.Count > 0 || uptimeMinutes % 15 == 0)
                {
                    await _apiService.SendLogsAsync(logs, jobsProcessados, jobsErro, uptimeMinutes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao enviar logs remotos: {ex.Message}");
            }
        }

        private void InitializeTrayIcon()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Abrir", null, OnTrayOpen);
            _trayMenu.Items.Add("Configuracoes", null, OnTrayConfig);
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add("Sair", null, OnTrayExit);

            _trayIcon = new NotifyIcon
            {
                Text = "Expresso Delivery Print Client",
                Visible = true,
                ContextMenuStrip = _trayMenu
            };

            try
            {
                // v2.1.0: Carregar ícone do recurso embarcado (.ico)
                var trayIconFromResource = InstallerService.LoadIconFromResource(16, 16);
                if (trayIconFromResource != null)
                {
                    _trayIcon.Icon = trayIconFromResource;
                }
                else
                {
                    // Fallback: tentar PNG da pasta logoparaapp
                    string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logoparaapp", "logo-v2.png");
                    if (File.Exists(logoPath))
                    {
                        using (var logo = new Bitmap(logoPath))
                        {
                            using (var iconBitmap = new Bitmap(16, 16))
                            {
                                using (Graphics g = Graphics.FromImage(iconBitmap))
                                {
                                    g.SmoothingMode = SmoothingMode.HighQuality;
                                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                    g.DrawImage(logo, 0, 0, 16, 16);
                                }
                                IntPtr hIcon = iconBitmap.GetHicon();
                                _trayIcon.Icon = (Icon)Icon.FromHandle(hIcon).Clone();
                                DestroyIcon(hIcon);
                            }
                        }
                    }
                    else
                    {
                        CreateFallbackIcon();
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"Erro ao carregar icone do tray: {ex.Message}");
                CreateFallbackIcon();
            }

            _trayIcon.DoubleClick += (s, e) => OnTrayOpen(s, e);
        }

        private void CreateFallbackIcon()
        {
            using (var icon = new Bitmap(16, 16))
            {
                using (Graphics g = Graphics.FromImage(icon))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var brush = new SolidBrush(SuccessColor))
                        g.FillEllipse(brush, 1, 1, 14, 14);
                }
                IntPtr hIcon = icon.GetHicon();
                _trayIcon!.Icon = (Icon)Icon.FromHandle(hIcon).Clone();
                DestroyIcon(hIcon);
            }
        }

        private void InitializeComponent()
        {
            this.Text = $"Expresso Delivery Print Client v{UpdateService.CurrentVersion}";
            this.Size = new Size(700, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.BackColor = BackgroundColor;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // v2.1.0: Carregar ícone da aplicação do recurso embarcado
            try
            {
                var appIcon = InstallerService.LoadIconFromResource();
                if (appIcon != null)
                {
                    this.Icon = appIcon;
                }
            }
            catch
            {
                // Non-critical - usará ícone padrão do Windows
            }

            // LOGO NO TOPO
            var pnlHeader = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(700, 100),
                BackColor = CardBackground
            };

            pnlHeader.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(BorderColor, 1), 0, 99, 700, 99);
            };

            PictureBox picLogo = new PictureBox
            {
                Location = new Point(20, 15),
                Size = new Size(200, 70),
                SizeMode = PictureBoxSizeMode.Zoom
            };

            try
            {
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logoparaapp", "logo-fundo-branco.png");
                if (File.Exists(logoPath))
                {
                    picLogo.Image = Image.FromFile(logoPath);
                }
                else
                {
                    picLogo.Visible = false;
                    var lblLogoFallback = new Label
                    {
                        Text = "Delivery Print Client",
                        Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                        ForeColor = PrimaryColor,
                        Location = new Point(20, 30),
                        Size = new Size(400, 40),
                        AutoSize = false
                    };
                    pnlHeader.Controls.Add(lblLogoFallback);
                }
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"Erro ao carregar logo: {ex.Message}");
                picLogo.Visible = false;
            }

            var lblVersion = new Label
            {
                Text = $"v{UpdateService.CurrentVersion}",
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = TextSecondary,
                Location = new Point(240, 75),
                AutoSize = true
            };

            pnlHeader.Controls.Add(picLogo);
            pnlHeader.Controls.Add(lblVersion);

            // CARD - Configurações
            var pnlConfig = CreateCard("Configuracoes", new Point(20, 110), new Size(660, 280));

            int yPos = 40;

            AddFormField(pnlConfig, "URL da API:", "txtApiUrl", new Point(20, yPos), new Size(300, 25), isTextBox: true);
            yPos += 35;

            AddFormField(pnlConfig, "API Key:", "txtApiKey", new Point(20, yPos), new Size(300, 25), isTextBox: true, placeholder: "WPC_...");
            AddFormField(pnlConfig, "Impressora:", "cbPrinter", new Point(350, yPos), new Size(290, 25), isComboBox: true);
            yPos += 35;

            AddFormField(pnlConfig, "Secret Key:", "txtSecretKey", new Point(20, yPos), new Size(300, 25), isTextBox: true, isPassword: true, placeholder: "");
            AddFormField(pnlConfig, "Intervalo (ms):", "numInterval", new Point(350, yPos), new Size(140, 25), isNumeric: true, min: 1000, max: 60000, increment: 1000);
            yPos += 35;

            AddFormField(pnlConfig, "Local:", "txtLocal", new Point(20, yPos), new Size(300, 25), isTextBox: true, readOnly: true, placeholder: "Preenchido apos auth");
            AddFormField(pnlConfig, "Copias Padrao:", "numCopiasPadrao", new Point(350, yPos), new Size(140, 25), isNumeric: true, min: 1, max: 10);
            yPos += 35;

            AddFormField(pnlConfig, "Delay Copias (ms):", "numDelayCopias", new Point(20, yPos), new Size(140, 25), isNumeric: true, min: 0, max: 5000, increment: 100);
            yPos += 40;

            var chkAutoStart = new CheckBox
            {
                Name = "chkAutoStart",
                Text = "Iniciar automaticamente com Windows",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = TextPrimary,
                Location = new Point(20, yPos),
                Size = new Size(320, 25)
            };
            pnlConfig.Controls.Add(chkAutoStart);

            var btnSave = CreateModernButton("Salvar", new Point(360, yPos), new Size(140, 38), PrimaryColor);
            btnSave.Click += BtnSave_Click;

            var btnTest = CreateModernButton("Testar", new Point(510, yPos), new Size(130, 38), SuccessColor);
            btnTest.Click += BtnTest_Click;

            pnlConfig.Controls.Add(btnSave);
            pnlConfig.Controls.Add(btnTest);

            // CARD - Status
            var pnlStatus = CreateCard("Status do Sistema", new Point(20, 400), new Size(660, 120));

            var lblStatus = new Label
            {
                Name = "lblStatus",
                Text = "Status: Iniciando...",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = TextPrimary,
                Location = new Point(20, 40),
                Size = new Size(620, 25)
            };

            var lblLastCheck = new Label
            {
                Name = "lblLastCheck",
                Text = "Ultima verificacao: -",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = TextSecondary,
                Location = new Point(20, 70),
                Size = new Size(300, 25)
            };

            var lblJobsProcessed = new Label
            {
                Name = "lblJobsProcessed",
                Text = "Jobs processados: 0",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = TextSecondary,
                Location = new Point(330, 70),
                Size = new Size(310, 25)
            };

            var btnOpenLogs = CreateModernButton("Abrir Logs", new Point(20, 95), new Size(140, 25), Color.FromArgb(99, 102, 241));
            btnOpenLogs.Click += BtnOpenLogs_Click;

            var btnDiagnostics = CreateModernButton("Diagnosticar", new Point(170, 95), new Size(150, 25), Color.FromArgb(139, 92, 246));
            btnDiagnostics.Click += BtnDiagnostics_Click;

            pnlStatus.Controls.AddRange(new Control[] { lblStatus, lblLastCheck, lblJobsProcessed, btnOpenLogs, btnDiagnostics });

            // CARD - Log
            var pnlLog = CreateCard("Log de Atividades", new Point(20, 530), new Size(660, 150));

            var txtLog = new TextBox
            {
                Name = "txtLog",
                Location = new Point(15, 40),
                Size = new Size(630, 95),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 9F),
                BorderStyle = BorderStyle.None
            };

            pnlLog.Controls.Add(txtLog);

            this.Controls.AddRange(new Control[] { pnlHeader, pnlConfig, pnlStatus, pnlLog });

            this.FormClosing += MainForm_FormClosing;
        }

        private Panel CreateCard(string title, Point location, Size size)
        {
            var panel = new Panel
            {
                Location = location,
                Size = size,
                BackColor = CardBackground
            };

            panel.Paint += (s, e) =>
            {
                var rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                using (var pen = new Pen(BorderColor, 1))
                {
                    e.Graphics.DrawRectangle(pen, rect);
                }
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(15, 10),
                AutoSize = true
            };

            panel.Controls.Add(lblTitle);

            return panel;
        }

        private Button CreateModernButton(string text, Point location, Size size, Color bgColor)
        {
            var btn = new Button
            {
                Text = text,
                Location = location,
                Size = size,
                BackColor = bgColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };

            btn.FlatAppearance.BorderSize = 0;

            btn.MouseEnter += (s, e) =>
            {
                btn.BackColor = ControlPaint.Light(bgColor, 0.1f);
            };

            btn.MouseLeave += (s, e) =>
            {
                btn.BackColor = bgColor;
            };

            return btn;
        }

        private T? FindControl<T>(string name) where T : Control
        {
            try
            {
                var controls = this.Controls.Find(name, true);
                if (controls.Length > 0 && controls[0] is T control)
                {
                    return control;
                }
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"Erro ao buscar controle '{name}': {ex.Message}");
            }
            return null;
        }

        private void AddFormField(Panel parent, string label, string name, Point location, Size size,
            bool isTextBox = false, bool isComboBox = false, bool isNumeric = false,
            bool isPassword = false, bool readOnly = false, string placeholder = "",
            decimal min = 0, decimal max = 100, decimal increment = 1)
        {
            var lbl = new Label
            {
                Text = label,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = TextSecondary,
                Location = new Point(location.X, location.Y - 20),
                AutoSize = true
            };
            parent.Controls.Add(lbl);

            if (isTextBox)
            {
                var txt = new TextBox
                {
                    Name = name,
                    Location = location,
                    Size = size,
                    Font = new Font("Segoe UI", 9F),
                    BorderStyle = BorderStyle.FixedSingle,
                    ReadOnly = readOnly,
                    BackColor = readOnly ? Color.FromArgb(241, 245, 249) : Color.White
                };

                if (isPassword)
                    txt.PasswordChar = '\u2022';

                if (!string.IsNullOrEmpty(placeholder))
                    txt.PlaceholderText = placeholder;

                parent.Controls.Add(txt);
            }
            else if (isComboBox)
            {
                var cb = new ComboBox
                {
                    Name = name,
                    Location = location,
                    Size = size,
                    Font = new Font("Segoe UI", 9F),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    FlatStyle = FlatStyle.Flat
                };
                parent.Controls.Add(cb);
            }
            else if (isNumeric)
            {
                var num = new NumericUpDown
                {
                    Name = name,
                    Location = location,
                    Size = size,
                    Font = new Font("Segoe UI", 9F),
                    Minimum = min,
                    Maximum = max,
                    Increment = increment,
                    BorderStyle = BorderStyle.FixedSingle
                };
                parent.Controls.Add(num);
            }
        }

        private void LoadConfiguration()
        {
            var txtApiUrl = FindControl<TextBox>("txtApiUrl");
            var txtApiKey = FindControl<TextBox>("txtApiKey");
            var txtSecretKey = FindControl<TextBox>("txtSecretKey");
            var txtLocal = FindControl<TextBox>("txtLocal");
            var cbPrinter = FindControl<ComboBox>("cbPrinter");
            var numInterval = FindControl<NumericUpDown>("numInterval");
            var numCopiasPadrao = FindControl<NumericUpDown>("numCopiasPadrao");
            var numDelayCopias = FindControl<NumericUpDown>("numDelayCopias");
            var chkAutoStart = FindControl<CheckBox>("chkAutoStart");

            if (txtApiUrl == null || txtApiKey == null || txtSecretKey == null ||
                txtLocal == null || cbPrinter == null || numInterval == null ||
                numCopiasPadrao == null || numDelayCopias == null || chkAutoStart == null)
            {
                LogService.LogWarning("Alguns controles nao foram encontrados. Ignorando LoadConfiguration.");
                return;
            }

            txtApiUrl.Text = _config.ApiUrl;
            txtApiKey.Text = _config.ApiKey ?? "";
            txtSecretKey.Text = _config.SecretKey ?? "";
            txtLocal.Text = _config.ClientLocal ?? "";
            numInterval.Value = _config.PollingInterval;
            numCopiasPadrao.Value = _config.CopiasPadrao;
            numDelayCopias.Value = _config.DelayEntreCopias;

            // Auto-start é gerenciado pelo instalador Inno Setup
            chkAutoStart.Checked = StartupService.EstaNoAutoStart();
            chkAutoStart.Enabled = false; // Somente leitura — gerenciado pelo instalador

            string[] printers = PrinterService.GetAvailablePrinters();
            cbPrinter.Items.AddRange(printers);

            if (!string.IsNullOrEmpty(_config.PrinterName) && cbPrinter.Items.Contains(_config.PrinterName))
            {
                cbPrinter.SelectedItem = _config.PrinterName;
            }
            else if (printers.Length > 0)
            {
                string defaultPrinter = PrinterService.GetDefaultPrinter();
                cbPrinter.SelectedItem = !string.IsNullOrEmpty(defaultPrinter) ? defaultPrinter : printers[0];
            }
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                var txtApiUrl = FindControl<TextBox>("txtApiUrl");
                var txtApiKey = FindControl<TextBox>("txtApiKey");
                var txtSecretKey = FindControl<TextBox>("txtSecretKey");
                var cbPrinter = FindControl<ComboBox>("cbPrinter");
                var numInterval = FindControl<NumericUpDown>("numInterval");
                var numCopiasPadrao = FindControl<NumericUpDown>("numCopiasPadrao");
                var numDelayCopias = FindControl<NumericUpDown>("numDelayCopias");
                var chkAutoStart = FindControl<CheckBox>("chkAutoStart");

                if (txtApiUrl == null || txtApiKey == null || txtSecretKey == null ||
                    cbPrinter == null || numInterval == null || numCopiasPadrao == null ||
                    numDelayCopias == null || chkAutoStart == null)
                {
                    MessageBox.Show("Erro ao acessar controles do formulario.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string apiUrl = txtApiUrl.Text.Trim().TrimEnd('/');
                string apiKey = txtApiKey.Text.Trim();
                string secretKey = txtSecretKey.Text.Trim();

                if (string.IsNullOrWhiteSpace(apiUrl))
                {
                    MessageBox.Show("URL da API e obrigatoria", "Validacao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out Uri? uri) ||
                    (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    MessageBox.Show("URL da API invalida. Use http:// ou https://", "Validacao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    MessageBox.Show("API Key e obrigatoria", "Validacao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(secretKey))
                {
                    MessageBox.Show("Secret Key e obrigatoria", "Validacao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (cbPrinter.SelectedIndex == -1)
                {
                    MessageBox.Show("Selecione uma impressora", "Validacao", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _config.ApiUrl = apiUrl;
                _config.ApiKey = apiKey;
                _config.SecretKey = secretKey;
                _config.PrinterName = cbPrinter.SelectedItem?.ToString() ?? "";
                _config.PollingInterval = (int)numInterval.Value;
                _config.CopiasPadrao = (int)numCopiasPadrao.Value;
                _config.DelayEntreCopias = (int)numDelayCopias.Value;
                _config.AutoStart = chkAutoStart.Checked;

                ConfigService.Save(_config);

                // Recriar HealthCheckService com a nova URL (string é cópia, não referência)
                _healthCheckService = new HealthCheckService(_config.ApiUrl);

                // Nota: Auto-start é gerenciado pelo instalador Inno Setup

                LogMessage("Autenticando com servidor...");
                await AuthenticateAsync(true);

                // Iniciar/reiniciar polling e timers (importante quando credenciais foram configuradas pela primeira vez)
                InitializePolling();
                InitializeUpdateChecker();
                InitializeRemoteLogSender();

                if (!string.IsNullOrEmpty(_config.PrinterName))
                {
                    _printerService = new PrinterService(_config.PrinterName);
                }

                LogMessage("Configuracoes salvas com sucesso!");

                string mensagem = "Configuracoes salvas com sucesso!\n\n";
                mensagem += "Configuracoes salvas:\n";
                mensagem += "   - API Key e Secret Key\n";
                mensagem += "   - URL da API\n";
                mensagem += "   - Impressora selecionada\n";
                mensagem += "   - Intervalo de polling\n";
                mensagem += "   - Copias padrao e delay\n";
                mensagem += $"\nConfig salvo em: {ConfigService.GetConfigPath()}\n";

                if (chkAutoStart.Checked)
                {
                    mensagem += "\nAuto-start configurado: O aplicativo iniciara automaticamente quando o Windows reiniciar.";
                }
                else
                {
                    mensagem += "\nAuto-start desabilitado.";
                }

                MessageBox.Show(mensagem, "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                LogMessage($"Erro ao salvar: {ex.Message}");
                MessageBox.Show($"Erro ao salvar configuracoes: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task AuthenticateAsync(bool showErrorDialog = false)
        {
            try
            {
                LogMessage("Autenticando com servidor...");

                var authResponse = await _apiService.AuthenticateAsync();

                if (authResponse.Success && authResponse.Authenticated && authResponse.Client != null)
                {
                    _config.ClientLocal = authResponse.Client.Local;
                    ConfigService.Save(_config);

                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() =>
                        {
                            var txtLocal = FindControl<TextBox>("txtLocal");
                            if (txtLocal != null) txtLocal.Text = authResponse.Client.Local;
                        }));
                    }
                    else
                    {
                        var txtLocal = FindControl<TextBox>("txtLocal");
                        if (txtLocal != null) txtLocal.Text = authResponse.Client.Local;
                    }

                    LogMessage($"Autenticado como: {authResponse.Client.Nome} ({authResponse.Client.Local})");
                }
                else
                {
                    LogMessage($"Falha na autenticacao: {authResponse.Error ?? "Erro desconhecido"}");

                    if (showErrorDialog)
                    {
                        MessageBox.Show(
                            $"Falha na autenticacao:\n\n{authResponse.Error ?? "Erro desconhecido"}\n\nVerifique API Key e Secret Key.",
                            "Erro de Autenticacao",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Erro ao autenticar: {ex.Message}");

                if (showErrorDialog)
                {
                    MessageBox.Show(
                        $"Erro ao autenticar:\n\n{ex.Message}",
                        "Erro de Autenticacao",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
        }

        private async void BtnTest_Click(object? sender, EventArgs e)
        {
            try
            {
                LogMessage("Testando conexao com API...");
                bool success = await _apiService.TestConnectionAsync();

                if (success)
                {
                    LogMessage("Conexao com API OK!");
                    MessageBox.Show("Conexao com API estabelecida com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    LogMessage("Falha na conexao com API");
                    MessageBox.Show("Nao foi possivel conectar a API. Verifique a URL e tente novamente.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Erro no teste: {ex.Message}");
                MessageBox.Show($"Erro ao testar conexao: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnOpenLogs_Click(object? sender, EventArgs e)
        {
            try
            {
                LogMessage("Abrindo pasta de logs...");
                LogService.OpenLogFolder();
            }
            catch (Exception ex)
            {
                LogMessage($"Erro ao abrir pasta de logs: {ex.Message}");
                MessageBox.Show($"Erro ao abrir pasta de logs: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnDiagnostics_Click(object? sender, EventArgs e)
        {
            try
            {
                LogMessage("Executando diagnostico completo...");

                string diagnostics = await _healthCheckService.RunFullDiagnosticsAsync();

                LogMessage("Diagnostico completo realizado");

                MessageBox.Show(
                    diagnostics,
                    "Diagnostico Completo do Sistema",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                LogMessage($"Erro ao executar diagnostico: {ex.Message}");
                MessageBox.Show($"Erro ao executar diagnostico: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializePolling()
        {
            _pollingTimer?.Dispose();

            if (!string.IsNullOrEmpty(_config.PrinterName))
            {
                _printerService = new PrinterService(_config.PrinterName);
            }

            _pollingTimer = new System.Threading.Timer(
                async _ => await PollJobsAsync(),
                null,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromMilliseconds(_config.PollingInterval)
            );

            LogMessage("Sistema iniciado. Aguardando impressoes...");

            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    var lblStatus = FindControl<Label>("lblStatus");
                    if (lblStatus != null)
                    {
                        lblStatus.Text = "Status: Ativo";
                        lblStatus.ForeColor = Color.FromArgb(16, 185, 129);
                    }
                }));
            }
            else
            {
                var lblStatus = FindControl<Label>("lblStatus");
                if (lblStatus != null)
                {
                    lblStatus.Text = "Status: Ativo";
                    lblStatus.ForeColor = Color.FromArgb(16, 185, 129);
                }
            }
        }

        private async Task PollJobsAsync()
        {
            if (!await _processingSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                var response = await _apiService.FetchJobsAsync();

                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        var lblLastCheck = FindControl<Label>("lblLastCheck");
                        if (lblLastCheck != null)
                            lblLastCheck.Text = $"Ultima verificacao: {DateTime.Now:HH:mm:ss}";

                        var lblStatus = FindControl<Label>("lblStatus");
                        if (lblStatus != null)
                        {
                            lblStatus.Text = "Status: Ativo";
                            lblStatus.ForeColor = Color.FromArgb(16, 185, 129);
                        }
                    }));
                }

                if (response.Success && response.Jobs.Count > 0)
                {
                    LogMessage($"{response.Jobs.Count} job(s) encontrado(s)");

                    foreach (var job in response.Jobs)
                    {
                        await ProcessJobAsync(job);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Erro no polling: {ex.Message}");

                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        var lblStatus = FindControl<Label>("lblStatus");
                        if (lblStatus != null)
                        {
                            lblStatus.Text = "Status: Erro de Conexao";
                            lblStatus.ForeColor = Color.FromArgb(239, 68, 68);
                        }
                    }));
                }
                else
                {
                    var lblStatus = FindControl<Label>("lblStatus");
                    if (lblStatus != null)
                    {
                        lblStatus.Text = "Status: Erro de Conexao";
                        lblStatus.ForeColor = Color.FromArgb(239, 68, 68);
                    }
                }
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }

        private async Task ProcessJobAsync(PrintJob job)
        {
            int tentativas = 0;
            int maxTentativas = _config.MaxRetries;

            while (tentativas < maxTentativas)
            {
                try
                {
                    if (_printerService == null && string.IsNullOrEmpty(job.ImpressoraNome))
                    {
                        throw new Exception("Nenhuma impressora configurada");
                    }

                    if (tentativas > 0)
                    {
                        LogMessage($"Tentativa {tentativas + 1}/{maxTentativas} para job #{job.Id}...");
                        await Task.Delay(1000 * tentativas);
                    }
                    else
                    {
                        if (job.Copias > 1)
                        {
                            LogMessage($"Imprimindo job #{job.Id} ({job.Copias} copias)...");
                        }
                        else
                        {
                            LogMessage($"Imprimindo job #{job.Id}...");
                        }
                    }

                    if (_printerService != null)
                    {
                        _printerService.Print(job, _config.DelayEntreCopias);
                    }
                    else
                    {
                        var tempPrinter = new PrinterService(job.ImpressoraNome!);
                        tempPrinter.Print(job, _config.DelayEntreCopias);
                    }

                    await _apiService.CompleteJobAsync(job.Id);

                    LogMessage($"Job #{job.Id} impresso com sucesso");

                    // v2.1.0: Incrementar stats
                    LogService.IncrementJobsProcessados();

                    // Notificação visual no tray
                    if (_trayIcon != null)
                    {
                        string notificationTitle = "Impressao Concluida";
                        string notificationMessage = job.Copias > 1
                            ? $"Job #{job.Id} impresso com sucesso ({job.Copias} copias)"
                            : $"Job #{job.Id} impresso com sucesso";

                        if (this.InvokeRequired)
                        {
                            this.Invoke(new Action(() =>
                            {
                                _trayIcon.ShowBalloonTip(3000, notificationTitle, notificationMessage, ToolTipIcon.Info);
                            }));
                        }
                        else
                        {
                            _trayIcon.ShowBalloonTip(3000, notificationTitle, notificationMessage, ToolTipIcon.Info);
                        }
                    }

                    // Atualizar contador
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() =>
                        {
                            _jobsProcessed++;
                            var lblJobsProcessed = FindControl<Label>("lblJobsProcessed");
                            if (lblJobsProcessed != null)
                            {
                                lblJobsProcessed.Text = $"Jobs processados: {_jobsProcessed}";
                            }
                        }));
                    }

                    return;
                }
                catch (Exception ex)
                {
                    tentativas++;

                    if (tentativas >= maxTentativas)
                    {
                        LogMessage($"Erro ao processar job #{job.Id} apos {maxTentativas} tentativas: {ex.Message}");
                        await _apiService.FailJobAsync(job.Id, $"Falha apos {maxTentativas} tentativas: {ex.Message}");

                        // v2.1.0: Incrementar stats de erro
                        LogService.IncrementJobsErro();
                    }
                    else
                    {
                        LogMessage($"Erro na tentativa {tentativas}/{maxTentativas} para job #{job.Id}: {ex.Message}. Tentando novamente...");
                    }
                }
            }
        }

        /// <summary>
        /// Atualiza o label de status na UI (thread-safe)
        /// </summary>
        private void UpdateStatusDisplay(string text, Color color)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateStatusDisplay(text, color)));
                return;
            }

            var lblStatus = FindControl<Label>("lblStatus");
            if (lblStatus != null)
            {
                lblStatus.Text = text;
                lblStatus.ForeColor = color;
            }
        }

        private void LogMessage(string message)
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}";

            if (message.Contains("Erro") || message.Contains("ERRO") || message.Contains("Falha"))
            {
                LogService.LogError(message);
            }
            else if (message.Contains("nao acessivel") || message.Contains("nao foi possivel"))
            {
                LogService.LogWarning(message);
            }
            else if (message.Contains("sucesso") || message.Contains("OK") || message.Contains("Autenticado"))
            {
                LogService.LogSuccess(message);
            }
            else
            {
                LogService.LogInfo(message);
            }

            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    var txtLog = FindControl<TextBox>("txtLog");
                    if (txtLog != null)
                        txtLog.AppendText(logEntry + Environment.NewLine);
                }));
            }
            else
            {
                var txtLog = FindControl<TextBox>("txtLog");
                if (txtLog != null)
                    txtLog.AppendText(logEntry + Environment.NewLine);
            }

            Console.WriteLine(logEntry);
        }

        private void OnTrayOpen(object? sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }

        private void OnTrayConfig(object? sender, EventArgs e)
        {
            OnTrayOpen(sender, e);
        }

        private void OnTrayExit(object? sender, EventArgs e)
        {
            _trayIcon?.Dispose();
            Application.Exit();
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_config.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                this.ShowInTaskbar = false;

                if (_config.ShowNotifications)
                {
                    _trayIcon?.ShowBalloonTip(2000, "Delivery Print Client", "Aplicativo minimizado para bandeja", ToolTipIcon.Info);
                }
            }
        }

        // Nota: ConfigurarPrimeiraExecucao removido — atalhos e auto-start
        // são configurados pelo instalador Inno Setup, eliminando padrões
        // que antivírus detectam como comportamento de trojan.

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pollingTimer?.Dispose();
                _updateCheckTimer?.Dispose();
                _remoteLogTimer?.Dispose();
                _trayIcon?.Dispose();
                _trayMenu?.Dispose();
                _processingSemaphore?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
