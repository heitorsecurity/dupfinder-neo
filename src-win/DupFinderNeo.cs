using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DupFinderNeo
{
    public enum ScanMode
    {
        DuplicatesOnly = 0,
        Both = 1,
        EmptyDirsOnly = 2
    }

    public class FileCandidate
    {
        public string FullPath { get; set; }
        public string FileName { get; set; }
        public string DirectoryPath { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public ulong SampleHash { get; set; }
        public string FullSha256 { get; set; }
    }

    public class DuplicateGroup
    {
        public long Size { get; set; }
        public string Hash { get; set; }
        public List<FileCandidate> Files { get; set; }

        public DuplicateGroup()
        {
            Files = new List<FileCandidate>();
        }
    }

    public class EmptyDirectoryInfo
    {
        public string Path { get; set; }
        public bool IsSystemProtected { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class CommonFolderSuggestion
    {
        public string DuplicateFolder { get; set; }
        public string PrimaryFolder { get; set; }
        public int DuplicateCount { get; set; }
        public long WastedBytes { get; set; }
        public bool IsCompleteDuplicate { get; set; }
        public string Recommendation { get; set; }
    }

    public static class SystemSafety
    {
        private static readonly string[] ProtectedPatterns = new string[]
        {
            "\\windows",
            "\\system32",
            "\\syswow64",
            "\\winsxs",
            "\\program files",
            "\\program files (x86)",
            "\\programdata",
            "\\$recycle.bin",
            "\\system volume information",
            "\\recovery",
            "\\$winreagent",
            "\\boot",
            "\\appdata\\local\\microsoft",
            "\\appdata\\roaming\\microsoft"
        };

        public static bool IsSystemProtected(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;
            try
            {
                string norm = path.Trim().ToLowerInvariant().Replace('/', '\\');
                while (norm.EndsWith("\\")) norm = norm.Substring(0, norm.Length - 1);

                // Drive roots like C:, D:
                if (norm.Length <= 3 && norm.Contains(":")) return true;

                foreach (string pat in ProtectedPatterns)
                {
                    if (norm.Contains(pat)) return true;
                }
            }
            catch { return true; }
            return false;
        }
    }

    public class Engine
    {
        public ScanMode Mode { get; set; }
        public bool ProtectSystemDirs { get; set; }
        public bool RecurseSubdirectories { get; set; }
        public bool IncludeHidden { get; set; }
        public long MinSize { get; set; }
        public long MaxSize { get; set; }

        public List<string> SearchPaths { get; set; }

        public List<DuplicateGroup> DuplicateGroups { get; private set; }
        public List<EmptyDirectoryInfo> EmptyDirectories { get; private set; }
        public List<CommonFolderSuggestion> FolderSuggestions { get; private set; }

        public event Action<string> OnStatusUpdate;
        public event Action<int, int> OnProgressUpdate;

        public Engine()
        {
            Mode = ScanMode.Both;
            ProtectSystemDirs = true;
            RecurseSubdirectories = true;
            IncludeHidden = false;
            MinSize = 1;
            MaxSize = 0;
            SearchPaths = new List<string>();
            DuplicateGroups = new List<DuplicateGroup>();
            EmptyDirectories = new List<EmptyDirectoryInfo>();
            FolderSuggestions = new List<CommonFolderSuggestion>();
        }

        public void Execute(CancellationToken token)
        {
            DuplicateGroups.Clear();
            EmptyDirectories.Clear();
            FolderSuggestions.Clear();

            List<string> scannedDirs = new List<string>();
            List<FileCandidate> allFiles = new List<FileCandidate>();

            if (OnStatusUpdate != null) OnStatusUpdate("Fase 1: Varrendo diretorios e catalogando arquivos...");

            foreach (string root in SearchPaths)
            {
                if (token.IsCancellationRequested) return;
                if (!Directory.Exists(root)) continue;

                Queue<string> queue = new Queue<string>();
                queue.Enqueue(root);

                while (queue.Count > 0)
                {
                    if (token.IsCancellationRequested) return;
                    string current = queue.Dequeue();

                    if (ProtectSystemDirs && SystemSafety.IsSystemProtected(current))
                    {
                        continue;
                    }

                    scannedDirs.Add(current);
                    if (OnStatusUpdate != null) OnStatusUpdate("Varrendo: " + current);

                    try
                    {
                        string[] files = Directory.GetFiles(current);
                        foreach (string file in files)
                        {
                            try
                            {
                                FileInfo fi = new FileInfo(file);
                                if (!IncludeHidden && (fi.Attributes & FileAttributes.Hidden) != 0) continue;
                                if (fi.Length < MinSize) continue;
                                if (MaxSize > 0 && fi.Length > MaxSize) continue;

                                FileCandidate fc = new FileCandidate();
                                fc.FullPath = fi.FullName;
                                fc.FileName = fi.Name;
                                fc.DirectoryPath = fi.DirectoryName;
                                fc.Size = fi.Length;
                                fc.LastModified = fi.LastWriteTime;
                                allFiles.Add(fc);
                            }
                            catch { }
                        }

                        if (RecurseSubdirectories)
                        {
                            string[] subdirs = Directory.GetDirectories(current);
                            foreach (string sub in subdirs)
                            {
                                try
                                {
                                    DirectoryInfo di = new DirectoryInfo(sub);
                                    if (!IncludeHidden && (di.Attributes & FileAttributes.Hidden) != 0) continue;
                                    if (ProtectSystemDirs && SystemSafety.IsSystemProtected(sub)) continue;
                                    queue.Enqueue(sub);
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
            }

            // Empty directories processing
            if (Mode == ScanMode.Both || Mode == ScanMode.EmptyDirsOnly)
            {
                if (OnStatusUpdate != null) OnStatusUpdate("Fase 2: Identificando pastas vazias nao-sistema...");
                for (int i = scannedDirs.Count - 1; i >= 0; i--)
                {
                    if (token.IsCancellationRequested) return;
                    string d = scannedDirs[i];
                    try
                    {
                        if (Directory.Exists(d))
                        {
                            string[] files = Directory.GetFiles(d);
                            string[] subs = Directory.GetDirectories(d);
                            if (files.Length == 0 && subs.Length == 0)
                            {
                                EmptyDirectoryInfo ed = new EmptyDirectoryInfo();
                                ed.Path = d;
                                ed.IsSystemProtected = SystemSafety.IsSystemProtected(d);
                                ed.LastModified = Directory.GetLastWriteTime(d);
                                EmptyDirectories.Add(ed);
                            }
                        }
                    }
                    catch { }
                }
            }

            if (Mode == ScanMode.EmptyDirsOnly)
            {
                if (OnStatusUpdate != null) OnStatusUpdate("Concluido! Pastas vazias localizadas.");
                return;
            }

            // Duplicates multi-stage pipeline
            if (OnStatusUpdate != null) OnStatusUpdate("Fase 3: Filtrando candidatos por tamanho exato...");
            Dictionary<long, List<FileCandidate>> sizeGroups = new Dictionary<long, List<FileCandidate>>();
            foreach (var f in allFiles)
            {
                if (!sizeGroups.ContainsKey(f.Size))
                    sizeGroups[f.Size] = new List<FileCandidate>();
                sizeGroups[f.Size].Add(f);
            }

            List<FileCandidate> candidateFiles = new List<FileCandidate>();
            foreach (var kv in sizeGroups)
            {
                if (kv.Value.Count > 1)
                {
                    candidateFiles.AddRange(kv.Value);
                }
            }

            if (OnStatusUpdate != null) OnStatusUpdate(string.Format("Fase 4: Pre-hashing rapido (Prefix/Suffix) de {0} candidatos...", candidateFiles.Count));
            int processed = 0;
            byte[] buffer = new byte[8192];

            for (int i = 0; i < candidateFiles.Count; i++)
            {
                if (token.IsCancellationRequested) return;
                var f = candidateFiles[i];
                f.SampleHash = ComputeSampleHash(f.FullPath, f.Size, buffer);
                processed++;
                if (processed % 50 == 0 || processed == candidateFiles.Count)
                {
                    if (OnProgressUpdate != null) OnProgressUpdate(processed, candidateFiles.Count);
                }
            }

            // Group by Size + SampleHash
            Dictionary<string, List<FileCandidate>> sampleGroups = new Dictionary<string, List<FileCandidate>>();
            foreach (var f in candidateFiles)
            {
                string key = f.Size + "_" + f.SampleHash;
                if (!sampleGroups.ContainsKey(key))
                    sampleGroups[key] = new List<FileCandidate>();
                sampleGroups[key].Add(f);
            }

            List<FileCandidate> hashCandidates = new List<FileCandidate>();
            foreach (var kv in sampleGroups)
            {
                if (kv.Value.Count > 1)
                {
                    hashCandidates.AddRange(kv.Value);
                }
            }

            if (OnStatusUpdate != null) OnStatusUpdate(string.Format("Fase 5: Validacao rigorosa SHA-256 (100% acuracia) de {0} arquivos...", hashCandidates.Count));
            processed = 0;
            using (SHA256 sha = SHA256.Create())
            {
                for (int i = 0; i < hashCandidates.Count; i++)
                {
                    if (token.IsCancellationRequested) return;
                    var f = hashCandidates[i];
                    f.FullSha256 = ComputeFullHash(f.FullPath, sha, buffer);
                    processed++;
                    if (processed % 20 == 0 || processed == hashCandidates.Count)
                    {
                        if (OnProgressUpdate != null) OnProgressUpdate(processed, hashCandidates.Count);
                    }
                }
            }

            // Group by (Size + FullSha256)
            Dictionary<string, List<FileCandidate>> finalGroups = new Dictionary<string, List<FileCandidate>>();
            foreach (var f in hashCandidates)
            {
                if (string.IsNullOrEmpty(f.FullSha256)) continue;
                string key = f.Size + "_" + f.FullSha256;
                if (!finalGroups.ContainsKey(key))
                    finalGroups[key] = new List<FileCandidate>();
                finalGroups[key].Add(f);
            }

            foreach (var kv in finalGroups)
            {
                if (kv.Value.Count > 1)
                {
                    DuplicateGroup dg = new DuplicateGroup();
                    dg.Size = kv.Value[0].Size;
                    dg.Hash = kv.Value[0].FullSha256;
                    dg.Files = kv.Value;
                    DuplicateGroups.Add(dg);
                }
            }

            // Sort groups descending by size
            DuplicateGroups.Sort(delegate(DuplicateGroup a, DuplicateGroup b) { return b.Size.CompareTo(a.Size); });

            // Common folders analysis
            if (OnStatusUpdate != null) OnStatusUpdate("Fase 6: Analisando pastas em comum e gerando sugestoes...");
            AnalyzeCommonFolders();

            if (OnStatusUpdate != null) OnStatusUpdate("Busca finalizada com sucesso!");
        }

        private static ulong ComputeSampleHash(string path, long size, byte[] buf)
        {
            if (size == 0) return 0;
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    ulong hash = 14695981039346656037UL;
                    int read1 = fs.Read(buf, 0, (int)Math.Min(4096, size));
                    for (int i = 0; i < read1; i++)
                    {
                        hash ^= buf[i];
                        hash *= 1099511628211UL;
                    }

                    if (size > 8192)
                    {
                        fs.Seek(size - 4096, SeekOrigin.Begin);
                        int read2 = fs.Read(buf, 0, 4096);
                        for (int i = 0; i < read2; i++)
                        {
                            hash ^= buf[i];
                            hash *= 1099511628211UL;
                        }
                    }
                    return hash;
                }
            }
            catch { return 0; }
        }

        private static string ComputeFullHash(string path, SHA256 sha, byte[] buf)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536))
                {
                    byte[] hash = sha.ComputeHash(fs);
                    StringBuilder sb = new StringBuilder(hash.Length * 2);
                    foreach (byte b in hash) sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }
            }
            catch { return null; }
        }

        private void AnalyzeCommonFolders()
        {
            Dictionary<string, HashSet<string>> folderFiles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, long> folderWasted = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in DuplicateGroups)
            {
                foreach (var f in group.Files)
                {
                    if (!folderFiles.ContainsKey(f.DirectoryPath))
                    {
                        folderFiles[f.DirectoryPath] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        folderWasted[f.DirectoryPath] = 0;
                    }
                    folderFiles[f.DirectoryPath].Add(f.FileName);
                    folderWasted[f.DirectoryPath] += group.Size;
                }
            }

            foreach (var kvA in folderFiles)
            {
                string dirA = kvA.Key;
                var filesA = kvA.Value;

                foreach (var kvB in folderFiles)
                {
                    if (string.Equals(dirA, kvB.Key, StringComparison.OrdinalIgnoreCase)) continue;
                    string dirB = kvB.Key;
                    var filesB = kvB.Value;

                    int matchCount = 0;
                    foreach (var fn in filesA)
                    {
                        if (filesB.Contains(fn)) matchCount++;
                    }

                    if (matchCount > 0 && (matchCount == filesA.Count || matchCount >= 3))
                    {
                        bool isComplete = (matchCount == filesA.Count);
                        string rec;
                        if (isComplete)
                        {
                            rec = string.Format("Todos os {0} arquivos duplicados desta pasta existem em '{1}'. Sugere-se excluir as copias desta pasta.", matchCount, dirB);
                        }
                        else
                        {
                            rec = string.Format("{0} arquivos duplicados compartilhados com '{1}'.", matchCount, dirB);
                        }

                        CommonFolderSuggestion sug = new CommonFolderSuggestion();
                        sug.DuplicateFolder = dirA;
                        sug.PrimaryFolder = dirB;
                        sug.DuplicateCount = matchCount;
                        sug.WastedBytes = folderWasted.ContainsKey(dirA) ? folderWasted[dirA] : 0;
                        sug.IsCompleteDuplicate = isComplete;
                        sug.Recommendation = rec;
                        FolderSuggestions.Add(sug);
                        break;
                    }
                }
            }
        }
    }

    public class MainForm : Form
    {
        private ListBox lbPaths;
        private Button btnAddPath, btnRemovePath, btnClearPaths;
        private RadioButton rbDupsOnly, rbBoth, rbEmptyOnly;
        private CheckBox cbProtectSystem, cbRecursive, cbHidden;
        private Button btnStart, btnCancel;
        private ProgressBar progressBar;
        private Label lblStatus;

        private TabControl tabControl;
        private ListView lvDuplicates;
        private ListView lvCommonFolders;
        private ListView lvEmptyDirs;

        private Button btnDeleteSelectedFiles;
        private Button btnOpenFolder;
        private Button btnSelectSuggestedFolder;
        private Button btnDeleteSelectedEmptyDirs;
        private Button btnDeleteAllEmptyDirs;

        private Label lblSummary;

        private Engine engine = new Engine();
        private CancellationTokenSource cts;
        private BackgroundWorker worker;

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "DupFinder-Neo (Windows XP ate 11)";
            this.Size = new Size(1020, 720);
            this.MinimumSize = new Size(880, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            TableLayoutPanel mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.RowCount = 4;
            mainLayout.ColumnCount = 1;
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 185)); // Top options
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Results
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Actions
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));  // Status bar
            this.Controls.Add(mainLayout);

            // GroupBox Configuration
            GroupBox gbConfig = new GroupBox();
            gbConfig.Text = "Configuracoes da Varredura (Windows XP ate Windows 11)";
            gbConfig.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(gbConfig, 0, 0);

            TableLayoutPanel configLayout = new TableLayoutPanel();
            configLayout.Dock = DockStyle.Fill;
            configLayout.ColumnCount = 3;
            configLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            configLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            configLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
            gbConfig.Controls.Add(configLayout);

            // Left: Paths List
            Panel pnlPaths = new Panel();
            pnlPaths.Dock = DockStyle.Fill;
            configLayout.Controls.Add(pnlPaths, 0, 0);

            Label lblPaths = new Label();
            lblPaths.Text = "Diretorios a pesquisar:";
            lblPaths.Dock = DockStyle.Top;
            lblPaths.Height = 20;

            lbPaths = new ListBox();
            lbPaths.Dock = DockStyle.Fill;

            FlowLayoutPanel pnlPathBtns = new FlowLayoutPanel();
            pnlPathBtns.Dock = DockStyle.Bottom;
            pnlPathBtns.Height = 32;

            btnAddPath = new Button(); btnAddPath.Text = "+ Adicionar Pasta..."; btnAddPath.AutoSize = true;
            btnRemovePath = new Button(); btnRemovePath.Text = "Remover"; btnRemovePath.AutoSize = true;
            btnClearPaths = new Button(); btnClearPaths.Text = "Limpar"; btnClearPaths.AutoSize = true;
            pnlPathBtns.Controls.AddRange(new Control[] { btnAddPath, btnRemovePath, btnClearPaths });
            pnlPaths.Controls.AddRange(new Control[] { lbPaths, pnlPathBtns, lblPaths });

            // Middle: Mode selection
            GroupBox gbMode = new GroupBox();
            gbMode.Text = "O que pesquisar logo no inicio:";
            gbMode.Dock = DockStyle.Fill;

            FlowLayoutPanel pnlModes = new FlowLayoutPanel();
            pnlModes.Dock = DockStyle.Fill;
            pnlModes.FlowDirection = FlowDirection.TopDown;

            rbBoth = new RadioButton(); rbBoth.Text = "Pesquisar Duplicatas E Pastas Vazias"; rbBoth.Checked = true; rbBoth.AutoSize = true;
            rbDupsOnly = new RadioButton(); rbDupsOnly.Text = "Apenas Arquivos Duplicados"; rbDupsOnly.AutoSize = true;
            rbEmptyOnly = new RadioButton(); rbEmptyOnly.Text = "Apenas Pastas Vazias"; rbEmptyOnly.AutoSize = true;
            cbProtectSystem = new CheckBox(); cbProtectSystem.Text = "Proteger pastas do sistema (Windows/Apps)"; cbProtectSystem.Checked = true; cbProtectSystem.AutoSize = true; cbProtectSystem.ForeColor = Color.DarkBlue;
            cbRecursive = new CheckBox(); cbRecursive.Text = "Subpastas recursivas"; cbRecursive.Checked = true; cbRecursive.AutoSize = true;
            cbHidden = new CheckBox(); cbHidden.Text = "Incluir ocultos"; cbHidden.Checked = false; cbHidden.AutoSize = true;

            pnlModes.Controls.AddRange(new Control[] { rbBoth, rbDupsOnly, rbEmptyOnly, cbProtectSystem, cbRecursive, cbHidden });
            gbMode.Controls.Add(pnlModes);
            configLayout.Controls.Add(gbMode, 1, 0);

            // Right: Actions
            Panel pnlStart = new Panel();
            pnlStart.Dock = DockStyle.Fill;

            btnStart = new Button();
            btnStart.Text = "INICIAR BUSCA";
            btnStart.Dock = DockStyle.Top;
            btnStart.Height = 48;
            btnStart.BackColor = Color.LightSteelBlue;
            btnStart.Font = new Font(this.Font.FontFamily, 10F, FontStyle.Bold);

            btnCancel = new Button();
            btnCancel.Text = "Cancelar";
            btnCancel.Dock = DockStyle.Top;
            btnCancel.Height = 35;
            btnCancel.Enabled = false;

            pnlStart.Controls.AddRange(new Control[] { btnCancel, btnStart });
            configLayout.Controls.Add(pnlStart, 2, 0);

            // Center: TabControl for results
            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(tabControl, 0, 1);

            // Tab 1: Duplicates
            TabPage tabDups = new TabPage("Arquivos Duplicados");
            lvDuplicates = new ListView();
            lvDuplicates.Dock = DockStyle.Fill;
            lvDuplicates.View = View.Details;
            lvDuplicates.FullRowSelect = true;
            lvDuplicates.CheckBoxes = true;
            lvDuplicates.GridLines = true;
            lvDuplicates.Columns.Add("Arquivo / Grupo", 340);
            lvDuplicates.Columns.Add("Tamanho", 110);
            lvDuplicates.Columns.Add("Diretorio", 380);
            lvDuplicates.Columns.Add("Modificado", 140);
            tabDups.Controls.Add(lvDuplicates);
            tabControl.TabPages.Add(tabDups);

            // Tab 2: Common Folders
            TabPage tabFolders = new TabPage("Pastas em Comum & Sugestoes");
            lvCommonFolders = new ListView();
            lvCommonFolders.Dock = DockStyle.Fill;
            lvCommonFolders.View = View.Details;
            lvCommonFolders.FullRowSelect = true;
            lvCommonFolders.GridLines = true;
            lvCommonFolders.Columns.Add("Pasta com Duplicatas", 320);
            lvCommonFolders.Columns.Add("Pasta Principal / Original", 320);
            lvCommonFolders.Columns.Add("Qtd Duplicatas", 100);
            lvCommonFolders.Columns.Add("Espaco Desperdicado", 130);
            lvCommonFolders.Columns.Add("Sugestao Inteligente", 380);
            tabFolders.Controls.Add(lvCommonFolders);
            tabControl.TabPages.Add(tabFolders);

            // Tab 3: Empty Folders
            TabPage tabEmpty = new TabPage("Pastas Vazias Encontradas");
            lvEmptyDirs = new ListView();
            lvEmptyDirs.Dock = DockStyle.Fill;
            lvEmptyDirs.View = View.Details;
            lvEmptyDirs.FullRowSelect = true;
            lvEmptyDirs.CheckBoxes = true;
            lvEmptyDirs.GridLines = true;
            lvEmptyDirs.Columns.Add("Caminho da Pasta", 600);
            lvEmptyDirs.Columns.Add("Seguranca", 180);
            lvEmptyDirs.Columns.Add("Data Modificacao", 150);
            tabEmpty.Controls.Add(lvEmptyDirs);
            tabControl.TabPages.Add(tabEmpty);

            // Action Toolbar
            FlowLayoutPanel pnlActions = new FlowLayoutPanel();
            pnlActions.Dock = DockStyle.Fill;
            pnlActions.Padding = new Padding(5);

            btnDeleteSelectedFiles = new Button(); btnDeleteSelectedFiles.Text = "Excluir Duplicatas Marcadas"; btnDeleteSelectedFiles.AutoSize = true; btnDeleteSelectedFiles.BackColor = Color.MistyRose;
            btnOpenFolder = new Button(); btnOpenFolder.Text = "Abrir Pasta no Explorer"; btnOpenFolder.AutoSize = true;
            btnSelectSuggestedFolder = new Button(); btnSelectSuggestedFolder.Text = "Marcar Duplicatas da Pasta Sugerida"; btnSelectSuggestedFolder.AutoSize = true;
            btnDeleteSelectedEmptyDirs = new Button(); btnDeleteSelectedEmptyDirs.Text = "Excluir Pastas Vazias Marcadas"; btnDeleteSelectedEmptyDirs.AutoSize = true; btnDeleteSelectedEmptyDirs.BackColor = Color.LightYellow;
            btnDeleteAllEmptyDirs = new Button(); btnDeleteAllEmptyDirs.Text = "Excluir TODAS as Pastas Vazias Seguras"; btnDeleteAllEmptyDirs.AutoSize = true; btnDeleteAllEmptyDirs.BackColor = Color.LavenderBlush;
            lblSummary = new Label(); lblSummary.AutoSize = true; lblSummary.Text = "Pronto para iniciar."; lblSummary.TextAlign = ContentAlignment.MiddleLeft; lblSummary.Padding = new Padding(10, 6, 0, 0);

            pnlActions.Controls.AddRange(new Control[]
            {
                btnDeleteSelectedFiles,
                btnOpenFolder,
                btnSelectSuggestedFolder,
                btnDeleteSelectedEmptyDirs,
                btnDeleteAllEmptyDirs,
                lblSummary
            });
            mainLayout.Controls.Add(pnlActions, 0, 2);

            // Bottom Status bar
            TableLayoutPanel pnlBottom = new TableLayoutPanel();
            pnlBottom.Dock = DockStyle.Fill;
            pnlBottom.ColumnCount = 2;
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

            lblStatus = new Label();
            lblStatus.Text = "Aguardando selecao de pastas...";
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;

            progressBar = new ProgressBar();
            progressBar.Dock = DockStyle.Fill;
            progressBar.Height = 20;

            pnlBottom.Controls.Add(lblStatus, 0, 0);
            pnlBottom.Controls.Add(progressBar, 1, 0);
            mainLayout.Controls.Add(pnlBottom, 0, 3);

            // Hook Events
            btnAddPath.Click += delegate(object s, EventArgs e)
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Selecione uma pasta para pesquisar duplicatas ou pastas vazias:";
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        if (!lbPaths.Items.Contains(fbd.SelectedPath))
                            lbPaths.Items.Add(fbd.SelectedPath);
                    }
                }
            };

            btnRemovePath.Click += delegate(object s, EventArgs e)
            {
                if (lbPaths.SelectedIndex >= 0) lbPaths.Items.RemoveAt(lbPaths.SelectedIndex);
            };

            btnClearPaths.Click += delegate(object s, EventArgs e) { lbPaths.Items.Clear(); };

            btnStart.Click += BtnStart_Click;
            btnCancel.Click += delegate(object s, EventArgs e) { if (cts != null) cts.Cancel(); };

            btnDeleteSelectedFiles.Click += BtnDeleteSelectedFiles_Click;
            btnOpenFolder.Click += BtnOpenFolder_Click;
            btnSelectSuggestedFolder.Click += BtnSelectSuggestedFolder_Click;
            btnDeleteSelectedEmptyDirs.Click += BtnDeleteSelectedEmptyDirs_Click;
            btnDeleteAllEmptyDirs.Click += BtnDeleteAllEmptyDirs_Click;

            // Add default user directory
            string userDocs = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userDocs) && Directory.Exists(userDocs))
            {
                lbPaths.Items.Add(userDocs);
            }
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (lbPaths.Items.Count == 0)
            {
                MessageBox.Show("Adicione ao menos uma pasta para pesquisar.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            engine.SearchPaths.Clear();
            foreach (var item in lbPaths.Items) engine.SearchPaths.Add(item.ToString());

            if (rbDupsOnly.Checked) engine.Mode = ScanMode.DuplicatesOnly;
            else if (rbEmptyOnly.Checked) engine.Mode = ScanMode.EmptyDirsOnly;
            else engine.Mode = ScanMode.Both;

            engine.ProtectSystemDirs = cbProtectSystem.Checked;
            engine.RecurseSubdirectories = cbRecursive.Checked;
            engine.IncludeHidden = cbHidden.Checked;

            btnStart.Enabled = false;
            btnCancel.Enabled = true;
            progressBar.Value = 0;
            lvDuplicates.Items.Clear();
            lvCommonFolders.Items.Clear();
            lvEmptyDirs.Items.Clear();

            cts = new CancellationTokenSource();
            worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;

            engine.OnStatusUpdate += delegate(string msg)
            {
                if (this.InvokeRequired) this.BeginInvoke(new Action(delegate { lblStatus.Text = msg; }));
                else lblStatus.Text = msg;
            };

            engine.OnProgressUpdate += delegate(int cur, int total)
            {
                if (total <= 0) return;
                int pct = (int)((long)cur * 100 / total);
                if (pct < 0) pct = 0; if (pct > 100) pct = 100;
                if (this.InvokeRequired) this.BeginInvoke(new Action(delegate { progressBar.Value = pct; }));
                else progressBar.Value = pct;
            };

            worker.DoWork += delegate(object s, DoWorkEventArgs ea)
            {
                engine.Execute(cts.Token);
            };

            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs ea)
            {
                btnStart.Enabled = true;
                btnCancel.Enabled = false;
                progressBar.Value = 100;

                PopulateResults();
            };

            worker.RunWorkerAsync();
        }

        private void PopulateResults()
        {
            lvDuplicates.BeginUpdate();
            lvDuplicates.Items.Clear();

            long totalWasted = 0;
            int totalDupFiles = 0;

            foreach (var g in engine.DuplicateGroups)
            {
                totalWasted += g.Size * (g.Files.Count - 1);
                totalDupFiles += (g.Files.Count - 1);

                ListViewItem groupItem = new ListViewItem(new string[]
                {
                    string.Format("[Grupo com {0} copias]", g.Files.Count),
                    FormatBytes(g.Size),
                    "Hash: " + (g.Hash != null ? g.Hash.Substring(0, Math.Min(16, g.Hash.Length)) : ""),
                    ""
                });
                groupItem.BackColor = Color.FromArgb(240, 243, 250);
                groupItem.Font = new Font(this.Font, FontStyle.Bold);
                lvDuplicates.Items.Add(groupItem);

                // Files in group
                for (int i = 0; i < g.Files.Count; i++)
                {
                    var f = g.Files[i];
                    ListViewItem fItem = new ListViewItem(new string[]
                    {
                        "    " + f.FileName,
                        FormatBytes(f.Size),
                        f.DirectoryPath,
                        f.LastModified.ToString("yyyy-MM-dd HH:mm")
                    });
                    fItem.Tag = f;
                    // Auto-check duplicates except the first one (primary)
                    if (i > 0) fItem.Checked = true;
                    lvDuplicates.Items.Add(fItem);
                }
            }
            lvDuplicates.EndUpdate();

            // Populate Common Folders
            lvCommonFolders.BeginUpdate();
            lvCommonFolders.Items.Clear();
            foreach (var sug in engine.FolderSuggestions)
            {
                ListViewItem sItem = new ListViewItem(new string[]
                {
                    sug.DuplicateFolder,
                    sug.PrimaryFolder,
                    sug.DuplicateCount.ToString(),
                    FormatBytes(sug.WastedBytes),
                    sug.Recommendation
                });
                sItem.Tag = sug;
                if (sug.IsCompleteDuplicate) sItem.BackColor = Color.FromArgb(235, 250, 235);
                lvCommonFolders.Items.Add(sItem);
            }
            lvCommonFolders.EndUpdate();

            // Populate Empty Dirs
            lvEmptyDirs.BeginUpdate();
            lvEmptyDirs.Items.Clear();
            foreach (var ed in engine.EmptyDirectories)
            {
                ListViewItem eItem = new ListViewItem(new string[]
                {
                    ed.Path,
                    ed.IsSystemProtected ? "Protegido pelo Sistema" : "Seguro para Exclusao",
                    ed.LastModified.ToString("yyyy-MM-dd HH:mm")
                });
                eItem.Tag = ed;
                if (ed.IsSystemProtected)
                {
                    eItem.ForeColor = Color.Red;
                    eItem.BackColor = Color.FromArgb(255, 240, 240);
                }
                else
                {
                    eItem.Checked = true;
                }
                lvEmptyDirs.Items.Add(eItem);
            }
            lvEmptyDirs.EndUpdate();

            lblSummary.Text = string.Format("Resultado: {0} duplicatas ({1} desperdicados) | {2} sugestoes de pastas | {3} pastas vazias.",
                totalDupFiles, FormatBytes(totalWasted), engine.FolderSuggestions.Count, engine.EmptyDirectories.Count);
        }

        private void BtnDeleteSelectedFiles_Click(object sender, EventArgs e)
        {
            List<ListViewItem> toDelete = new List<ListViewItem>();
            foreach (ListViewItem item in lvDuplicates.Items)
            {
                if (item.Checked && item.Tag is FileCandidate)
                {
                    toDelete.Add(item);
                }
            }

            if (toDelete.Count == 0)
            {
                MessageBox.Show("Nenhum arquivo duplicado foi marcado para exclusao.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(string.Format("Deseja realmente excluir permanentemente os {0} arquivos duplicados marcados?", toDelete.Count),
                "Confirmacao de Exclusao", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            int deleted = 0;
            foreach (var item in toDelete)
            {
                FileCandidate f = item.Tag as FileCandidate;
                try
                {
                    if (File.Exists(f.FullPath))
                    {
                        File.Delete(f.FullPath);
                        deleted++;
                        lvDuplicates.Items.Remove(item);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format("Erro ao apagar '{0}': {1}", f.FullPath, ex.Message), "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            MessageBox.Show(string.Format("{0} arquivos foram apagados com sucesso!", deleted), "Concluido", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnOpenFolder_Click(object sender, EventArgs e)
        {
            string dir = null;
            if (tabControl.SelectedTab == tabControl.TabPages[0] && lvDuplicates.SelectedItems.Count > 0)
            {
                var f = lvDuplicates.SelectedItems[0].Tag as FileCandidate;
                if (f != null) dir = f.DirectoryPath;
            }
            else if (tabControl.SelectedTab == tabControl.TabPages[1] && lvCommonFolders.SelectedItems.Count > 0)
            {
                var s = lvCommonFolders.SelectedItems[0].Tag as CommonFolderSuggestion;
                if (s != null) dir = s.DuplicateFolder;
            }
            else if (tabControl.SelectedTab == tabControl.TabPages[2] && lvEmptyDirs.SelectedItems.Count > 0)
            {
                var ed = lvEmptyDirs.SelectedItems[0].Tag as EmptyDirectoryInfo;
                if (ed != null) dir = ed.Path;
            }

            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Process.Start("explorer.exe", dir);
            }
        }

        private void BtnSelectSuggestedFolder_Click(object sender, EventArgs e)
        {
            if (lvCommonFolders.SelectedItems.Count == 0)
            {
                MessageBox.Show("Selecione uma pasta sugerida na aba 'Pastas em Comum & Sugestoes' primeiro.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var sug = lvCommonFolders.SelectedItems[0].Tag as CommonFolderSuggestion;
            if (sug == null) return;

            int marked = 0;
            foreach (ListViewItem item in lvDuplicates.Items)
            {
                var f = item.Tag as FileCandidate;
                if (f != null)
                {
                    if (f.DirectoryPath.StartsWith(sug.DuplicateFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        item.Checked = true;
                        marked++;
                    }
                    else if (f.DirectoryPath.StartsWith(sug.PrimaryFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        item.Checked = false; // preserve primary!
                    }
                }
            }

            tabControl.SelectedTab = tabControl.TabPages[0];
            MessageBox.Show(string.Format("Foram marcados {0} arquivos da pasta duplicada '{1}' para exclusao (preservando '{2}').",
                marked, sug.DuplicateFolder, sug.PrimaryFolder), "Sugestao Aplicada", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnDeleteSelectedEmptyDirs_Click(object sender, EventArgs e)
        {
            List<ListViewItem> toDelete = new List<ListViewItem>();
            foreach (ListViewItem item in lvEmptyDirs.Items)
            {
                if (item.Checked && item.Tag is EmptyDirectoryInfo)
                {
                    toDelete.Add(item);
                }
            }

            if (toDelete.Count == 0)
            {
                MessageBox.Show("Marque as pastas vazias que deseja excluir.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(string.Format("Deseja excluir as {0} pastas vazias selecionadas?", toDelete.Count),
                "Confirmacao", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            int deleted = 0;
            foreach (var item in toDelete)
            {
                var ed = item.Tag as EmptyDirectoryInfo;
                if (ed.IsSystemProtected && cbProtectSystem.Checked) continue;

                try
                {
                    if (Directory.Exists(ed.Path))
                    {
                        Directory.Delete(ed.Path, false);
                        deleted++;
                        lvEmptyDirs.Items.Remove(item);
                    }
                }
                catch { }
            }
            MessageBox.Show(string.Format("{0} pastas vazias foram apagadas com sucesso!", deleted), "Concluido", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnDeleteAllEmptyDirs_Click(object sender, EventArgs e)
        {
            if (lvEmptyDirs.Items.Count == 0) return;

            if (MessageBox.Show("Deseja excluir TODAS as pastas vazias que NAO pertencem ao sistema?",
                "Confirmar Limpeza Total de Pastas Vazias", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            int deleted = 0;
            List<ListViewItem> removeList = new List<ListViewItem>();
            foreach (ListViewItem item in lvEmptyDirs.Items)
            {
                var ed = item.Tag as EmptyDirectoryInfo;
                if (ed != null && !ed.IsSystemProtected)
                {
                    try
                    {
                        if (Directory.Exists(ed.Path))
                        {
                            Directory.Delete(ed.Path, false);
                            deleted++;
                            removeList.Add(item);
                        }
                    }
                    catch { }
                }
            }

            foreach (var it in removeList) lvEmptyDirs.Items.Remove(it);
            MessageBox.Show(string.Format("{0} pastas vazias foram apagadas!", deleted), "Concluido", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F2") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F2") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}

