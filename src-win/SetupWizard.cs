using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DupFinderNeoSetup
{
    public class SetupProgressBar : Control
    {
        private int min = 0;
        private int max = 100;
        private int val = 0;
        private string customText = string.Empty;

        public SetupProgressBar()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.Height = 26;
        }

        public int Minimum { get { return min; } set { min = value; Invalidate(); } }
        public int Maximum { get { return max; } set { max = value; Invalidate(); } }
        public int Value { get { return val; } set { val = Math.Max(min, Math.Min(max, value)); Invalidate(); } }
        public string CustomText { get { return customText; } set { customText = value; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(235, 238, 242)))
            {
                g.FillRectangle(bgBrush, 0, 0, this.Width, this.Height);
            }

            float pct = (max > min) ? (float)(val - min) / (max - min) : 0f;
            int fillWidth = (int)(this.Width * pct);

            if (fillWidth > 0)
            {
                Rectangle fillRect = new Rectangle(0, 0, fillWidth, this.Height);
                using (LinearGradientBrush fillBrush = new LinearGradientBrush(fillRect, Color.FromArgb(41, 128, 185), Color.FromArgb(52, 152, 219), LinearGradientMode.Vertical))
                {
                    g.FillRectangle(fillBrush, fillRect);
                }
            }

            using (Pen borderPen = new Pen(Color.FromArgb(189, 195, 199)))
            {
                g.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
            }

            string display = !string.IsNullOrEmpty(customText) ? customText : string.Format("{0}%", (int)(pct * 100));
            using (Font font = new Font("Segoe UI", 9F, FontStyle.Bold))
            {
                SizeF textSize = g.MeasureString(display, font);
                float tx = (this.Width - textSize.Width) / 2f;
                float ty = (this.Height - textSize.Height) / 2f;

                if (fillWidth > tx + (textSize.Width / 2))
                {
                    using (SolidBrush textBrush = new SolidBrush(Color.White))
                        g.DrawString(display, font, textBrush, tx, ty);
                }
                else
                {
                    using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(44, 62, 80)))
                        g.DrawString(display, font, textBrush, tx, ty);
                }
            }
        }
    }

    public class SetupForm : Form
    {
        private Panel pnlHeader;
        private Label lblHeaderTitle;
        private Label lblHeaderSubtitle;

        private Panel pnlContent;
        private Panel pnlBottom;
        private Button btnBack;
        private Button btnNext;
        private Button btnCancel;

        // Steps panels
        private Panel pnlWelcome;
        private Panel pnlLicense;
        private Panel pnlDest;
        private Panel pnlOptions;
        private Panel pnlProgress;
        private Panel pnlFinish;

        private int currentStep = 0;

        // Controls in Step 1 (License)
        private RichTextBox rtbLicense;
        private RadioButton rbAcceptLicense;
        private RadioButton rbRejectLicense;

        // Controls in Step 2 (Dest & Scope)
        private RadioButton rbAllUsers;
        private RadioButton rbCurrentUser;
        private TextBox txtInstallPath;
        private Button btnBrowse;

        // Controls in Step 3 (Options)
        private CheckBox cbDesktopShortcut;
        private CheckBox cbStartMenuShortcut;
        private CheckBox cbWatcherService;

        // Controls in Step 4 (Progress)
        private SetupProgressBar progressBar;
        private Label lblProgressStatus;

        // Controls in Step 5 (Finish)
        private CheckBox cbLaunchAfter;

        private string previousInstallDir = null;
        private string previousVersion = null;

        public SetupForm()
        {
            CheckExistingInstallation();
            InitializeComponent();
            ShowStep(0);
        }

        private void CheckExistingInstallation()
        {
            try
            {
                RegistryKey[] roots = new RegistryKey[] { Registry.LocalMachine, Registry.CurrentUser };
                foreach (var root in roots)
                {
                    using (var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DupFinder-Neo"))
                    {
                        if (key != null)
                        {
                            previousInstallDir = key.GetValue("InstallLocation") as string;
                            previousVersion = key.GetValue("DisplayVersion") as string;
                            if (!string.IsNullOrEmpty(previousInstallDir)) break;
                        }
                    }
                }
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Text = "DupFinder-Neo v1.3 - Assistente de Instalacao (by @heitorsecurity)";
            this.Size = new Size(650, 480);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // Header banner
            pnlHeader = new Panel() { Dock = DockStyle.Top, Height = 72, BackColor = Color.FromArgb(245, 248, 255) };
            pnlHeader.Paint += (s, pe) =>
            {
                using (Pen p = new Pen(Color.FromArgb(215, 225, 240)))
                    pe.Graphics.DrawLine(p, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
            };

            lblHeaderTitle = new Label()
            {
                Text = "DupFinder-Neo v1.3 (by @heitorsecurity)",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 43, 73),
                Location = new Point(22, 13),
                AutoSize = true
            };
            lblHeaderSubtitle = new Label()
            {
                Text = "Assistente de Instalacao Completo para Windows XP ate Windows 11",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(90, 105, 125),
                Location = new Point(24, 38),
                AutoSize = true
            };
            pnlHeader.Controls.AddRange(new Control[] { lblHeaderTitle, lblHeaderSubtitle });
            this.Controls.Add(pnlHeader);

            // Bottom action panel
            pnlBottom = new Panel() { Dock = DockStyle.Bottom, Height = 56, BackColor = Color.FromArgb(245, 245, 245) };
            pnlBottom.Paint += (s, pe) =>
            {
                using (Pen p = new Pen(Color.FromArgb(220, 220, 220)))
                    pe.Graphics.DrawLine(p, 0, 0, pnlBottom.Width, 0);
            };

            btnBack = new Button() { Text = "< Voltar", Location = new Point(330, 13), Width = 90, Height = 30 };
            btnNext = new Button() { Text = "Avancar >", Location = new Point(425, 13), Width = 100, Height = 30, BackColor = Color.LightSteelBlue, Font = new Font(this.Font, FontStyle.Bold) };
            btnCancel = new Button() { Text = "Cancelar", Location = new Point(530, 13), Width = 90, Height = 30 };

            btnBack.Click += (s, e) => ShowStep(currentStep - 1);
            btnNext.Click += BtnNext_Click;
            btnCancel.Click += (s, e) => this.Close();

            pnlBottom.Controls.AddRange(new Control[] { btnBack, btnNext, btnCancel });
            this.Controls.Add(pnlBottom);

            // Center content container
            pnlContent = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(25, 18, 25, 15) };
            this.Controls.Add(pnlContent);

            BuildWelcomePanel();
            BuildLicensePanel();
            BuildDestPanel();
            BuildOptionsPanel();
            BuildProgressPanel();
            BuildFinishPanel();
        }

        private void BuildWelcomePanel()
        {
            pnlWelcome = new Panel() { Dock = DockStyle.Fill, Visible = false };

            Label lblTitle = new Label()
            {
                Text = "Bem-vindo a Instalacao do DupFinder-Neo v1.3",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 50, 90),
                Location = new Point(0, 5),
                AutoSize = true
            };

            Label lblDesc = new Label()
            {
                Text = "O DupFinder-Neo v1.3 e um localizador ultra-rapido de arquivos duplicados, detector de pastas espelhadas em comum e limpador seguro de pastas vazias, compativel com Windows XP, Vista, 7, 8, 10 e 11.\r\n\r\n" +
                       "• Desenvolvido originalmente por Patrick De Smet.\r\n" +
                       "• Totalmente modernizado e expandido por @heitorsecurity com pre-hashing amostral FNV-1a 64-bit, salvaguarda do SO, historico detalhado de delecoes e monitor em tempo real.\r\n\r\n" +
                       "Clique em 'Avancar' para continuar com o assistente.",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                Location = new Point(2, 45),
                Size = new Size(580, 160)
            };

            pnlWelcome.Controls.AddRange(new Control[] { lblTitle, lblDesc });

            if (!string.IsNullOrEmpty(previousInstallDir))
            {
                Label lblReinstall = new Label()
                {
                    Text = "ℹ️ Versao anterior (" + (previousVersion ?? "v1.0") + ") detectada em:\r\n" +
                           previousInstallDir + "\r\n" +
                           "O assistente ira atualizar e sobrescrever os arquivos existentes de forma limpa.",
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = Color.DarkSlateBlue,
                    Location = new Point(2, 185),
                    Size = new Size(580, 55)
                };
                pnlWelcome.Controls.Add(lblReinstall);
            }

            pnlContent.Controls.Add(pnlWelcome);
        }

        private void BuildLicensePanel()
        {
            pnlLicense = new Panel() { Dock = DockStyle.Fill, Visible = false };

            Label lblLicPrompt = new Label()
            {
                Text = "Termos do Contrato de Licenca e Modificacoes:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(0, 0),
                AutoSize = true
            };

            rtbLicense = new RichTextBox()
            {
                Location = new Point(0, 24),
                Size = new Size(585, 180),
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 8.25F, FontStyle.Regular)
            };

            string licenseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LICENSE.TXT");
            if (File.Exists(licenseFile))
            {
                try { rtbLicense.Text = File.ReadAllText(licenseFile); } catch { }
            }
            if (string.IsNullOrEmpty(rtbLicense.Text))
            {
                rtbLicense.Text = "DupFinder-Neo v1.3 (by @heitorsecurity)\r\nOriginal Work: Copyright (C) Patrick De Smet (GNU GPL v2)\r\nModernization: Copyright (C) 2026 @heitorsecurity\r\nLicensed under GNU General Public License v2.";
            }

            rbAcceptLicense = new RadioButton()
            {
                Text = "Eu aceito os termos do Contrato de Licenca e modificacoes de @heitorsecurity",
                Location = new Point(2, 215),
                AutoSize = true
            };
            rbRejectLicense = new RadioButton()
            {
                Text = "Eu nao aceito os termos do contrato",
                Location = new Point(2, 240),
                Checked = true,
                AutoSize = true
            };

            rbAcceptLicense.CheckedChanged += (s, e) => btnNext.Enabled = rbAcceptLicense.Checked;
            rbRejectLicense.CheckedChanged += (s, e) => btnNext.Enabled = rbAcceptLicense.Checked;

            pnlLicense.Controls.AddRange(new Control[] { lblLicPrompt, rtbLicense, rbAcceptLicense, rbRejectLicense });
            pnlContent.Controls.Add(pnlLicense);
        }

        private void BuildDestPanel()
        {
            pnlDest = new Panel() { Dock = DockStyle.Fill, Visible = false };

            Label lblScopeTitle = new Label()
            {
                Text = "Escopo de Instalacao (Quem podera usar):",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(0, 5),
                AutoSize = true
            };

            rbAllUsers = new RadioButton()
            {
                Text = "Instalar para TODOS os usuarios deste computador (Recomendado)",
                Location = new Point(5, 30),
                Checked = true,
                AutoSize = true
            };

            rbCurrentUser = new RadioButton()
            {
                Text = "Instalar apenas para o usuario atual",
                Location = new Point(5, 55),
                AutoSize = true
            };

            Label lblPathTitle = new Label()
            {
                Text = "Pasta de Destino da Instalacao:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(0, 95),
                AutoSize = true
            };

            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (string.IsNullOrEmpty(progFiles)) progFiles = @"C:\Program Files";
            string defaultAll = Path.Combine(progFiles, "DupFinder-Neo");

            string initialPath = !string.IsNullOrEmpty(previousInstallDir) ? previousInstallDir : defaultAll;
            txtInstallPath = new TextBox()
            {
                Text = initialPath,
                Location = new Point(2, 120),
                Width = 475
            };

            btnBrowse = new Button()
            {
                Text = "Procurar...",
                Location = new Point(485, 118),
                Width = 95,
                Height = 26
            };

            rbAllUsers.CheckedChanged += (s, e) =>
            {
                if (rbAllUsers.Checked)
                    txtInstallPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "DupFinder-Neo");
            };

            rbCurrentUser.CheckedChanged += (s, e) =>
            {
                if (rbCurrentUser.Checked)
                    txtInstallPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs\\DupFinder-Neo");
            };

            btnBrowse.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = txtInstallPath.Text;
                    if (fbd.ShowDialog() == DialogResult.OK) txtInstallPath.Text = fbd.SelectedPath;
                }
            };

            pnlDest.Controls.AddRange(new Control[] { lblScopeTitle, rbAllUsers, rbCurrentUser, lblPathTitle, txtInstallPath, btnBrowse });
            pnlContent.Controls.Add(pnlDest);
        }

        private void BuildOptionsPanel()
        {
            pnlOptions = new Panel() { Dock = DockStyle.Fill, Visible = false };

            Label lblOptTitle = new Label()
            {
                Text = "Selecione as tarefas e recursos adicionais desejados:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(0, 5),
                AutoSize = true
            };

            cbDesktopShortcut = new CheckBox()
            {
                Text = "Criar atalho na Area de Trabalho (Desktop)",
                Checked = true,
                Location = new Point(5, 35),
                AutoSize = true
            };

            cbStartMenuShortcut = new CheckBox()
            {
                Text = "Criar atalhos no Menu Iniciar (DupFinder-Neo, Readme.txt e Desinstalar)",
                Checked = true,
                Location = new Point(5, 65),
                AutoSize = true
            };

            GroupBox gbWatcher = new GroupBox()
            {
                Text = "Monitor / Indexador Inteligente em Segundo Plano (Estilo 'Everything')",
                Location = new Point(0, 105),
                Size = new Size(585, 120),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            cbWatcherService = new CheckBox()
            {
                Text = "Ativar o Monitor DupFinder-Neo Watcher em segundo plano",
                Checked = false,
                Location = new Point(15, 25),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };

            Label lblWatcherDesc = new Label()
            {
                Text = "Quando ativado, o monitor funciona de forma ultra-leve na bandeja do sistema e detecta quando voce cria, baixa ou copia arquivos repetidos no computador, exibindo um alerta imediato na barra de notificacoes do Windows com opcoes para abrir ou excluir a copia duplicada.\r\n(Opcional: so sera ativado se voce marcar esta caixa).",
                Location = new Point(15, 52),
                Size = new Size(550, 60),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.DimGray
            };

            gbWatcher.Controls.AddRange(new Control[] { cbWatcherService, lblWatcherDesc });
            pnlOptions.Controls.AddRange(new Control[] { lblOptTitle, cbDesktopShortcut, cbStartMenuShortcut, gbWatcher });
            pnlContent.Controls.Add(pnlOptions);
        }

        private void BuildProgressPanel()
        {
            pnlProgress = new Panel() { Dock = DockStyle.Fill, Visible = false };

            Label lblProgTitle = new Label()
            {
                Text = "Instalando o DupFinder-Neo v1.3...",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Location = new Point(0, 30),
                AutoSize = true
            };

            lblProgressStatus = new Label()
            {
                Text = "Preparando componentes...",
                Location = new Point(2, 70),
                AutoSize = true,
                ForeColor = Color.DimGray
            };

            progressBar = new SetupProgressBar()
            {
                Location = new Point(0, 100),
                Size = new Size(585, 26),
                Value = 0
            };

            pnlProgress.Controls.AddRange(new Control[] { lblProgTitle, lblProgressStatus, progressBar });
            pnlContent.Controls.Add(pnlProgress);
        }

        private void BuildFinishPanel()
        {
            pnlFinish = new Panel() { Dock = DockStyle.Fill, Visible = false };

            Label lblFinTitle = new Label()
            {
                Text = "Instalacao Concluida com Sucesso!",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.DarkGreen,
                Location = new Point(0, 15),
                AutoSize = true
            };

            Label lblFinDesc = new Label()
            {
                Text = "O DupFinder-Neo v1.3 (by @heitorsecurity) foi instalado com sucesso no seu computador.\r\n\r\n" +
                       "Foram gerados atalhos para o programa, documentacao e desinstalador limpo no seu Menu Iniciar.\r\n" +
                       "Clique em 'Concluir' para fechar o assistente.",
                Location = new Point(2, 55),
                Size = new Size(580, 80)
            };

            cbLaunchAfter = new CheckBox()
            {
                Text = "Executar o DupFinder-Neo v1.3 agora",
                Checked = true,
                Location = new Point(5, 150),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };

            pnlFinish.Controls.AddRange(new Control[] { lblFinTitle, lblFinDesc, cbLaunchAfter });
            pnlContent.Controls.Add(pnlFinish);
        }

        private void ShowStep(int step)
        {
            currentStep = step;
            pnlWelcome.Visible = (step == 0);
            pnlLicense.Visible = (step == 1);
            pnlDest.Visible = (step == 2);
            pnlOptions.Visible = (step == 3);
            pnlProgress.Visible = (step == 4);
            pnlFinish.Visible = (step == 5);

            btnBack.Enabled = (step > 0 && step < 4);
            btnBack.Visible = (step < 4);
            btnCancel.Visible = (step < 4);

            if (step == 0)
            {
                lblHeaderSubtitle.Text = "Bem-vindo a Instalacao do DupFinder-Neo v1.3";
                btnNext.Text = "Avancar >";
                btnNext.Enabled = true;
            }
            else if (step == 1)
            {
                lblHeaderSubtitle.Text = "Contrato de Licenca e Modificacoes";
                btnNext.Text = "Avancar >";
                btnNext.Enabled = rbAcceptLicense.Checked;
            }
            else if (step == 2)
            {
                lblHeaderSubtitle.Text = "Escolha da Pasta de Destino e Escopo";
                btnNext.Text = "Avancar >";
                btnNext.Enabled = true;
            }
            else if (step == 3)
            {
                lblHeaderSubtitle.Text = "Atalhos e Monitor em Segundo Plano";
                btnNext.Text = "Instalar";
                btnNext.Enabled = true;
            }
            else if (step == 4)
            {
                lblHeaderSubtitle.Text = "Instalando componentes...";
                btnNext.Enabled = false;
                btnBack.Enabled = false;
                btnCancel.Enabled = false;
            }
            else if (step == 5)
            {
                lblHeaderSubtitle.Text = "Instalacao Finalizada com Exito";
                btnNext.Text = "Concluir";
                btnNext.Enabled = true;
                btnBack.Visible = false;
                btnCancel.Visible = false;
            }
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            if (currentStep < 3)
            {
                ShowStep(currentStep + 1);
            }
            else if (currentStep == 3)
            {
                ShowStep(4);
                PerformInstallation();
            }
            else if (currentStep == 5)
            {
                if (cbLaunchAfter.Checked)
                {
                    string mainExe = Path.Combine(txtInstallPath.Text, "dupfinder-neo.exe");
                    if (File.Exists(mainExe)) Process.Start(mainExe);
                }
                this.Close();
            }
        }

        private void PerformInstallation()
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                string targetDir = txtInstallPath.Text;
                string sourceDir = AppDomain.CurrentDomain.BaseDirectory;

                UpdateProgress(10, "Finalizando processos anteriores para atualizacao limpa...");
                StopAllDupFinderProcesses();

                UpdateProgress(18, "Removendo arquivos da versao anterior para substituicao limpa...");
                string[] filesToClean = new string[] { "dupfinder-neo.exe", "dupfinder-watcher.exe", "readme.txt", "LICENSE.TXT" };
                if (Directory.Exists(targetDir))
                {
                    foreach (var f in filesToClean)
                    {
                        string p = Path.Combine(targetDir, f);
                        try { if (File.Exists(p)) File.Delete(p); } catch { }
                    }
                }
                else
                {
                    try { Directory.CreateDirectory(targetDir); } catch { }
                }

                if (!string.IsNullOrEmpty(previousInstallDir) &&
                    !string.Equals(previousInstallDir, targetDir, StringComparison.OrdinalIgnoreCase) &&
                    Directory.Exists(previousInstallDir))
                {
                    foreach (var f in filesToClean)
                    {
                        string p = Path.Combine(previousInstallDir, f);
                        try { if (File.Exists(p)) File.Delete(p); } catch { }
                    }
                }

                UpdateProgress(30, "Copiando executaveis do DupFinder-Neo v1.3...");
                CopyFileSafe(sourceDir, targetDir, "dupfinder-neo.exe");
                CopyFileSafe(sourceDir, targetDir, "dupfinder-watcher.exe");

                UpdateProgress(45, "Copiando assistente e desinstalador...");
                string currentExe = Application.ExecutablePath;
                string targetSetup = Path.Combine(targetDir, "dupfinder-neo-setup.exe");
                try { File.Copy(currentExe, targetSetup, true); } catch { }

                UpdateProgress(60, "Copiando arquivos de documentacao...");
                CopyFileSafe(sourceDir, targetDir, "readme.txt");
                CopyFileSafe(sourceDir, targetDir, "LICENSE.TXT");

                UpdateProgress(75, "Criando atalhos no Desktop e Menu Iniciar...");
                string exeTarget = Path.Combine(targetDir, "dupfinder-neo.exe");
                string readmeTarget = Path.Combine(targetDir, "readme.txt");

                // Desktop shortcut
                if (cbDesktopShortcut.Checked)
                {
                    string desktopFolder = rbAllUsers.Checked
                        ? Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
                        : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                    string deskLink = Path.Combine(desktopFolder, "DupFinder-Neo.lnk");
                    CreateShortcut(deskLink, exeTarget, "", "DupFinder-Neo v1.3 (by @heitorsecurity)", targetDir, exeTarget);
                }

                // Start Menu folder
                if (cbStartMenuShortcut.Checked)
                {
                    string startPrograms = rbAllUsers.Checked
                        ? Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
                        : Environment.GetFolderPath(Environment.SpecialFolder.Programs);

                    string startGroup = Path.Combine(startPrograms, "DupFinder-Neo");
                    try { if (!Directory.Exists(startGroup)) Directory.CreateDirectory(startGroup); } catch { }

                    CreateShortcut(Path.Combine(startGroup, "DupFinder-Neo.lnk"), exeTarget, "", "DupFinder-Neo v1.3 (by @heitorsecurity)", targetDir, exeTarget);
                    CreateShortcut(Path.Combine(startGroup, "Readme.txt.lnk"), readmeTarget, "", "Informacoes e Ajuda do DupFinder-Neo", targetDir, "");
                    CreateShortcut(Path.Combine(startGroup, "Desinstalar DupFinder-Neo.lnk"), targetSetup, "/uninstall", "Desinstalar DupFinder-Neo v1.3", targetDir, targetSetup);
                }

                // Background Watcher
                if (cbWatcherService.Checked)
                {
                    UpdateProgress(85, "Configurando Monitor em Segundo Plano...");
                    string watcherTarget = Path.Combine(targetDir, "dupfinder-watcher.exe");
                    if (File.Exists(watcherTarget))
                    {
                        try
                        {
                            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                            {
                                if (key != null) key.SetValue("DupFinderNeoWatcher", "\"" + watcherTarget + "\" --bg");
                            }
                            Process.Start(new ProcessStartInfo(watcherTarget, "--bg") { UseShellExecute = true });
                        }
                        catch { }
                    }
                }

                UpdateProgress(95, "Registrando desinstalador no Painel de Controle...");
                RegisterUninstall(targetDir, targetSetup, exeTarget);

                Thread.Sleep(500);
                UpdateProgress(100, "Instalacao Concluida!");

                this.BeginInvoke(new Action(() => ShowStep(5)));
            });
        }

        private void CopyFileSafe(string srcDir, string dstDir, string fileName)
        {
            try
            {
                string src = Path.Combine(srcDir, fileName);
                string dst = Path.Combine(dstDir, fileName);
                if (File.Exists(src)) File.Copy(src, dst, true);
            }
            catch { }
        }

        private void UpdateProgress(int pct, string status)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() =>
                {
                    progressBar.Value = pct;
                    progressBar.CustomText = string.Format("{0}%", pct);
                    lblProgressStatus.Text = status;
                }));
            }
            else
            {
                progressBar.Value = pct;
                progressBar.CustomText = string.Format("{0}%", pct);
                lblProgressStatus.Text = status;
            }
        }

        private void StopAllDupFinderProcesses()
        {
            try
            {
                Process[] watchProcs = Process.GetProcessesByName("dupfinder-watcher");
                foreach (var p in watchProcs) { try { p.Kill(); } catch { } }

                Process[] mainProcs = Process.GetProcessesByName("dupfinder-neo");
                foreach (var p in mainProcs) { try { p.Kill(); } catch { } }
            }
            catch { }
        }

        private void RegisterUninstall(string installDir, string setupPath, string exePath)
        {
            try
            {
                RegistryKey root = rbAllUsers.Checked ? Registry.LocalMachine : Registry.CurrentUser;
                using (RegistryKey key = root.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DupFinder-Neo"))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "DupFinder-Neo v1.3 (by @heitorsecurity)");
                        key.SetValue("DisplayVersion", "1.3.0");
                        key.SetValue("Publisher", "@heitorsecurity");
                        key.SetValue("InstallLocation", installDir);
                        key.SetValue("UninstallString", "\"" + setupPath + "\" /uninstall");
                        key.SetValue("DisplayIcon", exePath + ",0");
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }

        public static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string description, string workingDir, string iconLocation)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                if (!string.IsNullOrEmpty(arguments))
                    shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
                if (!string.IsNullOrEmpty(description))
                    shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
                if (!string.IsNullOrEmpty(workingDir))
                    shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
                if (!string.IsNullOrEmpty(iconLocation))
                    shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconLocation });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch { }
        }
    }

    public static class Uninstaller
    {
        public static void Run()
        {
            if (MessageBox.Show("Deseja realmente desinstalar o DupFinder-Neo v1.3 (by @heitorsecurity) e todos os seus componentes do computador?",
                "Desinstalar DupFinder-Neo v1.3", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                // 1. Kill any running watcher or main app
                Process[] watchProcs = Process.GetProcessesByName("dupfinder-watcher");
                foreach (var p in watchProcs) { try { p.Kill(); } catch { } }

                Process[] mainProcs = Process.GetProcessesByName("dupfinder-neo");
                foreach (var p in mainProcs) { try { p.Kill(); } catch { } }

                // 2. Remove autostart registry entries
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (key != null) key.DeleteValue("DupFinderNeoWatcher", false);
                    }
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (key != null) key.DeleteValue("DupFinderNeoWatcher", false);
                    }
                }
                catch { }

                // 3. Remove shortcuts
                string[] desktopFolders = new string[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
                };
                foreach (var df in desktopFolders)
                {
                    try
                    {
                        string link = Path.Combine(df, "DupFinder-Neo.lnk");
                        if (File.Exists(link)) File.Delete(link);
                    }
                    catch { }
                }

                string[] programFolders = new string[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
                };
                foreach (var pf in programFolders)
                {
                    try
                    {
                        string group = Path.Combine(pf, "DupFinder-Neo");
                        if (Directory.Exists(group)) Directory.Delete(group, true);
                    }
                    catch { }
                }

                // 4. Remove Add/Remove Programs registry key
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DupFinder-Neo", false);
                    Registry.LocalMachine.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DupFinder-Neo", false);
                }
                catch { }

                // 5. Clean AppData config
                try
                {
                    string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DupFinder-Neo");
                    if (Directory.Exists(appData)) Directory.Delete(appData, true);
                }
                catch { }

                // 6. Delete installation folder files and clean self
                string installDir = AppDomain.CurrentDomain.BaseDirectory;
                string setupExe = Application.ExecutablePath;

                string[] filesToDelete = new string[] { "dupfinder-neo.exe", "dupfinder-watcher.exe", "readme.txt", "LICENSE.TXT" };
                foreach (var f in filesToDelete)
                {
                    try
                    {
                        string p = Path.Combine(installDir, f);
                        if (File.Exists(p)) File.Delete(p);
                    }
                    catch { }
                }

                MessageBox.Show("O DupFinder-Neo v1.3 foi completamente desinstalado do seu computador sem deixar arquivos residuais.",
                    "Desinstalacao Concluida", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Self delete batch
                string cmd = string.Format("/C ping 127.0.0.1 -n 2 > nul & del \"{0}\" & rmdir \"{1}\"", setupExe, installDir);
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", cmd)
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro durante a desinstalacao: " + ex.Message);
            }
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length > 0)
            {
                string cmd = args[0].Trim().ToLowerInvariant();
                if (cmd == "/uninstall" || cmd == "-uninstall" || cmd == "/u" || cmd == "--uninstall")
                {
                    Uninstaller.Run();
                    return;
                }
            }

            Application.Run(new SetupForm());
        }
    }
}
