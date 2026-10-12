using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
            "\\programdata\\microsoft",
            "\\appdata\\local\\microsoft",
            "\\appdata\\roaming\\microsoft",
            "\\$recycle.bin",
            "\\system volume information",
            "\\recovery",
            "\\boot"
        };

        public static bool IsSystemProtected(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;
            string lower = path.ToLowerInvariant();
            foreach (var pattern in ProtectedPatterns)
            {
                if (lower.Contains(pattern)) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Custom progress bar that draws the percentage and details directly inside the bar.
    /// </summary>
    public class PercentageProgressBar : Control
    {
        private int min = 0;
        private int max = 100;
        private int val = 0;
        private string customText = string.Empty;

        public PercentageProgressBar()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.Height = 24;
        }

        public int Minimum
        {
            get { return min; }
            set { min = value; Invalidate(); }
        }

        public int Maximum
        {
            get { return max; }
            set { max = value; Invalidate(); }
        }

        public int Value
        {
            get { return val; }
            set
            {
                val = Math.Max(min, Math.Min(max, value));
                Invalidate();
            }
        }

        public string CustomText
        {
            get { return customText; }
            set { customText = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(235, 238, 242)))
            {
                g.FillRectangle(bgBrush, 0, 0, this.Width, this.Height);
            }

            // Fill
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

            // Border
            using (Pen borderPen = new Pen(Color.FromArgb(189, 195, 199)))
            {
                g.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
            }

            // Text inside progress bar
            int intPct = (int)(pct * 100);
            string display = !string.IsNullOrEmpty(customText) ? customText : string.Format("{0}%", intPct);

            using (Font font = new Font("Segoe UI", 9F, FontStyle.Bold))
            {
                SizeF textSize = g.MeasureString(display, font);
                float tx = (this.Width - textSize.Width) / 2f;
                float ty = (this.Height - textSize.Height) / 2f;

                // Draw high-visibility text
                if (fillWidth > tx + (textSize.Width / 2))
                {
                    // Over the colored bar
                    using (SolidBrush textBrush = new SolidBrush(Color.White))
                    {
                        g.DrawString(display, font, textBrush, tx, ty);
                    }
                }
                else
                {
                    // Over background
                    using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(44, 62, 80)))
                    {
                        g.DrawString(display, font, textBrush, tx, ty);
                    }
                }
            }
        }
    }

    public class Engine
    {
        public ScanMode Mode { get; set; }
        public bool ProtectSystem { get; set; }
        public bool Recursive { get; set; }
        public bool IncludeHidden { get; set; }
        public List<string> TargetPaths { get; set; }

        public List<DuplicateGroup> DuplicateGroups { get; private set; }
        public List<EmptyDirectoryInfo> EmptyDirectories { get; private set; }
        public List<CommonFolderSuggestion> FolderSuggestions { get; private set; }

        public int ScannedFilesCount { get; private set; }
        public int ScannedDirsCount { get; private set; }

        public Action<string> OnStatusUpdate;
        public Action<int, int, string> OnProgressUpdate;
        private int lastStatusTick = 0;

        public Engine()
        {
            TargetPaths = new List<string>();
            DuplicateGroups = new List<DuplicateGroup>();
            EmptyDirectories = new List<EmptyDirectoryInfo>();
            FolderSuggestions = new List<CommonFolderSuggestion>();
            Mode = ScanMode.Both;
            ProtectSystem = true;
            Recursive = true;
            IncludeHidden = false;
        }

        public void Execute(CancellationToken token)
        {
            DuplicateGroups.Clear();
            EmptyDirectories.Clear();
            FolderSuggestions.Clear();
            ScannedFilesCount = 0;
            ScannedDirsCount = 0;
            lastStatusTick = 0;

            if (OnStatusUpdate != null) OnStatusUpdate("Iniciando varredura com o motor DupFinder-Neo v1.0...");
            if (OnProgressUpdate != null) OnProgressUpdate(0, 0, "Indexando arquivos...");

            List<FileCandidate> allFiles = new List<FileCandidate>();

            // Phase 1: Directory traversal & file gathering
            foreach (var rootPath in TargetPaths)
            {
                if (token.IsCancellationRequested) return;
                if (!Directory.Exists(rootPath)) continue;

                if (ProtectSystem && SystemSafety.IsSystemProtected(rootPath))
                {
                    if (OnStatusUpdate != null) OnStatusUpdate("Ignorando pasta protegida do sistema: " + rootPath);
                    continue;
                }

                ScanDirectoryRecursive(rootPath, allFiles, token);
            }

            if (token.IsCancellationRequested) return;

            // Phase 2: Empty directories bottom-up analysis
            if (Mode == ScanMode.Both || Mode == ScanMode.EmptyDirsOnly)
            {
                if (OnStatusUpdate != null) OnStatusUpdate("Analisando e catalogando pastas vazias...");
                foreach (var rootPath in TargetPaths)
                {
                    if (token.IsCancellationRequested) return;
                    if (!Directory.Exists(rootPath)) continue;
                    FindEmptyDirectoriesRecursive(rootPath, token);
                }
            }

            if (Mode == ScanMode.EmptyDirsOnly)
            {
                if (OnStatusUpdate != null) OnStatusUpdate(string.Format("Varredura de pastas vazias concluida! {0:N0} pastas encontradas.", EmptyDirectories.Count));
                if (OnProgressUpdate != null) OnProgressUpdate(100, 100, "100% Concluido");
                return;
            }

            // Duplicates multi-stage pipeline
            if (OnStatusUpdate != null) OnStatusUpdate(string.Format("Filtrando {0:N0} arquivos coletados por tamanho exato...", allFiles.Count));
            if (OnProgressUpdate != null) OnProgressUpdate(0, 0, string.Format("Agrupando {0:N0} arquivos...", allFiles.Count));

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

            if (candidateFiles.Count == 0)
            {
                if (OnStatusUpdate != null) OnStatusUpdate(string.Format("Varredura concluida! {0:N0} arquivos varridos, nenhuma duplicata encontrada.", allFiles.Count));
                if (OnProgressUpdate != null) OnProgressUpdate(100, 100, "100% Concluido");
                return;
            }

            if (OnStatusUpdate != null) OnStatusUpdate(string.Format("Pre-hashing rapido (Prefix/Suffix) de {0:N0} arquivos candidatos...", candidateFiles.Count));
            int processed = 0;
            byte[] buffer = new byte[8192];

            for (int i = 0; i < candidateFiles.Count; i++)
            {
                if (token.IsCancellationRequested) return;
                var f = candidateFiles[i];
                f.SampleHash = ComputeSampleHash(f.FullPath, f.Size, buffer);
                processed++;
                if (processed % 25 == 0 || processed == candidateFiles.Count)
                {
                    if (OnProgressUpdate != null)
                    {
                        int pct = (int)((long)processed * 100 / candidateFiles.Count);
                        string custom = string.Format("Pre-hash: {0}% ({1:N0} / {2:N0})", pct, processed, candidateFiles.Count);
                        OnProgressUpdate(processed, candidateFiles.Count, custom);
                    }
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

            if (hashCandidates.Count == 0)
            {
                if (OnStatusUpdate != null) OnStatusUpdate("Varredura concluida: nenhuma duplicata confirmada apos pre-hash.");
                if (OnProgressUpdate != null) OnProgressUpdate(100, 100, "100% Concluido");
                return;
            }

            if (OnStatusUpdate != null) OnStatusUpdate(string.Format("Validacao binaria SHA-256 (100% acuracia) de {0:N0} arquivos...", hashCandidates.Count));
            processed = 0;
            using (SHA256 sha = SHA256.Create())
            {
                for (int i = 0; i < hashCandidates.Count; i++)
                {
                    if (token.IsCancellationRequested) return;
                    var f = hashCandidates[i];
                    f.FullSha256 = ComputeFullHash(f.FullPath, sha, buffer);
                    processed++;
                    if (processed % 15 == 0 || processed == hashCandidates.Count)
                    {
                        if (OnProgressUpdate != null)
                        {
                            int pct = (int)((long)processed * 100 / hashCandidates.Count);
                            string custom = string.Format("SHA-256: {0}% ({1:N0} / {2:N0})", pct, processed, hashCandidates.Count);
                            OnProgressUpdate(processed, hashCandidates.Count, custom);
                        }
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
            if (OnStatusUpdate != null) OnStatusUpdate("Analisando pastas em comum e gerando recomendacoes...");
            AnalyzeCommonFolders();

            if (OnStatusUpdate != null) OnStatusUpdate(string.Format("Busca finalizada! {0:N0} grupos de duplicatas identificados.", DuplicateGroups.Count));
            if (OnProgressUpdate != null) OnProgressUpdate(100, 100, "100% Concluido");
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
                    if (size > 4096)
                    {
                        fs.Seek(Math.Max(0, size - 4096), SeekOrigin.Begin);
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

        private void ScanDirectoryRecursive(string dir, List<FileCandidate> files, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            try
            {
                ScannedDirsCount++;

                int now = Environment.TickCount;
                if (now - lastStatusTick >= 50 || lastStatusTick == 0)
                {
                    lastStatusTick = now;
                    string shortDir = dir.Length > 55 ? "..." + dir.Substring(dir.Length - 52) : dir;
                    if (OnStatusUpdate != null)
                        OnStatusUpdate(string.Format("Lendo pasta: {0} ({1:N0} arqs, {2:N0} pastas)", shortDir, ScannedFilesCount, ScannedDirsCount));
                    if (OnProgressUpdate != null)
                        OnProgressUpdate(0, 0, string.Format("{0:N0} arquivos ({1:N0} pastas)", ScannedFilesCount, ScannedDirsCount));
                }

                // 1. Files in current dir
                string[] dirFiles = null;
                try { dirFiles = Directory.GetFiles(dir); } catch { }

                if (dirFiles != null)
                {
                    foreach (var file in dirFiles)
                    {
                        if (token.IsCancellationRequested) return;
                        try
                        {
                            FileInfo fi = new FileInfo(file);
                            if (!IncludeHidden && (fi.Attributes & FileAttributes.Hidden) != 0) continue;

                            FileCandidate fc = new FileCandidate
                            {
                                FullPath = fi.FullName,
                                FileName = fi.Name,
                                DirectoryPath = fi.DirectoryName,
                                Size = fi.Length,
                                LastModified = fi.LastWriteTime
                            };
                            files.Add(fc);
                            ScannedFilesCount++;
                        }
                        catch { }
                    }
                }

                // 2. Subdirectories in current dir
                if (Recursive)
                {
                    string[] subDirs = null;
                    try { subDirs = Directory.GetDirectories(dir); } catch { }

                    if (subDirs != null)
                    {
                        foreach (var sub in subDirs)
                        {
                            if (token.IsCancellationRequested) return;
                            try
                            {
                                DirectoryInfo di = new DirectoryInfo(sub);
                                // Skip reparse points (symlinks, junctions) to prevent recursive loops like AppData\Local\Application Data
                                if ((di.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                                if (ProtectSystem && SystemSafety.IsSystemProtected(sub)) continue;
                                if (!IncludeHidden && (di.Attributes & FileAttributes.Hidden) != 0) continue;

                                ScanDirectoryRecursive(sub, files, token);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        private bool FindEmptyDirectoriesRecursive(string dir, CancellationToken token)
        {
            if (token.IsCancellationRequested) return false;

            bool hasEntries = false;
            try
            {
                DirectoryInfo di = new DirectoryInfo(dir);
                if ((di.Attributes & FileAttributes.ReparsePoint) != 0) return true;

                int now = Environment.TickCount;
                if (now - lastStatusTick >= 50)
                {
                    lastStatusTick = now;
                    string shortDir = dir.Length > 55 ? "..." + dir.Substring(dir.Length - 52) : dir;
                    if (OnStatusUpdate != null)
                        OnStatusUpdate(string.Format("Verificando pastas vazias: {0} ({1:N0} encontradas)", shortDir, EmptyDirectories.Count));
                    if (OnProgressUpdate != null)
                        OnProgressUpdate(0, 0, string.Format("{0:N0} pastas vazias", EmptyDirectories.Count));
                }

                string[] subDirs = null;
                try { subDirs = Directory.GetDirectories(dir); } catch { }
                if (subDirs != null)
                {
                    foreach (var sub in subDirs)
                    {
                        if (token.IsCancellationRequested) return false;
                        try
                        {
                            DirectoryInfo subDi = new DirectoryInfo(sub);
                            if ((subDi.Attributes & FileAttributes.ReparsePoint) != 0)
                            {
                                hasEntries = true;
                                continue;
                            }
                            if (ProtectSystem && SystemSafety.IsSystemProtected(sub))
                            {
                                hasEntries = true;
                                continue;
                            }
                            bool subHasEntries = FindEmptyDirectoriesRecursive(sub, token);
                            if (subHasEntries) hasEntries = true;
                        }
                        catch { hasEntries = true; }
                    }
                }

                string[] dirFiles = null;
                try { dirFiles = Directory.GetFiles(dir); } catch { }
                if (dirFiles != null && dirFiles.Length > 0) hasEntries = true;

                if (!hasEntries)
                {
                    EmptyDirectories.Add(new EmptyDirectoryInfo
                    {
                        Path = dir,
                        IsSystemProtected = SystemSafety.IsSystemProtected(dir),
                        LastModified = Directory.GetLastWriteTime(dir)
                    });
                }
            }
            catch { hasEntries = true; }

            return hasEntries;
        }

        private void AnalyzeCommonFolders()
        {
            Dictionary<string, Dictionary<string, int>> pairCounts = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, long> folderWasted = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            foreach (var g in DuplicateGroups)
            {
                for (int i = 0; i < g.Files.Count; i++)
                {
                    for (int j = 0; j < g.Files.Count; j++)
                    {
                        if (i == j) continue;
                        string dirA = g.Files[i].DirectoryPath;
                        string dirB = g.Files[j].DirectoryPath;

                        if (string.Equals(dirA, dirB, StringComparison.OrdinalIgnoreCase)) continue;

                        if (!pairCounts.ContainsKey(dirA))
                            pairCounts[dirA] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                        if (!pairCounts[dirA].ContainsKey(dirB))
                            pairCounts[dirA][dirB] = 0;

                        pairCounts[dirA][dirB]++;

                        if (!folderWasted.ContainsKey(dirA))
                            folderWasted[dirA] = 0;
                        folderWasted[dirA] += g.Size;
                    }
                }
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvA in pairCounts)
            {
                string dirA = kvA.Key;
                foreach (var kvB in kvA.Value)
                {
                    string dirB = kvB.Key;
                    int sharedCount = kvB.Value;

                    if (sharedCount >= 2)
                    {
                        string pairKey = string.Compare(dirA, dirB, StringComparison.OrdinalIgnoreCase) < 0
                            ? dirA + "|||" + dirB
                            : dirB + "|||" + dirA;

                        if (seen.Contains(pairKey)) continue;
                        seen.Add(pairKey);

                        long wasted = folderWasted.ContainsKey(dirA) ? folderWasted[dirA] : 0;

                        CommonFolderSuggestion sug = new CommonFolderSuggestion
                        {
                            DuplicateFolder = dirA,
                            PrimaryFolder = dirB,
                            DuplicateCount = sharedCount,
                            WastedBytes = wasted,
                            IsCompleteDuplicate = false,
                            Recommendation = string.Format("Esta pasta compartilha {0} arquivos identicos com '{1}'. Sugestao: Excluir a copia redundante.", sharedCount, Path.GetFileName(dirB))
                        };
                        FolderSuggestions.Add(sug);
                    }
                }
            }

            FolderSuggestions.Sort(delegate(CommonFolderSuggestion a, CommonFolderSuggestion b) { return b.DuplicateCount.CompareTo(a.DuplicateCount); });
        }
    }

    /// <summary>
    /// Dialog for selecting multiple folders with treeview and drive pickers.
    /// </summary>
    public class MultiFolderSelectorForm : Form
    {
        private TreeView treeDrives;
        private ListBox lbSelected;
        private Button btnBrowseFolderAndSubfolders;
        private Button btnOk;
        private Button btnCancel;

        public List<string> SelectedFolders { get; private set; }

        public MultiFolderSelectorForm(string initialFolder)
        {
            SelectedFolders = new List<string>();
            InitializeComponent(initialFolder);
        }

        private void InitializeComponent(string initialFolder)
        {
            this.Text = "Selecionar Multiplas Pastas - DupFinder-Neo v1.0 (by @heitorsecurity)";
            this.Size = new Size(720, 520);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.RowCount = 3;
            layout.ColumnCount = 2;
            layout.Padding = new Padding(10);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            this.Controls.Add(layout);

            // Header instructions
            Label lblInstr = new Label();
            lblInstr.Text = "Marque as pastas desejadas na arvore ou adicione subpastas de uma vez:";
            lblInstr.Dock = DockStyle.Fill;
            lblInstr.TextAlign = ContentAlignment.MiddleLeft;
            layout.Controls.Add(lblInstr, 0, 0);
            layout.SetColumnSpan(lblInstr, 2);

            // Left: TreeView with checkboxes
            treeDrives = new TreeView();
            treeDrives.Dock = DockStyle.Fill;
            treeDrives.CheckBoxes = true;
            treeDrives.BeforeExpand += TreeDrives_BeforeExpand;
            treeDrives.AfterCheck += TreeDrives_AfterCheck;
            layout.Controls.Add(treeDrives, 0, 1);

            // Right: Selected list & helper buttons
            Panel pnlRight = new Panel();
            pnlRight.Dock = DockStyle.Fill;
            layout.Controls.Add(pnlRight, 1, 1);

            Label lblSelTitle = new Label();
            lblSelTitle.Text = "Pastas selecionadas para adicionar:";
            lblSelTitle.Dock = DockStyle.Top;
            lblSelTitle.Height = 22;

            lbSelected = new ListBox();
            lbSelected.Dock = DockStyle.Fill;

            FlowLayoutPanel pnlQuick = new FlowLayoutPanel();
            pnlQuick.Dock = DockStyle.Bottom;
            pnlQuick.Height = 65;

            btnBrowseFolderAndSubfolders = new Button();
            btnBrowseFolderAndSubfolders.Text = "Adicionar Todas as Subpastas de Uma Pasta...";
            btnBrowseFolderAndSubfolders.Width = 280;
            btnBrowseFolderAndSubfolders.Height = 28;
            btnBrowseFolderAndSubfolders.Click += BtnBrowseFolderAndSubfolders_Click;

            Button btnRemoveSelected = new Button();
            btnRemoveSelected.Text = "Remover da Lista";
            btnRemoveSelected.Width = 135;
            btnRemoveSelected.Height = 26;
            btnRemoveSelected.Click += delegate { if (lbSelected.SelectedIndex >= 0) lbSelected.Items.RemoveAt(lbSelected.SelectedIndex); };

            Button btnClear = new Button();
            btnClear.Text = "Limpar Tudo";
            btnClear.Width = 135;
            btnClear.Height = 26;
            btnClear.Click += delegate { lbSelected.Items.Clear(); };

            pnlQuick.Controls.AddRange(new Control[] { btnBrowseFolderAndSubfolders, btnRemoveSelected, btnClear });
            pnlRight.Controls.AddRange(new Control[] { lbSelected, pnlQuick, lblSelTitle });

            // Bottom action buttons
            FlowLayoutPanel pnlBottom = new FlowLayoutPanel();
            pnlBottom.Dock = DockStyle.Fill;
            pnlBottom.FlowDirection = FlowDirection.RightToLeft;

            btnCancel = new Button();
            btnCancel.Text = "Cancelar";
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Width = 100;
            btnCancel.Height = 32;

            btnOk = new Button();
            btnOk.Text = "Adicionar Selecionadas";
            btnOk.Width = 180;
            btnOk.Height = 32;
            btnOk.BackColor = Color.LightSteelBlue;
            btnOk.Click += (s, e) =>
            {
                foreach (var item in lbSelected.Items)
                {
                    string p = item.ToString();
                    if (!SelectedFolders.Contains(p)) SelectedFolders.Add(p);
                }
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            pnlBottom.Controls.AddRange(new Control[] { btnCancel, btnOk });
            layout.Controls.Add(pnlBottom, 0, 2);
            layout.SetColumnSpan(pnlBottom, 2);

            PopulateDrives();
        }

        private void PopulateDrives()
        {
            treeDrives.Nodes.Clear();
            try
            {
                foreach (DriveInfo d in DriveInfo.GetDrives())
                {
                    if (d.IsReady)
                    {
                        TreeNode driveNode = new TreeNode(d.Name + " (" + d.VolumeLabel + ")")
                        {
                            Tag = d.RootDirectory.FullName
                        };
                        driveNode.Nodes.Add(new TreeNode("Carregando..."));
                        treeDrives.Nodes.Add(driveNode);
                    }
                }
            }
            catch { }
        }

        private void TreeDrives_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            TreeNode node = e.Node;
            if (node.Nodes.Count == 1 && node.Nodes[0].Text == "Carregando...")
            {
                node.Nodes.Clear();
                string path = node.Tag as string;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    try
                    {
                        string[] subDirs = Directory.GetDirectories(path);
                        foreach (string sub in subDirs)
                        {
                            if (!SystemSafety.IsSystemProtected(sub))
                            {
                                TreeNode subNode = new TreeNode(Path.GetFileName(sub)) { Tag = sub };
                                subNode.Nodes.Add(new TreeNode("Carregando..."));
                                node.Nodes.Add(subNode);
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        private void TreeDrives_AfterCheck(object sender, TreeViewEventArgs e)
        {
            string path = e.Node.Tag as string;
            if (string.IsNullOrEmpty(path)) return;

            if (e.Node.Checked)
            {
                if (!lbSelected.Items.Contains(path)) lbSelected.Items.Add(path);
            }
            else
            {
                lbSelected.Items.Remove(path);
            }
        }

        private void BtnBrowseFolderAndSubfolders_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Selecione uma pasta principal para adicionar todas as subpastas:";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string root = fbd.SelectedPath;
                        if (!lbSelected.Items.Contains(root)) lbSelected.Items.Add(root);

                        string[] subs = Directory.GetDirectories(root);
                        foreach (string sub in subs)
                        {
                            if (!SystemSafety.IsSystemProtected(sub) && !lbSelected.Items.Contains(sub))
                            {
                                lbSelected.Items.Add(sub);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Erro ao listar subpastas: " + ex.Message);
                    }
                }
            }
        }
    }

    public class MainForm : Form
    {
        private ListBox lbPaths;
        private Button btnAddPath, btnAddMultiplePaths, btnRemovePath, btnClearPaths;
        private RadioButton rbDupsOnly, rbBoth, rbEmptyOnly;
        private CheckBox cbProtectSystem, cbRecursive, cbHidden;
        private Button btnStart, btnCancel;

        private PercentageProgressBar progressBar;
        private Label lblStatus;
        private Label lblElapsedTimer;

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

        private Stopwatch scanStopwatch = new Stopwatch();
        private System.Windows.Forms.Timer uiTimer;

        private string lastFolder = string.Empty;

        public MainForm()
        {
            LoadSettings();
            InitializeComponent();
        }

        private void LoadSettings()
        {
            try
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DupFinder-Neo");
                string file = Path.Combine(appData, "last_folder.txt");
                if (File.Exists(file))
                {
                    string saved = File.ReadAllText(file).Trim();
                    if (!string.IsNullOrEmpty(saved) && Directory.Exists(saved))
                    {
                        lastFolder = saved;
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(lastFolder))
            {
                lastFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
        }

        private void SaveLastFolder(string folder)
        {
            try
            {
                if (string.IsNullOrEmpty(folder)) return;
                lastFolder = folder;
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DupFinder-Neo");
                if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
                File.WriteAllText(Path.Combine(appData, "last_folder.txt"), folder);
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Text = "DupFinder-Neo v1.0 (by @heitorsecurity)";
            this.Size = new Size(1060, 740);
            this.MinimumSize = new Size(900, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            TableLayoutPanel mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.RowCount = 4;
            mainLayout.ColumnCount = 1;
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220)); // Top options
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Results
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Actions
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));  // Status bar
            this.Controls.Add(mainLayout);

            // GroupBox Configuration
            GroupBox gbConfig = new GroupBox();
            gbConfig.Text = "Configuracoes da Varredura - DupFinder-Neo v1.0 (by @heitorsecurity)";
            gbConfig.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(gbConfig, 0, 0);

            TableLayoutPanel configLayout = new TableLayoutPanel();
            configLayout.Dock = DockStyle.Fill;
            configLayout.ColumnCount = 3;
            configLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            configLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            configLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16));
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
            btnAddMultiplePaths = new Button(); btnAddMultiplePaths.Text = "+ Varias Pastas..."; btnAddMultiplePaths.AutoSize = true; btnAddMultiplePaths.BackColor = Color.AliceBlue;
            btnRemovePath = new Button(); btnRemovePath.Text = "Remover"; btnRemovePath.AutoSize = true;
            btnClearPaths = new Button(); btnClearPaths.Text = "Limpar"; btnClearPaths.AutoSize = true;
            pnlPathBtns.Controls.AddRange(new Control[] { btnAddPath, btnAddMultiplePaths, btnRemovePath, btnClearPaths });
            pnlPaths.Controls.AddRange(new Control[] { lbPaths, pnlPathBtns, lblPaths });

            // Middle: Mode selection
            GroupBox gbMode = new GroupBox();
            gbMode.Text = "Modo de busca inicial:";
            gbMode.Dock = DockStyle.Fill;

            FlowLayoutPanel pnlModes = new FlowLayoutPanel();
            pnlModes.Dock = DockStyle.Fill;
            pnlModes.FlowDirection = FlowDirection.TopDown;
            pnlModes.WrapContents = false;
            pnlModes.AutoScroll = false;
            pnlModes.Padding = new Padding(6, 4, 6, 4);

            rbBoth = new RadioButton(); rbBoth.Text = "Pesquisar Duplicatas E Pastas Vazias"; rbBoth.Checked = true; rbBoth.AutoSize = true; rbBoth.Margin = new Padding(3, 2, 3, 2);
            rbDupsOnly = new RadioButton(); rbDupsOnly.Text = "Apenas Arquivos Duplicados"; rbDupsOnly.AutoSize = true; rbDupsOnly.Margin = new Padding(3, 2, 3, 2);
            rbEmptyOnly = new RadioButton(); rbEmptyOnly.Text = "Apenas Pastas Vazias"; rbEmptyOnly.AutoSize = true; rbEmptyOnly.Margin = new Padding(3, 2, 3, 2);
            cbProtectSystem = new CheckBox(); cbProtectSystem.Text = "Proteger pastas do sistema (Windows/Apps)"; cbProtectSystem.Checked = true; cbProtectSystem.AutoSize = true; cbProtectSystem.ForeColor = Color.DarkBlue; cbProtectSystem.Margin = new Padding(3, 4, 3, 2);
            cbRecursive = new CheckBox(); cbRecursive.Text = "Subpastas recursivas"; cbRecursive.Checked = true; cbRecursive.AutoSize = true; cbRecursive.Margin = new Padding(3, 2, 3, 2);
            cbHidden = new CheckBox(); cbHidden.Text = "Incluir arquivos ocultos"; cbHidden.Checked = false; cbHidden.AutoSize = true; cbHidden.Margin = new Padding(3, 2, 3, 2);

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

            // Bottom Status bar with PercentageProgressBar and ElapsedTimer
            TableLayoutPanel pnlBottom = new TableLayoutPanel();
            pnlBottom.Dock = DockStyle.Fill;
            pnlBottom.ColumnCount = 3;
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));

            lblStatus = new Label();
            lblStatus.Text = "Aguardando selecao de pastas...";
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;

            progressBar = new PercentageProgressBar();
            progressBar.Dock = DockStyle.Fill;
            progressBar.Height = 24;

            lblElapsedTimer = new Label();
            lblElapsedTimer.Text = "⏱️ 00:00:00";
            lblElapsedTimer.Dock = DockStyle.Fill;
            lblElapsedTimer.TextAlign = ContentAlignment.MiddleCenter;
            lblElapsedTimer.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblElapsedTimer.ForeColor = Color.DarkSlateBlue;

            pnlBottom.Controls.Add(lblStatus, 0, 0);
            pnlBottom.Controls.Add(progressBar, 1, 0);
            pnlBottom.Controls.Add(lblElapsedTimer, 2, 0);
            mainLayout.Controls.Add(pnlBottom, 0, 3);

            // Setup timer
            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 250;
            uiTimer.Tick += UiTimer_Tick;

            // Hook Events
            btnAddPath.Click += delegate(object s, EventArgs e)
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Selecione uma pasta para pesquisar duplicatas ou pastas vazias:";
                    if (!string.IsNullOrEmpty(lastFolder) && Directory.Exists(lastFolder))
                    {
                        fbd.SelectedPath = lastFolder;
                    }
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        SaveLastFolder(fbd.SelectedPath);
                        if (!lbPaths.Items.Contains(fbd.SelectedPath))
                            lbPaths.Items.Add(fbd.SelectedPath);
                    }
                }
            };

            btnAddMultiplePaths.Click += delegate(object s, EventArgs e)
            {
                using (MultiFolderSelectorForm form = new MultiFolderSelectorForm(lastFolder))
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        foreach (string p in form.SelectedFolders)
                        {
                            if (!lbPaths.Items.Contains(p))
                            {
                                lbPaths.Items.Add(p);
                                SaveLastFolder(p);
                            }
                        }
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

            // Add default user directory if exists
            string userDocs = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userDocs) && Directory.Exists(userDocs))
            {
                lbPaths.Items.Add(userDocs);
            }
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (scanStopwatch.IsRunning)
            {
                TimeSpan elapsed = scanStopwatch.Elapsed;
                lblElapsedTimer.Text = string.Format("⏱️ {0:00}:{1:00}:{2:00}", (int)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds);
            }
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (lbPaths.Items.Count == 0)
            {
                MessageBox.Show("Adicione ao menos uma pasta para pesquisar.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            engine.TargetPaths.Clear();
            foreach (var item in lbPaths.Items)
            {
                engine.TargetPaths.Add(item.ToString());
            }

            if (rbDupsOnly.Checked) engine.Mode = ScanMode.DuplicatesOnly;
            else if (rbEmptyOnly.Checked) engine.Mode = ScanMode.EmptyDirsOnly;
            else engine.Mode = ScanMode.Both;

            engine.ProtectSystem = cbProtectSystem.Checked;
            engine.Recursive = cbRecursive.Checked;
            engine.IncludeHidden = cbHidden.Checked;

            btnStart.Enabled = false;
            btnCancel.Enabled = true;
            progressBar.Value = 0;
            progressBar.CustomText = "0%";

            scanStopwatch.Reset();
            scanStopwatch.Start();
            uiTimer.Start();

            cts = new CancellationTokenSource();
            worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;

            engine.OnStatusUpdate = delegate(string msg)
            {
                if (this.InvokeRequired) this.BeginInvoke(new Action(delegate { lblStatus.Text = msg; }));
                else lblStatus.Text = msg;
            };

            engine.OnProgressUpdate = delegate(int cur, int total, string custom)
            {
                if (total <= 0)
                {
                    if (this.InvokeRequired)
                    {
                        this.BeginInvoke(new Action(delegate
                        {
                            progressBar.Value = 0;
                            progressBar.CustomText = custom;
                        }));
                    }
                    else
                    {
                        progressBar.Value = 0;
                        progressBar.CustomText = custom;
                    }
                    return;
                }

                int pct = (int)((long)cur * 100 / total);
                if (pct < 0) pct = 0; if (pct > 100) pct = 100;
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(delegate
                    {
                        progressBar.Value = pct;
                        progressBar.CustomText = custom;
                    }));
                }
                else
                {
                    progressBar.Value = pct;
                    progressBar.CustomText = custom;
                }
            };

            worker.DoWork += delegate(object s, DoWorkEventArgs ea)
            {
                engine.Execute(cts.Token);
            };

            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs ea)
            {
                scanStopwatch.Stop();
                uiTimer.Stop();

                TimeSpan elapsed = scanStopwatch.Elapsed;
                lblElapsedTimer.Text = string.Format("⏱️ {0:00}:{1:00}:{2:00}", (int)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds);

                btnStart.Enabled = true;
                btnCancel.Enabled = false;
                progressBar.Value = 100;
                progressBar.CustomText = "100% Concluido";

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

            lblSummary.Text = string.Format("Resultado: {0:N0} duplicatas encontradas ({1} desperdicados) | {2:N0} pastas vazias detectadas | Tempo: {3:00}:{4:00}",
                totalDupFiles, FormatBytes(totalWasted), engine.EmptyDirectories.Count, scanStopwatch.Elapsed.Minutes, scanStopwatch.Elapsed.Seconds);
        }

        private void BtnDeleteSelectedFiles_Click(object sender, EventArgs e)
        {
            List<FileCandidate> toDelete = new List<FileCandidate>();
            foreach (ListViewItem item in lvDuplicates.Items)
            {
                if (item.Checked && item.Tag is FileCandidate)
                {
                    toDelete.Add((FileCandidate)item.Tag);
                }
            }

            if (toDelete.Count == 0)
            {
                MessageBox.Show("Nenhum arquivo duplicado foi marcado para exclusao.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(string.Format("Tem certeza que deseja excluir permanentemente {0} arquivos duplicados marcados?\r\nEsta acao nao pode ser desfeita.", toDelete.Count),
                "Confirmar Exclusao", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            int deleted = 0;
            foreach (var f in toDelete)
            {
                try
                {
                    if (File.Exists(f.FullPath))
                    {
                        File.Delete(f.FullPath);
                        deleted++;
                    }
                }
                catch { }
            }

            MessageBox.Show(string.Format("{0} arquivos duplicados foram excluidos com sucesso!", deleted), "Concluido", MessageBoxButtons.OK, MessageBoxIcon.Information);
            PopulateResults();
        }

        private void BtnOpenFolder_Click(object sender, EventArgs e)
        {
            string path = null;
            if (tabControl.SelectedTab == tabControl.TabPages[0] && lvDuplicates.SelectedItems.Count > 0)
            {
                var tag = lvDuplicates.SelectedItems[0].Tag as FileCandidate;
                if (tag != null) path = tag.DirectoryPath;
            }
            else if (tabControl.SelectedTab == tabControl.TabPages[1] && lvCommonFolders.SelectedItems.Count > 0)
            {
                var sug = lvCommonFolders.SelectedItems[0].Tag as CommonFolderSuggestion;
                if (sug != null) path = sug.DuplicateFolder;
            }
            else if (tabControl.SelectedTab == tabControl.TabPages[2] && lvEmptyDirs.SelectedItems.Count > 0)
            {
                var ed = lvEmptyDirs.SelectedItems[0].Tag as EmptyDirectoryInfo;
                if (ed != null) path = ed.Path;
            }

            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                Process.Start("explorer.exe", path);
            }
        }

        private void BtnSelectSuggestedFolder_Click(object sender, EventArgs e)
        {
            if (lvCommonFolders.SelectedItems.Count == 0)
            {
                MessageBox.Show("Selecione uma pasta sugerida na lista primeiro.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var sug = lvCommonFolders.SelectedItems[0].Tag as CommonFolderSuggestion;
            if (sug == null) return;

            int checkedCount = 0;
            foreach (ListViewItem item in lvDuplicates.Items)
            {
                var fc = item.Tag as FileCandidate;
                if (fc != null)
                {
                    if (fc.DirectoryPath.StartsWith(sug.DuplicateFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        item.Checked = true;
                        checkedCount++;
                    }
                    else if (fc.DirectoryPath.StartsWith(sug.PrimaryFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        item.Checked = false;
                    }
                }
            }

            tabControl.SelectedIndex = 0;
            MessageBox.Show(string.Format("Foram marcados {0} arquivos da pasta clonada '{1}' para exclusao, preservando a pasta original.", checkedCount, sug.DuplicateFolder), "Sugestao Aplicada", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnDeleteSelectedEmptyDirs_Click(object sender, EventArgs e)
        {
            List<EmptyDirectoryInfo> toDelete = new List<EmptyDirectoryInfo>();
            foreach (ListViewItem item in lvEmptyDirs.Items)
            {
                if (item.Checked && item.Tag is EmptyDirectoryInfo)
                {
                    var ed = (EmptyDirectoryInfo)item.Tag;
                    if (!ed.IsSystemProtected) toDelete.Add(ed);
                }
            }

            if (toDelete.Count == 0)
            {
                MessageBox.Show("Nenhuma pasta vazia desprotegida selecionada.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(string.Format("Deseja excluir {0} pastas vazias selecionadas?", toDelete.Count), "Confirmar Exclusao", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            int count = 0;
            foreach (var ed in toDelete)
            {
                try
                {
                    if (Directory.Exists(ed.Path))
                    {
                        Directory.Delete(ed.Path);
                        count++;
                    }
                }
                catch { }
            }

            MessageBox.Show(string.Format("{0} pastas vazias foram excluidas com sucesso!", count), "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            lvEmptyDirs.Items.Clear();
        }

        private void BtnDeleteAllEmptyDirs_Click(object sender, EventArgs e)
        {
            List<EmptyDirectoryInfo> safeDirs = new List<EmptyDirectoryInfo>();
            foreach (var ed in engine.EmptyDirectories)
            {
                if (!ed.IsSystemProtected) safeDirs.Add(ed);
            }

            if (safeDirs.Count == 0)
            {
                MessageBox.Show("Nenhuma pasta vazia segura disponivel.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(string.Format("Excluir TODAS as {0} pastas vazias identificadas como seguras?", safeDirs.Count), "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            int count = 0;
            foreach (var ed in safeDirs)
            {
                try
                {
                    if (Directory.Exists(ed.Path))
                    {
                        Directory.Delete(ed.Path);
                        count++;
                    }
                }
                catch { }
            }

            MessageBox.Show(string.Format("{0} pastas vazias seguras foram removidas!", count), "Concluido", MessageBoxButtons.OK, MessageBoxIcon.Information);
            lvEmptyDirs.Items.Clear();
        }

        private string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double val = bytes;
            int u = 0;
            while (val >= 1024 && u < units.Length - 1)
            {
                val /= 1024;
                u++;
            }
            return string.Format("{0:0.##} {1}", val, units[u]);
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
