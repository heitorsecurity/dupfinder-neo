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
using System.Runtime.InteropServices;

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

    public class DuplicateItemTag
    {
        public FileCandidate File { get; set; }
        public DuplicateGroup Group { get; set; }
        public bool IsPrimary { get; set; }
    }

    public class DeletionRecord
    {
        public string DeletedFileName { get; set; }
        public string DeletedFullPath { get; set; }
        public string DeletedExtension { get; set; }
        public long DeletedSize { get; set; }
        public DateTime DeletedCreated { get; set; }
        public DateTime DeletedModified { get; set; }
        public string ParentFullPath { get; set; }
        public DateTime ParentModified { get; set; }
        public string Sha256Hash { get; set; }
    }

    public class DeletionTaskItem
    {
        public FileCandidate File { get; set; }
        public DuplicateGroup Group { get; set; }
        public ListViewItem LvItem { get; set; }
    }

    public static class ScanCacheManager
    {
        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DupFinder-Neo", "Cache");

        private static readonly string ResultsCacheFile = Path.Combine(CacheDir, "last_scan_results.dat");
        private static readonly string HashCacheFile = Path.Combine(CacheDir, "hash_cache.dat");

        public static string GetResultsCachePath() { return ResultsCacheFile; }

        public static bool HasResultsCache()
        {
            try { return File.Exists(ResultsCacheFile) && new FileInfo(ResultsCacheFile).Length > 0; }
            catch { return false; }
        }

        public static DateTime GetResultsCacheTime()
        {
            try { return File.GetLastWriteTime(ResultsCacheFile); }
            catch { return DateTime.MinValue; }
        }

        public static void DeleteCache()
        {
            try
            {
                if (File.Exists(ResultsCacheFile))
                    File.Delete(ResultsCacheFile);
            }
            catch { }
        }

        public static void SaveResultsCache(Engine engine)
        {
            try
            {
                if (!Directory.Exists(CacheDir))
                    Directory.CreateDirectory(CacheDir);

                string tempFile = ResultsCacheFile + ".tmp";
                using (FileStream fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                using (BinaryWriter bw = new BinaryWriter(fs, Encoding.UTF8))
                {
                    bw.Write("DUF2");
                    bw.Write((int)2);
                    bw.Write(DateTime.UtcNow.Ticks);

                    // TargetPaths
                    bw.Write(engine.TargetPaths != null ? engine.TargetPaths.Count : 0);
                    if (engine.TargetPaths != null)
                    {
                        foreach (var p in engine.TargetPaths)
                            bw.Write(p ?? string.Empty);
                    }

                    // DuplicateGroups
                    bw.Write(engine.DuplicateGroups != null ? engine.DuplicateGroups.Count : 0);
                    if (engine.DuplicateGroups != null)
                    {
                        foreach (var g in engine.DuplicateGroups)
                        {
                            bw.Write(g.Size);
                            bw.Write(g.Hash ?? string.Empty);
                            bw.Write(g.Files != null ? g.Files.Count : 0);
                            if (g.Files != null)
                            {
                                foreach (var f in g.Files)
                                {
                                    bw.Write(f.FullPath ?? string.Empty);
                                    bw.Write(f.FileName ?? string.Empty);
                                    bw.Write(f.DirectoryPath ?? string.Empty);
                                    bw.Write(f.Size);
                                    bw.Write(f.LastModified.Ticks);
                                    bw.Write(f.FullSha256 ?? string.Empty);
                                }
                            }
                        }
                    }

                    // EmptyDirectories
                    bw.Write(engine.EmptyDirectories != null ? engine.EmptyDirectories.Count : 0);
                    if (engine.EmptyDirectories != null)
                    {
                        foreach (var ed in engine.EmptyDirectories)
                        {
                            bw.Write(ed.Path ?? string.Empty);
                            bw.Write(ed.IsSystemProtected);
                            bw.Write(ed.LastModified.Ticks);
                        }
                    }

                    // FolderSuggestions
                    bw.Write(engine.FolderSuggestions != null ? engine.FolderSuggestions.Count : 0);
                    if (engine.FolderSuggestions != null)
                    {
                        foreach (var sug in engine.FolderSuggestions)
                        {
                            bw.Write(sug.DuplicateFolder ?? string.Empty);
                            bw.Write(sug.PrimaryFolder ?? string.Empty);
                            bw.Write(sug.DuplicateCount);
                            bw.Write(sug.WastedBytes);
                            bw.Write(sug.IsCompleteDuplicate);
                            bw.Write(sug.Recommendation ?? string.Empty);
                        }
                    }
                }

                if (File.Exists(ResultsCacheFile))
                    File.Delete(ResultsCacheFile);
                File.Move(tempFile, ResultsCacheFile);
            }
            catch { }
        }

        public static bool LoadResultsCache(Engine engine, out DateTime cacheTime, out List<string> targetPaths)
        {
            cacheTime = DateTime.MinValue;
            targetPaths = new List<string>();

            if (!HasResultsCache()) return false;

            try
            {
                using (FileStream fs = new FileStream(ResultsCacheFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8))
                {
                    string magic = br.ReadString();
                    if (magic != "DUF2") return false;
                    int ver = br.ReadInt32();
                    if (ver != 2) return false;

                    long ticks = br.ReadInt64();
                    cacheTime = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();

                    // TargetPaths
                    int pathCount = br.ReadInt32();
                    for (int i = 0; i < pathCount; i++)
                    {
                        targetPaths.Add(br.ReadString());
                    }

                    engine.DuplicateGroups.Clear();
                    int groupCount = br.ReadInt32();
                    for (int i = 0; i < groupCount; i++)
                    {
                        DuplicateGroup g = new DuplicateGroup();
                        g.Size = br.ReadInt64();
                        g.Hash = br.ReadString();
                        int fileCount = br.ReadInt32();
                        for (int j = 0; j < fileCount; j++)
                        {
                            FileCandidate f = new FileCandidate();
                            f.FullPath = br.ReadString();
                            f.FileName = br.ReadString();
                            f.DirectoryPath = br.ReadString();
                            f.Size = br.ReadInt64();
                            f.LastModified = new DateTime(br.ReadInt64());
                            f.FullSha256 = br.ReadString();
                            g.Files.Add(f);
                        }
                        if (g.Files.Count > 1)
                        {
                            engine.DuplicateGroups.Add(g);
                        }
                    }

                    engine.EmptyDirectories.Clear();
                    int emptyCount = br.ReadInt32();
                    for (int i = 0; i < emptyCount; i++)
                    {
                        EmptyDirectoryInfo ed = new EmptyDirectoryInfo();
                        ed.Path = br.ReadString();
                        ed.IsSystemProtected = br.ReadBoolean();
                        ed.LastModified = new DateTime(br.ReadInt64());
                        engine.EmptyDirectories.Add(ed);
                    }

                    engine.FolderSuggestions.Clear();
                    int sugCount = br.ReadInt32();
                    for (int i = 0; i < sugCount; i++)
                    {
                        CommonFolderSuggestion sug = new CommonFolderSuggestion();
                        sug.DuplicateFolder = br.ReadString();
                        sug.PrimaryFolder = br.ReadString();
                        sug.DuplicateCount = br.ReadInt32();
                        sug.WastedBytes = br.ReadInt64();
                        sug.IsCompleteDuplicate = br.ReadBoolean();
                        sug.Recommendation = br.ReadString();
                        engine.FolderSuggestions.Add(sug);
                    }

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public class CachedHashEntry
        {
            public long Size;
            public long LastWriteTicks;
            public ulong SampleHash;
            public string FullSha256;
        }

        public static Dictionary<string, CachedHashEntry> LoadHashCache()
        {
            var dict = new Dictionary<string, CachedHashEntry>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(HashCacheFile)) return dict;
            try
            {
                using (FileStream fs = new FileStream(HashCacheFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader br = new BinaryReader(fs, Encoding.UTF8))
                {
                    string magic = br.ReadString();
                    if (magic != "DUH1") return dict;
                    int count = br.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        string path = br.ReadString();
                        long sz = br.ReadInt64();
                        long lw = br.ReadInt64();
                        ulong sh = br.ReadUInt64();
                        string fsha = br.ReadString();
                        dict[path] = new CachedHashEntry { Size = sz, LastWriteTicks = lw, SampleHash = sh, FullSha256 = fsha };
                    }
                }
            }
            catch { }
            return dict;
        }

        public static void SaveHashCache(Dictionary<string, CachedHashEntry> dict)
        {
            if (dict == null || dict.Count == 0) return;
            try
            {
                if (!Directory.Exists(CacheDir)) Directory.CreateDirectory(CacheDir);
                string temp = HashCacheFile + ".tmp";
                using (FileStream fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (BinaryWriter bw = new BinaryWriter(fs, Encoding.UTF8))
                {
                    bw.Write("DUH1");
                    bw.Write(dict.Count);
                    foreach (var kv in dict)
                    {
                        bw.Write(kv.Key);
                        bw.Write(kv.Value.Size);
                        bw.Write(kv.Value.LastWriteTicks);
                        bw.Write(kv.Value.SampleHash);
                        bw.Write(kv.Value.FullSha256 ?? string.Empty);
                    }
                }
                if (File.Exists(HashCacheFile)) File.Delete(HashCacheFile);
                File.Move(temp, HashCacheFile);
            }
            catch { }
        }
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
    /// Robust cleaner for empty directories and OS junk files.
    /// Incorporates principles from Remove-Empty-Directories (by Jonas John &amp; hxseven)
    /// to handle ReadOnly/Hidden attributes, desktop.ini/Thumbs.db leftovers, Win32 extended paths,
    /// and transient file locks.
    /// </summary>
    public static class RobustDirectoryCleaner
    {
        private const int FILE_ATTRIBUTE_NORMAL = 0x80;

        [DllImport("kernel32.dll", EntryPoint = "RemoveDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveDirectoryWin32(string lpPathName);

        [DllImport("kernel32.dll", EntryPoint = "DeleteFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteFileWin32(string lpFileName);

        [DllImport("kernel32.dll", EntryPoint = "GetFileAttributesW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetFileAttributesWin32(string lpFileName);

        [DllImport("kernel32.dll", EntryPoint = "SetFileAttributesW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileAttributesWin32(string lpFileName, int dwFileAttributes);

        public static string NormalizeWin32Path(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            path = path.TrimEnd('\\', '/');
            if (path.StartsWith(@"\\?\")) return path;
            if (path.StartsWith(@"\\")) return @"\\?\UNC\" + path.Substring(2);
            return @"\\?\" + path;
        }

        public static bool IsDriveRoot(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;
            string trimmed = path.TrimEnd('\\', '/');
            if (trimmed.Length <= 2 && (trimmed.EndsWith(":") || trimmed.Length == 1)) return true;
            try
            {
                string root = Path.GetPathRoot(path);
                if (!string.IsNullOrEmpty(root) && root.TrimEnd('\\', '/').Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Checks whether a file is recognized as non-blocking OS junk (desktop.ini, Thumbs.db, etc.)
        /// or an empty 0-byte file that should not prevent directory cleanup.
        /// </summary>
        public static bool IsJunkOrEmptyFile(string filePath)
        {
            try
            {
                FileInfo fi = new FileInfo(filePath);
                if (!fi.Exists) return true;
                if (fi.Length == 0) return true;
                string name = fi.Name.ToLowerInvariant();
                if (name == "desktop.ini" || name == "thumbs.db" || name == "ehthumbs.db" || name == ".ds_store")
                    return true;
                if (name.StartsWith("._"))
                    return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Safely strips read-only, hidden, and system flags, then deletes a junk or empty file.
        /// </summary>
        public static bool DeleteJunkFile(string filePath, out string error)
        {
            error = null;
            try
            {
                string norm = NormalizeWin32Path(filePath);
                SetFileAttributesWin32(norm, FILE_ATTRIBUTE_NORMAL);
                try { File.SetAttributes(filePath, FileAttributes.Normal); } catch { }

                if (DeleteFileWin32(norm)) return true;

                int win32Err = Marshal.GetLastWin32Error();
                if (win32Err == 2 || win32Err == 3) return true;

                File.Delete(filePath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Deletes an empty directory. If junk files (desktop.ini, Thumbs.db, 0-byte) exist,
        /// they are cleaned first. Clears ReadOnly flags before deletion. Supports long paths.
        /// </summary>
        public static bool DeleteDirectory(string dirPath, out string error)
        {
            error = null;
            try
            {
                if (string.IsNullOrEmpty(dirPath))
                {
                    error = "Caminho do diretorio esta vazio.";
                    return false;
                }

                if (IsDriveRoot(dirPath))
                {
                    error = "Operacao proibida: unidades de disco raiz nao podem ser excluidas (" + dirPath + ").";
                    return false;
                }

                if (!Directory.Exists(dirPath))
                    return true; // Already deleted

                // 1. Purge known junk files if any exist inside this directory
                try
                {
                    string[] files = Directory.GetFiles(dirPath);
                    foreach (var f in files)
                    {
                        if (IsJunkOrEmptyFile(f))
                        {
                            string fErr;
                            DeleteJunkFile(f, out fErr);
                        }
                        else
                        {
                            error = "Diretorio contem arquivo nao-vazio: " + Path.GetFileName(f);
                            return false;
                        }
                    }
                }
                catch { }

                // 2. Strip ReadOnly/System/Hidden attributes from the directory itself
                string normPath = NormalizeWin32Path(dirPath);
                SetFileAttributesWin32(normPath, FILE_ATTRIBUTE_NORMAL);
                try { File.SetAttributes(dirPath, FileAttributes.Normal); } catch { }

                // 3. Retry loop with brief pause for transient locks (Windows Search, Explorer, Antivirus)
                int attempts = 3;
                while (attempts > 0)
                {
                    attempts--;
                    if (RemoveDirectoryWin32(normPath))
                        return true;

                    int win32Err = Marshal.GetLastWin32Error();
                    if (win32Err == 2 || win32Err == 3)
                        return true;

                    try
                    {
                        Directory.Delete(dirPath, false);
                        return true;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        return true;
                    }
                    catch (Exception ex)
                    {
                        if (attempts == 0)
                        {
                            error = string.Format("{0} (Win32Error: {1})", ex.Message, win32Err);
                            return false;
                        }
                    }
                    Thread.Sleep(50);
                }
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
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

            if (OnStatusUpdate != null) OnStatusUpdate("Iniciando varredura com o motor DupFinder-Neo v1.4...");
            if (OnProgressUpdate != null) OnProgressUpdate(0, 0, "Indexando arquivos...");

            var hashCache = ScanCacheManager.LoadHashCache();
            bool hashCacheModified = false;

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
                if (!token.IsCancellationRequested)
                {
                    ScanCacheManager.SaveResultsCache(this);
                }
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
                if (hashCacheModified) ScanCacheManager.SaveHashCache(hashCache);
                if (!token.IsCancellationRequested) ScanCacheManager.SaveResultsCache(this);
                return;
            }

            if (OnStatusUpdate != null) OnStatusUpdate(string.Format("Pre-hashing rapido de {0:N0} arquivos candidatos (com cache)...", candidateFiles.Count));
            int processed = 0;
            byte[] buffer = new byte[8192];

            for (int i = 0; i < candidateFiles.Count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    if (hashCacheModified) ScanCacheManager.SaveHashCache(hashCache);
                    return;
                }
                var f = candidateFiles[i];

                ScanCacheManager.CachedHashEntry che = null;
                if (hashCache.TryGetValue(f.FullPath, out che) && che.Size == f.Size && che.LastWriteTicks == f.LastModified.Ticks && che.SampleHash != 0)
                {
                    f.SampleHash = che.SampleHash;
                    if (!string.IsNullOrEmpty(che.FullSha256))
                        f.FullSha256 = che.FullSha256;
                }
                else
                {
                    f.SampleHash = ComputeSampleHash(f.FullPath, f.Size, buffer);
                    if (che == null) che = new ScanCacheManager.CachedHashEntry { Size = f.Size, LastWriteTicks = f.LastModified.Ticks, SampleHash = f.SampleHash };
                    else che.SampleHash = f.SampleHash;
                    hashCache[f.FullPath] = che;
                    hashCacheModified = true;
                }

                processed++;
                if (processed % 50 == 0 || processed == candidateFiles.Count)
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
                if (hashCacheModified) ScanCacheManager.SaveHashCache(hashCache);
                if (!token.IsCancellationRequested) ScanCacheManager.SaveResultsCache(this);
                return;
            }

            if (OnStatusUpdate != null) OnStatusUpdate(string.Format("Validacao binaria SHA-256 (100% acuracia) de {0:N0} arquivos...", hashCandidates.Count));
            processed = 0;
            using (SHA256 sha = SHA256.Create())
            {
                for (int i = 0; i < hashCandidates.Count; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        if (hashCacheModified) ScanCacheManager.SaveHashCache(hashCache);
                        return;
                    }
                    var f = hashCandidates[i];

                    int now = Environment.TickCount;
                    if (now - lastStatusTick >= 80 || f.Size > 10485760 || lastStatusTick == 0)
                    {
                        lastStatusTick = now;
                        string shortName = f.FileName.Length > 35 ? f.FileName.Substring(0, 32) + "..." : f.FileName;
                        if (OnStatusUpdate != null)
                            OnStatusUpdate(string.Format("Validando SHA-256 [{0:N0}/{1:N0}]: {2} ({3:0.##} MB)", i + 1, hashCandidates.Count, shortName, (double)f.Size / 1048576.0));
                    }

                    if (string.IsNullOrEmpty(f.FullSha256))
                    {
                        ScanCacheManager.CachedHashEntry che = null;
                        if (hashCache.TryGetValue(f.FullPath, out che) && che.Size == f.Size && che.LastWriteTicks == f.LastModified.Ticks && !string.IsNullOrEmpty(che.FullSha256))
                        {
                            f.FullSha256 = che.FullSha256;
                        }
                        else
                        {
                            f.FullSha256 = ComputeFullHash(f.FullPath, sha, buffer);
                            if (che == null) che = new ScanCacheManager.CachedHashEntry { Size = f.Size, LastWriteTicks = f.LastModified.Ticks, SampleHash = f.SampleHash, FullSha256 = f.FullSha256 };
                            else che.FullSha256 = f.FullSha256;
                            hashCache[f.FullPath] = che;
                            hashCacheModified = true;
                        }
                    }

                    processed++;

                    if (processed % 10 == 0 || processed == hashCandidates.Count || f.Size > 52428800)
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

            if (hashCacheModified)
            {
                ScanCacheManager.SaveHashCache(hashCache);
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

            if (!token.IsCancellationRequested)
            {
                ScanCacheManager.SaveResultsCache(this);
            }
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
                if (dirFiles != null && dirFiles.Length > 0)
                {
                    foreach (var f in dirFiles)
                    {
                        if (!RobustDirectoryCleaner.IsJunkOrEmptyFile(f))
                        {
                            hasEntries = true;
                            break;
                        }
                    }
                }

                if (!hasEntries && !RobustDirectoryCleaner.IsDriveRoot(dir))
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
            this.Text = "Selecionar Multiplas Pastas - DupFinder-Neo v1.4 (by @heitorsecurity)";
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
        private Button btnHistory;
        private Button btnClearCache;

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
            this.Text = "DupFinder-Neo v1.4 (by @heitorsecurity)";
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
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Actions (auto-resizes when wrapped on smaller screens)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));  // Status bar
            this.Controls.Add(mainLayout);

            // GroupBox Configuration
            GroupBox gbConfig = new GroupBox();
            gbConfig.Text = "Configuracoes da Varredura - DupFinder-Neo v1.4 (by @heitorsecurity)";
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
            pnlActions.AutoSize = true;
            pnlActions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            pnlActions.WrapContents = true;
            pnlActions.Padding = new Padding(5, 3, 5, 3);

            btnDeleteSelectedFiles = new Button(); btnDeleteSelectedFiles.Text = "Excluir Duplicatas Marcadas"; btnDeleteSelectedFiles.AutoSize = true; btnDeleteSelectedFiles.BackColor = Color.MistyRose;
            btnOpenFolder = new Button(); btnOpenFolder.Text = "Abrir Pasta no Explorer"; btnOpenFolder.AutoSize = true;
            btnSelectSuggestedFolder = new Button(); btnSelectSuggestedFolder.Text = "Marcar Duplicatas da Pasta Sugerida"; btnSelectSuggestedFolder.AutoSize = true;
            btnDeleteSelectedEmptyDirs = new Button(); btnDeleteSelectedEmptyDirs.Text = "Excluir Pastas Vazias Marcadas"; btnDeleteSelectedEmptyDirs.AutoSize = true; btnDeleteSelectedEmptyDirs.BackColor = Color.LightYellow;
            btnDeleteAllEmptyDirs = new Button(); btnDeleteAllEmptyDirs.Text = "Excluir TODAS as Pastas Vazias Seguras"; btnDeleteAllEmptyDirs.AutoSize = true; btnDeleteAllEmptyDirs.BackColor = Color.LavenderBlush;
            btnHistory = new Button(); btnHistory.Text = "📁 Historico de Delecoes..."; btnHistory.AutoSize = true; btnHistory.BackColor = Color.AliceBlue;
            btnClearCache = new Button(); btnClearCache.Text = "🗑️ Limpar Cache"; btnClearCache.AutoSize = true; btnClearCache.BackColor = Color.Ivory;
            lblSummary = new Label(); lblSummary.AutoSize = true; lblSummary.Text = "Pronto para iniciar."; lblSummary.TextAlign = ContentAlignment.MiddleLeft; lblSummary.Padding = new Padding(10, 6, 0, 0);

            pnlActions.Controls.AddRange(new Control[]
            {
                btnDeleteSelectedFiles,
                btnOpenFolder,
                btnSelectSuggestedFolder,
                btnDeleteSelectedEmptyDirs,
                btnDeleteAllEmptyDirs,
                btnHistory,
                btnClearCache,
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

            this.Resize += delegate(object s, EventArgs e)
            {
                pnlActions.PerformLayout();
                mainLayout.PerformLayout();
            };

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
            btnHistory.Click += BtnHistory_Click;

            btnClearCache.Click += delegate(object s, EventArgs e)
            {
                if (!ScanCacheManager.HasResultsCache())
                {
                    MessageBox.Show("Nenhum cache de varredura encontrado no disco.", "Cache Vazio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (MessageBox.Show("Deseja realmente limpar e excluir o arquivo de cache da ultima varredura?", "Confirmar Limpeza de Cache - DupFinder-Neo v1.4", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    ScanCacheManager.DeleteCache();
                    engine.DuplicateGroups.Clear();
                    engine.EmptyDirectories.Clear();
                    engine.FolderSuggestions.Clear();
                    PopulateResults();
                    lblStatus.Text = "Cache de varredura removido do disco com sucesso.";
                    lblSummary.Text = "Cache limpo. Pronto para nova busca.";
                }
            };

            // Check and auto-load saved scan cache
            bool loadedFromCache = false;
            if (ScanCacheManager.HasResultsCache())
            {
                DateTime cacheTime;
                List<string> cachedPaths;
                if (ScanCacheManager.LoadResultsCache(engine, out cacheTime, out cachedPaths))
                {
                    loadedFromCache = true;
                    if (cachedPaths.Count > 0)
                    {
                        lbPaths.Items.Clear();
                        foreach (var p in cachedPaths)
                        {
                            if (!lbPaths.Items.Contains(p)) lbPaths.Items.Add(p);
                        }
                    }
                    PopulateResults();
                    lblStatus.Text = string.Format("⚡ Cache carregado ({0:dd/MM/yyyy HH:mm}): {1:N0} grupos de duplicatas restaurados.", cacheTime, engine.DuplicateGroups.Count);
                }
            }

            // Add default user directory if exists and no cache was loaded
            if (!loadedFromCache)
            {
                string userDocs = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userDocs) && Directory.Exists(userDocs))
                {
                    lbPaths.Items.Add(userDocs);
                }
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

            List<ListViewItem> batch = new List<ListViewItem>(8000);

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
                batch.Add(groupItem);

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
                    fItem.Tag = new DuplicateItemTag { File = f, Group = g, IsPrimary = (i == 0) };
                    if (i > 0) fItem.Checked = true;
                    batch.Add(fItem);

                    if (batch.Count >= 8000)
                    {
                        lvDuplicates.Items.AddRange(batch.ToArray());
                        batch.Clear();
                        Application.DoEvents();
                    }
                }
            }

            if (batch.Count > 0)
            {
                lvDuplicates.Items.AddRange(batch.ToArray());
                batch.Clear();
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

        private string GetHistoryFolder()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string historyFolder = Path.Combine(appData, Path.Combine("DupFinder-Neo", "HistoricosDelecoes"));
                if (!Directory.Exists(historyFolder))
                {
                    Directory.CreateDirectory(historyFolder);
                }
                return historyFolder;
            }
            catch
            {
                string fallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HistoricosDelecoes");
                if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback);
                return fallback;
            }
        }

        private void BtnHistory_Click(object sender, EventArgs e)
        {
            try
            {
                string folder = GetHistoryFolder();
                if (Directory.Exists(folder))
                {
                    Process.Start("explorer.exe", folder);
                }
                else
                {
                    MessageBox.Show("A pasta de historico ainda nao contem registros de delecao.", "Historico de Delecoes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao abrir pasta de historico: " + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDeleteSelectedFiles_Click(object sender, EventArgs e)
        {
            List<DeletionTaskItem> toDelete = new List<DeletionTaskItem>();
            foreach (ListViewItem item in lvDuplicates.Items)
            {
                if (item.Checked && item.Tag is DuplicateItemTag)
                {
                    var tag = (DuplicateItemTag)item.Tag;
                    toDelete.Add(new DeletionTaskItem
                    {
                        File = tag.File,
                        Group = tag.Group,
                        LvItem = item
                    });
                }
            }

            if (toDelete.Count == 0)
            {
                MessageBox.Show("Nenhum arquivo duplicado foi marcado para exclusao.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            long totalBytesToFree = 0;
            foreach (var item in toDelete)
            {
                totalBytesToFree += item.File.Size;
            }

            string confirmMsg = string.Format(
                "CONFIRMACAO DE EXCLUSAO DE DUPLICATAS\n\n" +
                "• Arquivos selecionados para exclusao: {0:N0}\n" +
                "• Espaco em disco que sera LIBERADO: {1} ({2:N0} bytes)\n\n" +
                "ATENCAO: Os arquivos serao excluidos permanentemente do disco.\n" +
                "Um relatorio completo com os dados de cada arquivo e a indicacao do arquivo pai preservado sera salvo na pasta de Historicos no seu AppData.\n\n" +
                "Deseja realmente continuar e excluir estes {0:N0} arquivos?",
                toDelete.Count, FormatBytes(totalBytesToFree), totalBytesToFree);

            if (MessageBox.Show(confirmMsg, "Confirmar Exclusao - DupFinder-Neo v1.4", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            // Disable buttons to prevent conflicting actions
            btnStart.Enabled = false;
            btnDeleteSelectedFiles.Enabled = false;
            btnDeleteSelectedEmptyDirs.Enabled = false;
            btnDeleteAllEmptyDirs.Enabled = false;
            btnCancel.Enabled = true;

            cts = new CancellationTokenSource();
            scanStopwatch.Reset();
            scanStopwatch.Start();
            uiTimer.Start();

            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            progressBar.CustomText = "0% (0 B liberados)";
            lblStatus.Text = string.Format("Preparando exclusao de {0:N0} arquivos duplicados...", toDelete.Count);

            HashSet<string> pathsToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in toDelete)
            {
                pathsToDelete.Add(item.File.FullPath);
            }

            DateTime opTime = DateTime.Now;
            List<DeletionRecord> records = new List<DeletionRecord>();
            List<string> deletionErrors = new List<string>();
            int deleted = 0;
            long deletedBytes = 0;
            bool wasCancelled = false;
            string reportPath = string.Empty;

            BackgroundWorker delWorker = new BackgroundWorker();
            delWorker.WorkerSupportsCancellation = true;

            delWorker.DoWork += delegate(object s, DoWorkEventArgs ea)
            {
                int total = toDelete.Count;
                int lastTick = 0;

                for (int i = 0; i < total; i++)
                {
                    if (cts.IsCancellationRequested)
                    {
                        wasCancelled = true;
                        break;
                    }

                    var item = toDelete[i];
                    var f = item.File;
                    var g = item.Group;

                    FileCandidate parent = null;
                    if (g != null && g.Files != null)
                    {
                        foreach (var cand in g.Files)
                        {
                            if (!pathsToDelete.Contains(cand.FullPath))
                            {
                                parent = cand;
                                break;
                            }
                        }
                        if (parent == null && g.Files.Count > 0)
                            parent = g.Files[0];
                    }

                    DateTime created = DateTime.MinValue;
                    try
                    {
                        if (File.Exists(f.FullPath))
                            created = File.GetCreationTime(f.FullPath);
                    }
                    catch { }

                    try
                    {
                        if (File.Exists(f.FullPath))
                        {
                            try { File.SetAttributes(f.FullPath, FileAttributes.Normal); } catch { }
                            File.Delete(f.FullPath);
                            deleted++;
                            deletedBytes += f.Size;

                            records.Add(new DeletionRecord
                            {
                                DeletedFileName = f.FileName,
                                DeletedFullPath = f.FullPath,
                                DeletedExtension = Path.GetExtension(f.FullPath),
                                DeletedSize = f.Size,
                                DeletedCreated = created,
                                DeletedModified = f.LastModified,
                                ParentFullPath = parent != null ? parent.FullPath : "(Nenhum pai preservado - todos marcados)",
                                ParentModified = parent != null ? parent.LastModified : DateTime.MinValue,
                                Sha256Hash = !string.IsNullOrEmpty(f.FullSha256) ? f.FullSha256 : (g != null ? g.Hash : "")
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        deletionErrors.Add(string.Format("{0} -> {1}", f.FullPath, ex.Message));
                    }

                    // Update UI throttled at 50ms
                    int now = Environment.TickCount;
                    if (now - lastTick >= 50 || i == total - 1)
                    {
                        lastTick = now;
                        int pct = (int)((long)(i + 1) * 100 / total);
                        if (pct > 100) pct = 100;
                        string freedStr = FormatBytes(deletedBytes);
                        string totalStr = FormatBytes(totalBytesToFree);
                        string curFile = f.FileName.Length > 40 ? f.FileName.Substring(0, 37) + "..." : f.FileName;
                        string curStatus = string.Format("🗑️ Excluindo ({0:N0}/{1:N0}): {2}", i + 1, total, curFile);
                        string curCustom = string.Format("{0}% ({1} liberados)", pct, freedStr);
                        string curSummary = string.Format("Exclusao em andamento: {0:N0}/{1:N0} excluidos | Liberado: {2} / {3}", i + 1, total, freedStr, totalStr);

                        if (this.InvokeRequired)
                        {
                            this.BeginInvoke(new Action(delegate
                            {
                                lblStatus.Text = curStatus;
                                progressBar.Value = pct;
                                progressBar.CustomText = curCustom;
                                lblSummary.Text = curSummary;
                            }));
                        }
                        else
                        {
                            lblStatus.Text = curStatus;
                            progressBar.Value = pct;
                            progressBar.CustomText = curCustom;
                            lblSummary.Text = curSummary;
                        }
                    }
                }

                // Generate detailed TXT report in AppData\DupFinder-Neo\HistoricosDelecoes
                string historyFolder = GetHistoryFolder();
                string reportFileName = string.Format("Relatorio_Delecao_Duplicatas_{0}.txt", opTime.ToString("yyyyMMdd_HHmmss"));
                reportPath = Path.Combine(historyFolder, reportFileName);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine("DUPFINDER-NEO v1.4 (by @heitorsecurity) - RELATORIO DE EXCLUSAO DE DUPLICATAS");
                sb.AppendLine("================================================================================");
                sb.AppendLine(string.Format("Data e Hora da Execucao  : {0:yyyy-MM-dd HH:mm:ss}", opTime));
                sb.AppendLine(string.Format("Status da Operacao       : {0}", wasCancelled ? "INTERROMPIDO PELO USUARIO" : "CONCLUIDO COM SUCESSO"));
                sb.AppendLine(string.Format("Total de Itens Excluidos : {0:N0}", deleted));
                sb.AppendLine(string.Format("Espaco Total Liberado    : {0} ({1:N0} bytes)", FormatBytes(deletedBytes), deletedBytes));
                sb.AppendLine(string.Format("Diretorio de Historico   : {0}", historyFolder));
                sb.AppendLine("================================================================================");
                sb.AppendLine();
                sb.AppendLine("DETALHAMENTO DOS ARQUIVOS APAGADOS E ARQUIVO PAI (REFERENCIA PRINCIPAL PRESERVADA):");
                sb.AppendLine("--------------------------------------------------------------------------------");

                for (int i = 0; i < records.Count; i++)
                {
                    var r = records[i];
                    sb.AppendLine(string.Format("[{0}] ARQUIVO APAGADO:", i + 1));
                    sb.AppendLine(string.Format("    • Nome do Arquivo     : {0}", r.DeletedFileName));
                    sb.AppendLine(string.Format("    • Caminho Completo    : {0}", r.DeletedFullPath));
                    sb.AppendLine(string.Format("    • Extensao           : {0}", r.DeletedExtension));
                    sb.AppendLine(string.Format("    • Tamanho             : {0} ({1:N0} bytes)", FormatBytes(r.DeletedSize), r.DeletedSize));
                    sb.AppendLine(string.Format("    • Data de Criacao     : {0:yyyy-MM-dd HH:mm:ss}", r.DeletedCreated));
                    sb.AppendLine(string.Format("    • Data de Modificacao : {0:yyyy-MM-dd HH:mm:ss}", r.DeletedModified));
                    if (!string.IsNullOrEmpty(r.Sha256Hash))
                        sb.AppendLine(string.Format("    • Hash SHA-256        : {0}", r.Sha256Hash));
                    sb.AppendLine("    ARQUIVO PAI (REFERENCIA PRINCIPAL PRESERVADA):");
                    sb.AppendLine(string.Format("    • Caminho do Pai      : {0}", r.ParentFullPath));
                    if (r.ParentModified != DateTime.MinValue)
                        sb.AppendLine(string.Format("    • Data do Pai         : {0:yyyy-MM-dd HH:mm:ss}", r.ParentModified));
                    sb.AppendLine("--------------------------------------------------------------------------------");
                }

                if (deletionErrors.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("FALHAS / ERROS DE PERMISSAO DURANTE A EXCLUSAO:");
                    foreach (var err in deletionErrors)
                    {
                        sb.AppendLine("  ! " + err);
                    }
                    sb.AppendLine("--------------------------------------------------------------------------------");
                }

                sb.AppendLine();
                sb.AppendLine("Fim do Relatorio. Gerado automaticamente pelo DupFinder-Neo v1.4 (by @heitorsecurity).");

                try
                {
                    File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
                }
                catch { }
            };

            delWorker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs ea)
            {
                scanStopwatch.Stop();
                uiTimer.Stop();

                btnStart.Enabled = true;
                btnDeleteSelectedFiles.Enabled = true;
                btnDeleteSelectedEmptyDirs.Enabled = true;
                btnDeleteAllEmptyDirs.Enabled = true;
                btnCancel.Enabled = false;

                progressBar.Value = 100;
                progressBar.CustomText = string.Format("100% Concluido ({0} liberados)", FormatBytes(deletedBytes));
                lblStatus.Text = string.Format("Exclusao {0}: {1:N0} arquivos apagados, {2} liberados.",
                    wasCancelled ? "interrompida" : "finalizada", deleted, FormatBytes(deletedBytes));

                // Remove deleted files from in-memory groups
                foreach (var item in toDelete)
                {
                    if (item.Group != null && item.Group.Files != null)
                    {
                        item.Group.Files.Remove(item.File);
                    }
                }
                engine.DuplicateGroups.RemoveAll(delegate(DuplicateGroup g) { return g.Files.Count <= 1; });

                // Manage cache persistence after deletion:
                // "depois apague o cache se ele deletou tudo o que tinha pra deletar"
                bool allDeleted = (engine.DuplicateGroups.Count == 0);
                if (allDeleted)
                {
                    ScanCacheManager.DeleteCache();
                    lblStatus.Text = "Todas as duplicatas foram excluidas! O cache da varredura foi removido com sucesso.";
                }
                else
                {
                    ScanCacheManager.SaveResultsCache(engine);
                }

                // Refresh ListView smoothly
                PopulateResults();

                string finishMsg = string.Format(
                    "Exclusao {0}!\n\n" +
                    "• Arquivos excluidos: {1:N0}\n" +
                    "• Espaco em disco liberado: {2}\n" +
                    (allDeleted ? "• Todas as duplicatas foram eliminadas: o cache de varredura foi removido com sucesso!\n\n" : "• O cache de varredura foi atualizado com as duplicatas restantes.\n\n") +
                    "Relatorio detalhado gerado em:\n{3}\n\n" +
                    "Deseja abrir o relatorio agora no Bloco de Notas?",
                    wasCancelled ? "interrompida pelo usuario" : "concluida com sucesso",
                    deleted, FormatBytes(deletedBytes), reportPath);

                if (MessageBox.Show(finishMsg, "Exclusao Concluida - DupFinder-Neo v1.4", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    try { Process.Start("notepad.exe", reportPath); } catch { }
                }
            };

            delWorker.RunWorkerAsync();
        }

        private void BtnOpenFolder_Click(object sender, EventArgs e)
        {
            string path = null;
            if (tabControl.SelectedTab == tabControl.TabPages[0] && lvDuplicates.SelectedItems.Count > 0)
            {
                var tag = lvDuplicates.SelectedItems[0].Tag as DuplicateItemTag;
                if (tag != null && tag.File != null) path = tag.File.DirectoryPath;
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
                var tag = item.Tag as DuplicateItemTag;
                if (tag != null && tag.File != null)
                {
                    var fc = tag.File;
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

            string confirmMsg = string.Format(
                "CONFIRMACAO DE EXCLUSAO DE PASTAS VAZIAS\n\n" +
                "• Quantidade de pastas vazias selecionadas: {0:N0}\n" +
                "• Pastas do sistema estao protegidas e nao serao tocadas.\n\n" +
                "Deseja realmente excluir permanentemente estas {0:N0} pastas vazias?",
                toDelete.Count);

            if (MessageBox.Show(confirmMsg, "Confirmar Exclusao - DupFinder-Neo v1.4", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            btnStart.Enabled = false;
            btnDeleteSelectedFiles.Enabled = false;
            btnDeleteSelectedEmptyDirs.Enabled = false;
            btnDeleteAllEmptyDirs.Enabled = false;
            btnCancel.Enabled = true;

            cts = new CancellationTokenSource();
            scanStopwatch.Reset();
            scanStopwatch.Start();
            uiTimer.Start();

            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            lblStatus.Text = string.Format("Excluindo {0:N0} pastas vazias...", toDelete.Count);

            DateTime opTime = DateTime.Now;
            List<string> deletedPaths = new List<string>();
            List<string> errors = new List<string>();
            bool wasCancelled = false;
            string reportPath = string.Empty;

            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;

            worker.DoWork += delegate(object s, DoWorkEventArgs ea)
            {
                // Sort bottom-up (deepest paths first) so child directories are removed before parent directories
                toDelete.Sort(delegate(EmptyDirectoryInfo a, EmptyDirectoryInfo b)
                {
                    return b.Path.Length.CompareTo(a.Path.Length);
                });

                int totalCount = toDelete.Count;
                int lastTick = 0;

                for (int i = 0; i < totalCount; i++)
                {
                    if (cts.IsCancellationRequested)
                    {
                        wasCancelled = true;
                        break;
                    }

                    var ed = toDelete[i];
                    string delError;
                    if (RobustDirectoryCleaner.DeleteDirectory(ed.Path, out delError))
                    {
                        deletedPaths.Add(ed.Path);
                    }
                    else
                    {
                        errors.Add(ed.Path + " -> " + delError);
                    }

                    int now = Environment.TickCount;
                    if (now - lastTick >= 50 || i == totalCount - 1)
                    {
                        lastTick = now;
                        int pct = (int)((long)(i + 1) * 100 / totalCount);
                        if (pct > 100) pct = 100;

                        string curStatus = string.Format("🗑️ Excluindo pasta ({0:N0}/{1:N0}): {2}", i + 1, totalCount, Path.GetFileName(ed.Path));
                        string curCustom = string.Format("{0}% ({1:N0} removidas)", pct, deletedPaths.Count);

                        if (this.InvokeRequired)
                        {
                            this.BeginInvoke(new Action(delegate
                            {
                                lblStatus.Text = curStatus;
                                progressBar.Value = pct;
                                progressBar.CustomText = curCustom;
                            }));
                        }
                        else
                        {
                            lblStatus.Text = curStatus;
                            progressBar.Value = pct;
                            progressBar.CustomText = curCustom;
                        }
                    }
                }

                // Generate report
                string historyFolder = GetHistoryFolder();
                string reportFileName = string.Format("Relatorio_Delecao_PastasVazias_{0}.txt", opTime.ToString("yyyyMMdd_HHmmss"));
                reportPath = Path.Combine(historyFolder, reportFileName);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine("DUPFINDER-NEO v1.4 - RELATORIO DE EXCLUSAO DE PASTAS VAZIAS");
                sb.AppendLine("Engine inspirada no Remove-Empty-Directories (por Jonas John & hxseven) e DupFinder (por Patrick De Smet).");
                sb.AppendLine("Desenvolvido e mantido por @heitorsecurity sob licenca GNU GPL 3.0.");
                sb.AppendLine("================================================================================");
                sb.AppendLine(string.Format("Data e Hora da Execucao  : {0:yyyy-MM-dd HH:mm:ss}", opTime));
                sb.AppendLine(string.Format("Status da Operacao       : {0}", wasCancelled ? "INTERROMPIDO PELO USUARIO" : "CONCLUIDO COM SUCESSO"));
                sb.AppendLine(string.Format("Total de Pastas Excluidas: {0:N0}", deletedPaths.Count));
                sb.AppendLine(string.Format("Diretorio de Historico   : {0}", historyFolder));
                sb.AppendLine("================================================================================");
                sb.AppendLine();
                sb.AppendLine("LISTA DE PASTAS VAZIAS EXCLUIDAS:");
                for (int i = 0; i < deletedPaths.Count; i++)
                {
                    sb.AppendLine(string.Format("[{0}] {1}", i + 1, deletedPaths[i]));
                }
                if (errors.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("ERROS DURANTE A EXCLUSAO:");
                    foreach (var err in errors) sb.AppendLine("  ! " + err);
                }
                sb.AppendLine();
                sb.AppendLine("Fim do Relatorio. Gerado automaticamente pelo DupFinder-Neo v1.4.");

                try { File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8); } catch { }
            };

            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs ea)
            {
                scanStopwatch.Stop();
                uiTimer.Stop();

                btnStart.Enabled = true;
                btnDeleteSelectedFiles.Enabled = true;
                btnDeleteSelectedEmptyDirs.Enabled = true;
                btnDeleteAllEmptyDirs.Enabled = true;
                btnCancel.Enabled = false;

                progressBar.Value = 100;
                progressBar.CustomText = string.Format("100% Concluido ({0:N0} pastas)", deletedPaths.Count);
                lblStatus.Text = string.Format("Exclusao {0}: {1:N0} pastas vazias removidas.",
                    wasCancelled ? "interrompida" : "finalizada", deletedPaths.Count);

                // Remove deleted empty dirs from engine list
                HashSet<string> deletedSet = new HashSet<string>(deletedPaths, StringComparer.OrdinalIgnoreCase);
                engine.EmptyDirectories.RemoveAll(delegate(EmptyDirectoryInfo ed) { return deletedSet.Contains(ed.Path); });

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
                    if (!ed.IsSystemProtected) eItem.Checked = true;
                    lvEmptyDirs.Items.Add(eItem);
                }
                lvEmptyDirs.EndUpdate();

                string finishMsg = string.Format(
                    "Exclusao {0}!\n\n" +
                    "• Pastas vazias excluidas: {1:N0}\n\n" +
                    "Relatorio salvo em:\n{2}\n\n" +
                    "Deseja abrir o relatorio no Bloco de Notas?",
                    wasCancelled ? "interrompida pelo usuario" : "concluida com sucesso",
                    deletedPaths.Count, reportPath);

                if (MessageBox.Show(finishMsg, "Sucesso - DupFinder-Neo v1.4", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    try { Process.Start("notepad.exe", reportPath); } catch { }
                }
            };

            worker.RunWorkerAsync();
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

            string confirmMsg = string.Format(
                "CONFIRMACAO DE EXCLUSAO GERAL DE PASTAS VAZIAS\n\n" +
                "• Quantidade total de pastas vazias seguras: {0:N0}\n" +
                "• Pastas de sistema e de instalacao foram preservadas automaticamente.\n\n" +
                "Deseja realmente excluir TODAS as {0:N0} pastas vazias identificadas?",
                safeDirs.Count);

            if (MessageBox.Show(confirmMsg, "Confirmar Exclusao Geral - DupFinder-Neo v1.4", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            btnStart.Enabled = false;
            btnDeleteSelectedFiles.Enabled = false;
            btnDeleteSelectedEmptyDirs.Enabled = false;
            btnDeleteAllEmptyDirs.Enabled = false;
            btnCancel.Enabled = true;

            cts = new CancellationTokenSource();
            scanStopwatch.Reset();
            scanStopwatch.Start();
            uiTimer.Start();

            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
            lblStatus.Text = string.Format("Excluindo todas as {0:N0} pastas vazias...", safeDirs.Count);

            DateTime opTime = DateTime.Now;
            List<string> deletedPaths = new List<string>();
            List<string> errors = new List<string>();
            bool wasCancelled = false;
            string reportPath = string.Empty;

            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerSupportsCancellation = true;

            worker.DoWork += delegate(object s, DoWorkEventArgs ea)
            {
                // Sort bottom-up (deepest paths first) so child directories are removed before parent directories
                safeDirs.Sort(delegate(EmptyDirectoryInfo a, EmptyDirectoryInfo b)
                {
                    return b.Path.Length.CompareTo(a.Path.Length);
                });

                int totalCount = safeDirs.Count;
                int lastTick = 0;

                for (int i = 0; i < totalCount; i++)
                {
                    if (cts.IsCancellationRequested)
                    {
                        wasCancelled = true;
                        break;
                    }

                    var ed = safeDirs[i];
                    string delError;
                    if (RobustDirectoryCleaner.DeleteDirectory(ed.Path, out delError))
                    {
                        deletedPaths.Add(ed.Path);
                    }
                    else
                    {
                        errors.Add(ed.Path + " -> " + delError);
                    }

                    int now = Environment.TickCount;
                    if (now - lastTick >= 50 || i == totalCount - 1)
                    {
                        lastTick = now;
                        int pct = (int)((long)(i + 1) * 100 / totalCount);
                        if (pct > 100) pct = 100;

                        string curStatus = string.Format("🗑️ Excluindo pasta ({0:N0}/{1:N0}): {2}", i + 1, totalCount, Path.GetFileName(ed.Path));
                        string curCustom = string.Format("{0}% ({1:N0} removidas)", pct, deletedPaths.Count);

                        if (this.InvokeRequired)
                        {
                            this.BeginInvoke(new Action(delegate
                            {
                                lblStatus.Text = curStatus;
                                progressBar.Value = pct;
                                progressBar.CustomText = curCustom;
                            }));
                        }
                        else
                        {
                            lblStatus.Text = curStatus;
                            progressBar.Value = pct;
                            progressBar.CustomText = curCustom;
                        }
                    }
                }

                // Generate report
                string historyFolder = GetHistoryFolder();
                string reportFileName = string.Format("Relatorio_Delecao_TodasPastasVazias_{0}.txt", opTime.ToString("yyyyMMdd_HHmmss"));
                reportPath = Path.Combine(historyFolder, reportFileName);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine("DUPFINDER-NEO v1.4 - RELATORIO DE EXCLUSAO DE PASTAS VAZIAS");
                sb.AppendLine("Engine inspirada no Remove-Empty-Directories (por Jonas John & hxseven) e DupFinder (por Patrick De Smet).");
                sb.AppendLine("Desenvolvido e mantido por @heitorsecurity sob licenca GNU GPL 3.0.");
                sb.AppendLine("================================================================================");
                sb.AppendLine(string.Format("Data e Hora da Execucao  : {0:yyyy-MM-dd HH:mm:ss}", opTime));
                sb.AppendLine(string.Format("Status da Operacao       : {0}", wasCancelled ? "INTERROMPIDO PELO USUARIO" : "CONCLUIDO COM SUCESSO"));
                sb.AppendLine(string.Format("Total de Pastas Excluidas: {0:N0}", deletedPaths.Count));
                sb.AppendLine(string.Format("Diretorio de Historico   : {0}", historyFolder));
                sb.AppendLine("================================================================================");
                sb.AppendLine();
                sb.AppendLine("LISTA DE PASTAS VAZIAS EXCLUIDAS:");
                for (int i = 0; i < deletedPaths.Count; i++)
                {
                    sb.AppendLine(string.Format("[{0}] {1}", i + 1, deletedPaths[i]));
                }
                if (errors.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("ERROS DURANTE A EXCLUSAO:");
                    foreach (var err in errors) sb.AppendLine("  ! " + err);
                }
                sb.AppendLine();
                sb.AppendLine("Fim do Relatorio. Gerado automaticamente pelo DupFinder-Neo v1.4.");

                try { File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8); } catch { }
            };

            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs ea)
            {
                scanStopwatch.Stop();
                uiTimer.Stop();

                btnStart.Enabled = true;
                btnDeleteSelectedFiles.Enabled = true;
                btnDeleteSelectedEmptyDirs.Enabled = true;
                btnDeleteAllEmptyDirs.Enabled = true;
                btnCancel.Enabled = false;

                progressBar.Value = 100;
                progressBar.CustomText = string.Format("100% Concluido ({0:N0} pastas)", deletedPaths.Count);
                lblStatus.Text = string.Format("Exclusao geral {0}: {1:N0} pastas removidas.",
                    wasCancelled ? "interrompida" : "finalizada", deletedPaths.Count);

                // Remove all safe empty dirs from engine list
                HashSet<string> deletedSet = new HashSet<string>(deletedPaths, StringComparer.OrdinalIgnoreCase);
                engine.EmptyDirectories.RemoveAll(delegate(EmptyDirectoryInfo ed) { return deletedSet.Contains(ed.Path); });

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
                    if (!ed.IsSystemProtected) eItem.Checked = true;
                    lvEmptyDirs.Items.Add(eItem);
                }
                lvEmptyDirs.EndUpdate();

                string finishMsg = string.Format(
                    "Exclusao {0}!\n\n" +
                    "• Pastas vazias excluidas: {1:N0}\n\n" +
                    "Relatorio salvo em:\n{2}\n\n" +
                    "Deseja abrir o relatorio no Bloco de Notas?",
                    wasCancelled ? "interrompida pelo usuario" : "concluida com sucesso",
                    deletedPaths.Count, reportPath);

                if (MessageBox.Show(finishMsg, "Sucesso - DupFinder-Neo v1.4", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    try { Process.Start("notepad.exe", reportPath); } catch { }
                }
            };

            worker.RunWorkerAsync();
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

        private static bool IsAdministrator()
        {
            try
            {
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        private static bool RequestElevation(string[] args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.UseShellExecute = true;
                psi.Verb = "runas"; // Dispara a janela de confirmacao UAC do Windows (Sim / Nao)
                if (args != null && args.Length > 0)
                {
                    string[] quoted = new string[args.Length];
                    for (int i = 0; i < args.Length; i++)
                    {
                        quoted[i] = "\"" + args[i].Replace("\"", "\\\"") + "\"";
                    }
                    psi.Arguments = string.Join(" ", quoted);
                }
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!IsAdministrator())
            {
                if (!RequestElevation(args))
                {
                    MessageBox.Show(
                        "O DupFinder-Neo requer privilegios de Administrador para acessar, analisar e limpar pastas protegidas ou discos do sistema.\n\nPor favor, confirme clicando em 'SIM' na solicitacao do Windows para executar o aplicativo.",
                        "Privilegios de Administrador Necessarios - DupFinder-Neo v1.4",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return;
            }

            Application.Run(new MainForm());
        }
    }
}
