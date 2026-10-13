using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DupFinderNeo
{
    public class WatcherFileEntry
    {
        public string FilePath { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public ulong SampleHash { get; set; }
        public string FullSha256 { get; set; }
    }

    public static class WatcherProgram
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length > 0)
            {
                string cmd = args[0].Trim().ToLowerInvariant();
                if (cmd == "--stop" || cmd == "/stop")
                {
                    StopRunningWatchers();
                    return;
                }
                if (cmd == "--uninstall" || cmd == "/uninstall")
                {
                    StopRunningWatchers();
                    UnregisterStartup();
                    return;
                }
                if (cmd == "--install" || cmd == "/install")
                {
                    RegisterStartup();
                }
            }

            // Ensure single instance of watcher
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "DupFinderNeoWatcher_Mutex", out createdNew))
            {
                if (!createdNew)
                {
                    // Already running
                    return;
                }

                WatcherApplicationContext context = new WatcherApplicationContext();
                Application.Run(context);
            }
        }

        public static void StopRunningWatchers()
        {
            try
            {
                int currentId = Process.GetCurrentProcess().Id;
                Process[] procs = Process.GetProcessesByName("dupfinder-watcher");
                foreach (var p in procs)
                {
                    if (p.Id != currentId)
                    {
                        try { p.Kill(); } catch { }
                    }
                }
            }
            catch { }
        }

        public static void RegisterStartup()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        key.SetValue("DupFinderNeoWatcher", "\"" + exePath + "\" --bg");
                    }
                }
            }
            catch { }
        }

        public static void UnregisterStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        key.DeleteValue("DupFinderNeoWatcher", false);
                    }
                }
            }
            catch { }
        }
    }

    public class WatcherApplicationContext : ApplicationContext
    {
        private NotifyIcon notifyIcon;
        private List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        private Dictionary<long, List<WatcherFileEntry>> indexBySize = new Dictionary<long, List<WatcherFileEntry>>();
        private object syncLock = new object();
        private HashSet<string> recentAlerts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public WatcherApplicationContext()
        {
            InitializeTray();
            StartMonitoring();
        }

        private void InitializeTray()
        {
            ContextMenu contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add("DupFinder-Neo v1.1 (by @heitorsecurity)", (s, e) => OpenDupFinder());
            contextMenu.MenuItems[0].DefaultItem = true;
            contextMenu.MenuItems.Add("-");
            contextMenu.MenuItems.Add("Status do Monitor", (s, e) => ShowStatus());
            contextMenu.MenuItems.Add("Abrir Localizador Completo", (s, e) => OpenDupFinder());
            contextMenu.MenuItems.Add("-");
            contextMenu.MenuItems.Add("Encerrar Monitor", (s, e) => ExitWatcher());

            notifyIcon = new NotifyIcon();
            notifyIcon.Text = "DupFinder-Neo v1.1 - Monitor Ativo (by @heitorsecurity)";
            try
            {
                notifyIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                notifyIcon.Icon = SystemIcons.Information;
            }
            notifyIcon.ContextMenu = contextMenu;
            notifyIcon.Visible = true;

            notifyIcon.BalloonTipClicked += (s, e) =>
            {
                // Last alert click handling
                if (lastAlertPair != null)
                {
                    ShowDuplicateDialog(lastAlertPair.Item1, lastAlertPair.Item2);
                }
            };

            notifyIcon.DoubleClick += (s, e) => OpenDupFinder();
        }

        private Tuple<string, string> lastAlertPair = null;

        private void StartMonitoring()
        {
            // Initial folders to watch: User downloads, documents, desktop, pictures, videos
            List<string> pathsToWatch = new List<string>();
            AddSafePath(pathsToWatch, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            AddSafePath(pathsToWatch, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            AddSafePath(pathsToWatch, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                string downloads = Path.Combine(userProfile, "Downloads");
                AddSafePath(pathsToWatch, downloads);
                string pics = Path.Combine(userProfile, "Pictures");
                AddSafePath(pathsToWatch, pics);
            }

            // Start initial background indexing
            ThreadPool.QueueUserWorkItem(state => BuildInitialIndex(pathsToWatch));

            // Setup FileSystemWatchers
            foreach (string p in pathsToWatch)
            {
                try
                {
                    if (Directory.Exists(p))
                    {
                        FileSystemWatcher fsw = new FileSystemWatcher(p);
                        fsw.IncludeSubdirectories = true;
                        fsw.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite;
                        fsw.Created += OnFileEvent;
                        fsw.Changed += OnFileEvent;
                        fsw.EnableRaisingEvents = true;
                        watchers.Add(fsw);
                    }
                }
                catch { }
            }
        }

        private void AddSafePath(List<string> list, string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && !list.Contains(path))
            {
                list.Add(path);
            }
        }

        private void BuildInitialIndex(List<string> paths)
        {
            foreach (string dir in paths)
            {
                try
                {
                    IndexDirectoryRecursive(dir);
                }
                catch { }
            }
        }

        private void IndexDirectoryRecursive(string dir)
        {
            try
            {
                string[] files = Directory.GetFiles(dir);
                foreach (string file in files)
                {
                    try
                    {
                        FileInfo fi = new FileInfo(file);
                        if (fi.Length > 0 && !IsIgnored(fi.Name))
                        {
                            AddFileToIndex(fi.FullName, fi.Length, fi.LastWriteTimeUtc);
                        }
                    }
                    catch { }
                }

                string[] subDirs = Directory.GetDirectories(dir);
                foreach (string sub in subDirs)
                {
                    if (!IsSystemDir(sub))
                    {
                        IndexDirectoryRecursive(sub);
                    }
                }
            }
            catch { }
        }

        private bool IsSystemDir(string dir)
        {
            string lower = dir.ToLowerInvariant();
            return lower.Contains("\\windows") || lower.Contains("\\appdata\\local\\microsoft") ||
                   lower.Contains("\\$recycle.bin") || lower.Contains("\\system volume information");
        }

        private bool IsIgnored(string name)
        {
            string lower = name.ToLowerInvariant();
            return lower.EndsWith(".tmp") || lower.EndsWith(".crdownload") || lower.EndsWith(".part") ||
                   lower.StartsWith("~$") || lower == "desktop.ini" || lower == "thumbs.db";
        }

        private void AddFileToIndex(string path, long size, DateTime modified)
        {
            lock (syncLock)
            {
                if (!indexBySize.ContainsKey(size))
                {
                    indexBySize[size] = new List<WatcherFileEntry>();
                }

                var list = indexBySize[size];
                // Check if already in list
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].FilePath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        list[i].LastModified = modified;
                        return;
                    }
                }

                list.Add(new WatcherFileEntry
                {
                    FilePath = path,
                    Size = size,
                    LastModified = modified,
                    SampleHash = 0,
                    FullSha256 = null
                });
            }
        }

        private void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Created && e.ChangeType != WatcherChangeTypes.Changed)
                return;

            string filePath = e.FullPath;
            if (IsIgnored(Path.GetFileName(filePath))) return;

            // Debounce in background thread
            ThreadPool.QueueUserWorkItem(state => ProcessNewOrChangedFile(filePath));
        }

        private void ProcessNewOrChangedFile(string filePath)
        {
            try
            {
                // Wait for write lock release (file copy might still be in progress)
                Thread.Sleep(1200);

                if (!File.Exists(filePath)) return;

                FileInfo fi = null;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        fi = new FileInfo(filePath);
                        if (fi.Length > 0)
                        {
                            using (FileStream fs = fi.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                // Successfully opened for reading
                                break;
                            }
                        }
                    }
                    catch
                    {
                        Thread.Sleep(800);
                    }
                }

                if (fi == null || fi.Length == 0) return;

                long size = fi.Length;
                WatcherFileEntry match = null;

                lock (syncLock)
                {
                    if (indexBySize.ContainsKey(size))
                    {
                        var candidates = indexBySize[size];
                        foreach (var c in candidates)
                        {
                            if (!string.Equals(c.FilePath, filePath, StringComparison.OrdinalIgnoreCase) && File.Exists(c.FilePath))
                            {
                                match = c;
                                break;
                            }
                        }
                    }
                }

                if (match != null)
                {
                    // Verify sample hash first
                    ulong sample1 = ComputeSampleHash(filePath, size);
                    if (match.SampleHash == 0) match.SampleHash = ComputeSampleHash(match.FilePath, size);

                    if (sample1 == match.SampleHash && sample1 != 0)
                    {
                        // Verify full SHA-256 for 100% accuracy
                        string sha1 = ComputeFullHash(filePath);
                        if (match.FullSha256 == null) match.FullSha256 = ComputeFullHash(match.FilePath);

                        if (!string.IsNullOrEmpty(sha1) && string.Equals(sha1, match.FullSha256, StringComparison.OrdinalIgnoreCase))
                        {
                            // Trigger duplicate alert!
                            string alertKey = filePath + "|||" + match.FilePath;
                            lock (recentAlerts)
                            {
                                if (recentAlerts.Contains(alertKey)) return;
                                recentAlerts.Add(alertKey);
                            }

                            lastAlertPair = new Tuple<string, string>(filePath, match.FilePath);
                            TriggerDuplicateNotification(filePath, match.FilePath, size);
                        }
                    }
                }

                // Add or update in index
                AddFileToIndex(filePath, size, fi.LastWriteTimeUtc);
            }
            catch { }
        }

        private void TriggerDuplicateNotification(string newFile, string existingFile, long size)
        {
            string fileName = Path.GetFileName(newFile);
            string sizeStr = FormatBytes(size);

            if (notifyIcon != null)
            {
                notifyIcon.ShowBalloonTip(
                    9000,
                    "DupFinder-Neo v1.1: Arquivo Duplicado!",
                    string.Format("Voce duplicou um arquivo existente ({0}):\r\n'{1}'\r\nClique aqui para ver opcoes.", sizeStr, fileName),
                    ToolTipIcon.Warning
                );
            }
        }

        private void ShowDuplicateDialog(string newFile, string existingFile)
        {
            DuplicateAlertForm form = new DuplicateAlertForm(newFile, existingFile);
            form.ShowDialog();
        }

        private ulong ComputeSampleHash(string path, long size)
        {
            try
            {
                byte[] buf = new byte[8192];
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    ulong hash = 14695981039346656037UL;
                    int r1 = fs.Read(buf, 0, (int)Math.Min(4096, size));
                    for (int i = 0; i < r1; i++) { hash ^= buf[i]; hash *= 1099511628211UL; }
                    if (size > 4096)
                    {
                        fs.Seek(Math.Max(0, size - 4096), SeekOrigin.Begin);
                        int r2 = fs.Read(buf, 0, 4096);
                        for (int i = 0; i < r2; i++) { hash ^= buf[i]; hash *= 1099511628211UL; }
                    }
                    return hash;
                }
            }
            catch { return 0; }
        }

        private string ComputeFullHash(string path)
        {
            try
            {
                using (SHA256 sha = SHA256.Create())
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] hash = sha.ComputeHash(fs);
                    StringBuilder sb = new StringBuilder(hash.Length * 2);
                    foreach (byte b in hash) sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }
            }
            catch { return null; }
        }

        private string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double val = bytes;
            int u = 0;
            while (val >= 1024 && u < units.Length - 1) { val /= 1024; u++; }
            return string.Format("{0:0.##} {1}", val, units[u]);
        }

        private void ShowStatus()
        {
            int totalIndexed = 0;
            lock (syncLock)
            {
                foreach (var kv in indexBySize) totalIndexed += kv.Value.Count;
            }

            MessageBox.Show(
                string.Format("DupFinder-Neo v1.1 - Monitor de Arquivos (by @heitorsecurity)\r\n\r\n" +
                              "Status: Ativo e monitorando em tempo real\r\n" +
                              "Arquivos indexados no cache: {0:N0}\r\n" +
                              "Diretorios monitorados: {1}\r\n\r\n" +
                              "Quando qualquer arquivo duplicado for criado ou copiado no sistema, voce sera avisado na hora!",
                              totalIndexed, watchers.Count),
                "Status do Monitor DupFinder-Neo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OpenDupFinder()
        {
            try
            {
                string dir = Path.GetDirectoryName(Application.ExecutablePath);
                string mainExe = Path.Combine(dir, "dupfinder-neo.exe");
                if (File.Exists(mainExe))
                {
                    Process.Start(mainExe);
                }
            }
            catch { }
        }

        private void ExitWatcher()
        {
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            ExitThread();
        }
    }

    public class DuplicateAlertForm : Form
    {
        private string newFilePath;
        private string originalFilePath;

        public DuplicateAlertForm(string newFile, string originalFile)
        {
            this.newFilePath = newFile;
            this.originalFilePath = originalFile;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "DupFinder-Neo v1.1 - Alerta de Arquivo Duplicado (by @heitorsecurity)";
            this.Size = new Size(680, 460);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            TableLayoutPanel mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.RowCount = 5;
            mainLayout.ColumnCount = 1;
            mainLayout.Padding = new Padding(12);
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));  // New file box
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));  // Original file box
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Accuracy status
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // Action buttons
            this.Controls.Add(mainLayout);

            // Header banner
            Label lblHeader = new Label();
            lblHeader.Text = "⚠️ Arquivo Duplicado Detectado no Computador!";
            lblHeader.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblHeader.ForeColor = Color.DarkRed;
            lblHeader.Dock = DockStyle.Fill;
            lblHeader.TextAlign = ContentAlignment.MiddleLeft;
            mainLayout.Controls.Add(lblHeader, 0, 0);

            // New File GroupBox
            GroupBox gbNew = new GroupBox();
            gbNew.Text = "Arquivo Recem-Criado / Copiado (Possivel Copia Duplicada):";
            gbNew.Dock = DockStyle.Fill;
            gbNew.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            mainLayout.Controls.Add(gbNew, 0, 1);

            TableLayoutPanel pnlNew = new TableLayoutPanel();
            pnlNew.Dock = DockStyle.Fill;
            pnlNew.Padding = new Padding(8);
            pnlNew.RowCount = 2;
            pnlNew.ColumnCount = 2;
            pnlNew.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            pnlNew.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            gbNew.Controls.Add(pnlNew);

            Label lblNewInfo = new Label();
            lblNewInfo.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            lblNewInfo.Dock = DockStyle.Fill;
            lblNewInfo.Text = string.Format("Nome: {0}\r\nCaminho: {1}\r\nTamanho: {2}\r\nModificado: {3}",
                Path.GetFileName(newFilePath),
                newFilePath,
                GetFileSizeStr(newFilePath),
                File.Exists(newFilePath) ? File.GetLastWriteTime(newFilePath).ToString("dd/MM/yyyy HH:mm:ss") : "-");
            pnlNew.Controls.Add(lblNewInfo, 0, 0);

            FlowLayoutPanel pnlNewBtns = new FlowLayoutPanel();
            pnlNewBtns.Dock = DockStyle.Fill;
            pnlNewBtns.FlowDirection = FlowDirection.TopDown;
            Button btnOpenNew = new Button { Text = "Abrir Arquivo", AutoSize = true, Width = 150 };
            Button btnOpenNewDir = new Button { Text = "Abrir na Pasta", AutoSize = true, Width = 150 };
            btnOpenNew.Click += (s, e) => OpenFile(newFilePath);
            btnOpenNewDir.Click += (s, e) => OpenInExplorer(newFilePath);
            pnlNewBtns.Controls.AddRange(new Control[] { btnOpenNew, btnOpenNewDir });
            pnlNew.Controls.Add(pnlNewBtns, 1, 0);

            // Original File GroupBox
            GroupBox gbOrig = new GroupBox();
            gbOrig.Text = "Arquivo Original Existente no Sistema:";
            gbOrig.Dock = DockStyle.Fill;
            gbOrig.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            mainLayout.Controls.Add(gbOrig, 0, 2);

            TableLayoutPanel pnlOrig = new TableLayoutPanel();
            pnlOrig.Dock = DockStyle.Fill;
            pnlOrig.Padding = new Padding(8);
            pnlOrig.RowCount = 2;
            pnlOrig.ColumnCount = 2;
            pnlOrig.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            pnlOrig.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            gbOrig.Controls.Add(pnlOrig);

            Label lblOrigInfo = new Label();
            lblOrigInfo.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            lblOrigInfo.Dock = DockStyle.Fill;
            lblOrigInfo.Text = string.Format("Nome: {0}\r\nCaminho: {1}\r\nTamanho: {2}\r\nModificado: {3}",
                Path.GetFileName(originalFilePath),
                originalFilePath,
                GetFileSizeStr(originalFilePath),
                File.Exists(originalFilePath) ? File.GetLastWriteTime(originalFilePath).ToString("dd/MM/yyyy HH:mm:ss") : "-");
            pnlOrig.Controls.Add(lblOrigInfo, 0, 0);

            FlowLayoutPanel pnlOrigBtns = new FlowLayoutPanel();
            pnlOrigBtns.Dock = DockStyle.Fill;
            pnlOrigBtns.FlowDirection = FlowDirection.TopDown;
            Button btnOpenOrig = new Button { Text = "Abrir Arquivo", AutoSize = true, Width = 150 };
            Button btnOpenOrigDir = new Button { Text = "Abrir na Pasta", AutoSize = true, Width = 150 };
            btnOpenOrig.Click += (s, e) => OpenFile(originalFilePath);
            btnOpenOrigDir.Click += (s, e) => OpenInExplorer(originalFilePath);
            pnlOrigBtns.Controls.AddRange(new Control[] { btnOpenOrig, btnOpenOrigDir });
            pnlOrig.Controls.Add(pnlOrigBtns, 1, 0);

            // Accuracy Status
            Label lblStatus = new Label();
            lblStatus.Text = "✓ Verificacao Concluida: Conteudo binario e hash SHA-256 100% identicos.";
            lblStatus.ForeColor = Color.DarkGreen;
            lblStatus.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            mainLayout.Controls.Add(lblStatus, 0, 3);

            // Action Buttons
            FlowLayoutPanel pnlActions = new FlowLayoutPanel();
            pnlActions.Dock = DockStyle.Fill;
            pnlActions.FlowDirection = FlowDirection.RightToLeft;

            Button btnClose = new Button { Text = "Fechar", DialogResult = DialogResult.OK, Width = 100, Height = 32 };
            Button btnDeleteDuplicate = new Button { Text = "🗑️ Excluir Copia Recente", BackColor = Color.MistyRose, Width = 180, Height = 32 };
            Button btnOpenMainApp = new Button { Text = "Abrir DupFinder-Neo", Width = 150, Height = 32 };

            btnDeleteDuplicate.Click += (s, e) =>
            {
                if (MessageBox.Show("Deseja realmente excluir a copia duplicada recente?\r\n\r\n" + newFilePath,
                    "Confirmar Exclusao", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        File.Delete(newFilePath);
                        MessageBox.Show("Arquivo duplicado excluido com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Nao foi possivel excluir o arquivo: " + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            btnOpenMainApp.Click += (s, e) =>
            {
                try
                {
                    string dir = Path.GetDirectoryName(Application.ExecutablePath);
                    string mainExe = Path.Combine(dir, "dupfinder-neo.exe");
                    if (File.Exists(mainExe)) Process.Start(mainExe);
                }
                catch { }
            };

            pnlActions.Controls.AddRange(new Control[] { btnClose, btnDeleteDuplicate, btnOpenMainApp });
            mainLayout.Controls.Add(pnlActions, 0, 4);
        }

        private string GetFileSizeStr(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    long len = new FileInfo(path).Length;
                    string[] units = { "B", "KB", "MB", "GB" };
                    double val = len;
                    int u = 0;
                    while (val >= 1024 && u < units.Length - 1) { val /= 1024; u++; }
                    return string.Format("{0:0.##} {1} ({2:N0} bytes)", val, units[u], len);
                }
            }
            catch { }
            return "-";
        }

        private void OpenFile(string path)
        {
            try
            {
                if (File.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show("Erro ao abrir arquivo: " + ex.Message); }
        }

        private void OpenInExplorer(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    Process.Start("explorer.exe", "/select,\"" + path + "\"");
                }
                else if (Directory.Exists(path))
                {
                    Process.Start("explorer.exe", "\"" + path + "\"");
                }
            }
            catch (Exception ex) { MessageBox.Show("Erro ao abrir pasta: " + ex.Message); }
        }
    }
}
