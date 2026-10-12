using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DupFinderNeoSetup
{
    public class SetupForm : Form
    {
        private TextBox txtInstallPath;
        private Button btnBrowse;
        private CheckBox cbDesktopShortcut;
        private CheckBox cbStartMenuShortcut;
        private CheckBox cbLaunchAfter;
        private Button btnInstall;
        private Button btnCancel;
        private ProgressBar progressBar;
        private Label lblStatus;

        public SetupForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "DupFinder-Neo - Assistente de Instalacao";
            this.Size = new Size(580, 420);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Panel pnlHeader = new Panel() { Dock = DockStyle.Top, Height = 75, BackColor = Color.FromArgb(245, 248, 255) };
            Label lblTitle = new Label()
            {
                Text = "Instalacao do DupFinder-Neo v0.9.0",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Location = new Point(20, 15),
                AutoSize = true
            };
            Label lblSubtitle = new Label()
            {
                Text = "Compativel com Windows XP ate Windows 11 (32-bit e 64-bit)",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.DimGray,
                Location = new Point(22, 42),
                AutoSize = true
            };
            pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblSubtitle });
            this.Controls.Add(pnlHeader);

            Panel pnlBody = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(25, 15, 25, 15) };
            this.Controls.Add(pnlBody);

            Label lblPathTitle = new Label() { Text = "Pasta de Destino da Instalacao:", Location = new Point(25, 95), AutoSize = true };
            this.Controls.Add(lblPathTitle);

            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs\\DupFinder-Neo");
            txtInstallPath = new TextBox() { Text = defaultPath, Location = new Point(25, 120), Width = 410 };
            btnBrowse = new Button() { Text = "Procurar...", Location = new Point(445, 118), Width = 95, Height = 26 };
            btnBrowse.Click += delegate(object s, EventArgs e)
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = txtInstallPath.Text;
                    if (fbd.ShowDialog() == DialogResult.OK) txtInstallPath.Text = fbd.SelectedPath;
                }
            };
            this.Controls.AddRange(new Control[] { txtInstallPath, btnBrowse });

            GroupBox gbOptions = new GroupBox()
            {
                Text = "Opcoes de Instalacao",
                Location = new Point(25, 160),
                Size = new Size(515, 110)
            };
            cbDesktopShortcut = new CheckBox() { Text = "Criar atalho na Area de Trabalho (Desktop)", Checked = true, Location = new Point(20, 25), AutoSize = true };
            cbStartMenuShortcut = new CheckBox() { Text = "Criar atalho no Menu Iniciar", Checked = true, Location = new Point(20, 50), AutoSize = true };
            cbLaunchAfter = new CheckBox() { Text = "Iniciar o DupFinder-Neo ao concluir a instalacao", Checked = true, Location = new Point(20, 75), AutoSize = true };
            gbOptions.Controls.AddRange(new Control[] { cbDesktopShortcut, cbStartMenuShortcut, cbLaunchAfter });
            this.Controls.Add(gbOptions);

            lblStatus = new Label() { Text = "Pronto para instalar.", Location = new Point(25, 280), AutoSize = true, ForeColor = Color.DimGray };
            progressBar = new ProgressBar() { Location = new Point(25, 305), Size = new Size(515, 20), Visible = false };
            this.Controls.AddRange(new Control[] { lblStatus, progressBar });

            Panel pnlBottom = new Panel() { Dock = DockStyle.Bottom, Height = 55, BackColor = Color.FromArgb(240, 240, 240) };
            btnInstall = new Button() { Text = "Instalar Agora", Location = new Point(330, 12), Width = 110, Height = 30, BackColor = Color.LightSteelBlue, Font = new Font(this.Font, FontStyle.Bold) };
            btnCancel = new Button() { Text = "Cancelar", Location = new Point(450, 12), Width = 90, Height = 30 };
            btnInstall.Click += BtnInstall_Click;
            btnCancel.Click += delegate(object s, EventArgs e) { this.Close(); };
            pnlBottom.Controls.AddRange(new Control[] { btnInstall, btnCancel });
            this.Controls.Add(pnlBottom);
        }

        private void BtnInstall_Click(object sender, EventArgs e)
        {
            string targetDir = txtInstallPath.Text.Trim();
            if (string.IsNullOrEmpty(targetDir)) return;

            btnInstall.Enabled = false;
            btnBrowse.Enabled = false;
            txtInstallPath.ReadOnly = true;
            progressBar.Visible = true;
            progressBar.Value = 15;
            lblStatus.Text = "Criando diretorios de instalacao...";

            try
            {
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                progressBar.Value = 35;
                lblStatus.Text = "Copiando binarios e dependencias do DupFinder-Neo...";

                string sourceDir = AppDomain.CurrentDomain.BaseDirectory;
                string exeName = "dupfinder-neo.exe";
                string sourceExe = Path.Combine(sourceDir, exeName);
                string targetExe = Path.Combine(targetDir, exeName);

                if (File.Exists(sourceExe))
                {
                    File.Copy(sourceExe, targetExe, true);
                }

                // Copy documentation and icon
                string[] filesToCopy = new string[] { "readme.txt", "LICENSE.TXT", "dupf.ico" };
                foreach (string fn in filesToCopy)
                {
                    string sf = Path.Combine(sourceDir, fn);
                    if (File.Exists(sf))
                    {
                        File.Copy(sf, Path.Combine(targetDir, fn), true);
                    }
                }

                progressBar.Value = 65;
                lblStatus.Text = "Criando atalhos no Windows...";

                if (cbDesktopShortcut.Checked)
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(Path.Combine(desktop, "DupFinder-Neo.lnk"), targetExe, "Localizador e Limpador de Duplicatas e Pastas Vazias");
                }

                if (cbStartMenuShortcut.Checked)
                {
                    string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
                    CreateShortcut(Path.Combine(startMenu, "DupFinder-Neo.lnk"), targetExe, "Localizador e Limpador de Duplicatas e Pastas Vazias");
                }

                progressBar.Value = 85;
                lblStatus.Text = "Registrando informacoes no Painel de Controle...";
                RegisterUninstall(targetDir, targetExe);

                progressBar.Value = 100;
                lblStatus.Text = "Instalacao concluida com sucesso!";

                MessageBox.Show("O DupFinder-Neo foi instalado com sucesso no seu sistema!", "Instalacao Concluida", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (cbLaunchAfter.Checked && File.Exists(targetExe))
                {
                    Process.Start(targetExe);
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro durante a instalacao: " + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnInstall.Enabled = true;
                btnBrowse.Enabled = true;
                txtInstallPath.ReadOnly = false;
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string description)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                    shortcut.Description = description;
                    shortcut.Save();
                }
            }
            catch { }
        }

        private static void RegisterUninstall(string installDir, string exePath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\DupFinderNeo"))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "DupFinder-Neo (Duplicate Files & Empty Folders Cleaner)");
                        key.SetValue("DisplayVersion", "0.9.0");
                        key.SetValue("Publisher", "DupFinder Neo Project");
                        key.SetValue("InstallLocation", installDir);
                        key.SetValue("DisplayIcon", exePath);
                        key.SetValue("UninstallString", "cmd.exe /c rd /s /q \"" + installDir + "\"");
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }
}

