//! DupFinder-Neo: Motor de Deteccao de Duplicatas e Limpeza de Pastas Vazias
//! Referencia Oficial SOT (Single Source of Truth) para Rust.

use std::collections::{HashMap, HashSet};
use std::fs::{self, File};
use std::io::{Read, Seek, SeekFrom};
use std::path::{Path, PathBuf};
use std::time::SystemTime;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ScanMode {
    DuplicatesOnly,
    Both,
    EmptyDirsOnly,
}

#[derive(Debug, Clone)]
pub struct EngineOptions {
    pub search_paths: Vec<PathBuf>,
    pub mode: ScanMode,
    pub protect_system_dirs: bool,
    pub recurse_subdirs: bool,
    pub include_hidden: bool,
    pub min_size: u64,
    pub max_size: Option<u64>,
}

impl Default for EngineOptions {
    fn default() -> Self {
        Self {
            search_paths: Vec::new(),
            mode: ScanMode::Both,
            protect_system_dirs: true,
            recurse_subdirs: true,
            include_hidden: false,
            min_size: 1,
            max_size: None,
        }
    }
}

#[derive(Debug, Clone)]
pub struct FileCandidate {
    pub full_path: PathBuf,
    pub file_name: String,
    pub directory_path: PathBuf,
    pub size: u64,
    pub last_modified: SystemTime,
    pub sample_hash: u64,
    pub full_sha256: Option<String>,
}

#[derive(Debug, Clone)]
pub struct DuplicateGroup {
    pub size: u64,
    pub hash: String,
    pub files: Vec<FileCandidate>,
}

#[derive(Debug, Clone)]
pub struct EmptyDirectoryInfo {
    pub path: PathBuf,
    pub is_system_protected: bool,
    pub last_modified: SystemTime,
}

#[derive(Debug, Clone)]
pub struct CommonFolderSuggestion {
    pub duplicate_folder: PathBuf,
    pub primary_folder: PathBuf,
    pub duplicate_count: usize,
    pub wasted_bytes: u64,
    pub is_complete_duplicate: bool,
    pub recommendation: String,
}

#[derive(Debug, Clone, Default)]
pub struct ScanResults {
    pub duplicate_groups: Vec<DuplicateGroup>,
    pub empty_directories: Vec<EmptyDirectoryInfo>,
    pub folder_suggestions: Vec<CommonFolderSuggestion>,
    pub total_duplicate_files: usize,
    pub total_wasted_bytes: u64,
}

/// Identifica se um caminho e pasta protegida do sistema operacional.
pub fn is_system_protected(path: &Path) -> bool {
    let s = path.to_string_lossy().to_lowercase().replace('/', "\\");

    // Raiz de unidade (ex.: C:\, D:\)
    if s.len() <= 3 && s.contains(':') {
        return true;
    }

    const PROTECTED_PATTERNS: &[&str] = &[
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
        "\\appdata\\roaming\\microsoft",
    ];

    for pat in PROTECTED_PATTERNS {
        if s.contains(pat) {
            return true;
        }
    }

    false
}

/// Calcula o pre-hash rapido (FNV-1a 64-bit) dos primeiros 4KB e ultimos 4KB.
pub fn compute_sample_hash(path: &Path, size: u64) -> std::io::Result<u64> {
    if size == 0 {
        return Ok(0);
    }

    let mut file = File::open(path)?;
    let mut buf = [0u8; 4096];
    let mut hash: u64 = 14695981039346656037;

    let read_initial = file.read(&mut buf)?;
    for &b in &buf[..read_initial] {
        hash ^= b as u64;
        hash = hash.wrapping_mul(1099511628211);
    }

    if size > 8192 {
        file.seek(SeekFrom::Start(size - 4096))?;
        let read_suffix = file.read(&mut buf)?;
        for &b in &buf[..read_suffix] {
            hash ^= b as u64;
            hash = hash.wrapping_mul(1099511628211);
        }
    }

    Ok(hash)
}

/// Executa a varredura e o pipeline multi-estagio de deteccao.
pub fn run_scan<F>(options: &EngineOptions, mut on_status: F) -> std::io::Result<ScanResults>
where
    F: FnMut(&str),
{
    let mut results = ScanResults::default();
    let mut all_files = Vec::new();
    let mut scanned_dirs = Vec::new();

    on_status("Fase 1: Varrendo diretorios e coletando metadados...");

    for root in &options.search_paths {
        if !root.exists() {
            continue;
        }

        let mut queue = vec![root.clone()];
        while let Some(current) = queue.pop() {
            if options.protect_system_dirs && is_system_protected(&current) {
                continue;
            }

            scanned_dirs.push(current.clone());

            if let Ok(entries) = fs::read_dir(&current) {
                for entry in entries.flatten() {
                    let path = entry.path();
                    if let Ok(meta) = entry.metadata() {
                        if meta.is_file() {
                            let size = meta.len();
                            if size < options.min_size {
                                continue;
                            }
                            if let Some(max) = options.max_size {
                                if size > max {
                                    continue;
                                }
                            }

                            all_files.push(FileCandidate {
                                full_path: path.clone(),
                                file_name: path.file_name().unwrap_or_default().to_string_lossy().to_string(),
                                directory_path: current.clone(),
                                size,
                                last_modified: meta.modified().unwrap_or(SystemTime::UNIX_EPOCH),
                                sample_hash: 0,
                                full_sha256: None,
                            });
                        } else if meta.is_dir() && options.recurse_subdirs {
                            if !(options.protect_system_dirs && is_system_protected(&path)) {
                                queue.push(path);
                            }
                        }
                    }
                }
            }
        }
    }

    // Identificacao de pastas vazias
    if options.mode == ScanMode.Both || options.mode == ScanMode.EmptyDirsOnly {
        on_status("Fase 2: Identificando pastas vazias...");
        for dir in scanned_dirs.iter().rev() {
            if let Ok(mut entries) = fs::read_dir(dir) {
                if entries.next().is_none() {
                    let is_sys = is_system_protected(dir);
                    let mtime = fs::metadata(dir).and_then(|m| m.modified()).unwrap_or(SystemTime::UNIX_EPOCH);
                    results.empty_directories.push(EmptyDirectoryInfo {
                        path: dir.clone(),
                        is_system_protected: is_sys,
                        last_modified: mtime,
                    });
                }
            }
        }
    }

    if options.mode == ScanMode.EmptyDirsOnly {
        on_status("Concluido!");
        return Ok(results);
    }

    // Fase 3: Poda por tamanho
    on_status("Fase 3: Filtrando candidatos por tamanho exato...");
    let mut size_groups: HashMap<u64, Vec<FileCandidate>> = HashMap::new();
    for f in all_files {
        size_groups.entry(f.size).or_default().push(f);
    }

    let mut candidate_files = Vec::new();
    for (_sz, files) in size_groups {
        if files.len() > 1 {
            candidate_files.extend(files);
        }
    }

    // Fase 4: Fast Sample Hashing
    on_status("Fase 4: Pre-hashing rapido (Prefix/Suffix)...");
    for f in &mut candidate_files {
        f.sample_hash = compute_sample_hash(&f.full_path, f.size).unwrap_or(0);
    }

    let mut sample_groups: HashMap<(u64, u64), Vec<FileCandidate>> = HashMap::new();
    for f in candidate_files {
        sample_groups.entry((f.size, f.sample_hash)).or_default().push(f);
    }

    let mut hash_candidates = Vec::new();
    for (_key, files) in sample_groups {
        if files.len() > 1 {
            hash_candidates.extend(files);
        }
    }

    // Fase 5: Comparacao Byte a Byte ou Hash Completo (100% de acuracia)
    on_status("Fase 5: Validacao rigorosa (100% acuracia)...");
    let mut final_groups: HashMap<(u64, u64), Vec<FileCandidate>> = HashMap::new();
    for f in hash_candidates {
        final_groups.entry((f.size, f.sample_hash)).or_default().push(f);
    }

    for ((size, _sh), files) in final_groups {
        if files.len() > 1 {
            let mut verified_clusters: Vec<Vec<FileCandidate>> = Vec::new();

            for file in files {
                let mut matched = false;
                for cluster in &mut verified_clusters {
                    if files_are_identical(&cluster[0].full_path, &file.full_path, size)? {
                        cluster.push(file.clone());
                        matched = true;
                        break;
                    }
                }
                if !matched {
                    verified_clusters.push(vec![file]);
                }
            }

            for cluster in verified_clusters {
                if cluster.len() > 1 {
                    results.total_duplicate_files += cluster.len() - 1;
                    results.total_wasted_bytes += size * (cluster.len() - 1) as u64;
                    results.duplicate_groups.push(DuplicateGroup {
                        size,
                        hash: format!("{:016x}", cluster[0].sample_hash),
                        files: cluster,
                    });
                }
            }
        }
    }

    // Ordena do maior para o menor
    results.duplicate_groups.sort_by(|a, b| b.size.cmp(&a.size));

    // Fase 6: Analise de Pastas em Comum
    on_status("Fase 6: Analisando pastas em comum...");
    results.folder_suggestions = analyze_common_folders(&results.duplicate_groups);

    on_status("Concluido!");
    Ok(results)
}

/// Compara dois arquivos byte a byte sequencialmente (100% acuracia sem colisao).
fn files_are_identical(p1: &Path, p2: &Path, size: u64) -> std::io::Result<bool> {
    if size == 0 {
        return Ok(true);
    }
    let mut f1 = File::open(p1)?;
    let mut f2 = File::open(p2)?;

    let mut buf1 = [0u8; 65536];
    let mut buf2 = [0u8; 65536];

    loop {
        let n1 = f1.read(&mut buf1)?;
        let n2 = f2.read(&mut buf2)?;

        if n1 != n2 || buf1[..n1] != buf2[..n2] {
            return Ok(false);
        }
        if n1 == 0 {
            break;
        }
    }
    Ok(true)
}

/// Identifica pastas com concentracao de duplicatas e sugere a exclusao da copia.
pub fn analyze_common_folders(groups: &[DuplicateGroup]) -> Vec<CommonFolderSuggestion> {
    let mut folder_files: HashMap<PathBuf, HashSet<String>> = HashMap::new();
    let mut folder_wasted: HashMap<PathBuf, u64> = HashMap::new();

    for g in groups {
        for f in &g.files {
            folder_files
                .entry(f.directory_path.clone())
                .or_default()
                .insert(f.file_name.clone());

            *folder_wasted.entry(f.directory_path.clone()).or_insert(0) += g.size;
        }
    }

    let mut suggestions = Vec::new();

    for (dir_a, files_a) in &folder_files {
        for (dir_b, files_b) in &folder_files {
            if dir_a == dir_b {
                continue;
            }

            let matches = files_a.iter().filter(|f| files_b.contains(*f)).count();
            if matches > 0 && (matches == files_a.len() || matches >= 3) {
                let is_complete = matches == files_a.len();
                let rec = if is_complete {
                    format!(
                        "Todos os {} arquivos desta pasta existem em '{}'. Recomenda-se excluir as copias desta pasta.",
                        matches,
                        dir_b.display()
                    )
                } else {
                    format!("{} arquivos duplicados compartilhados com '{}'.", matches, dir_b.display())
                };

                suggestions.push(CommonFolderSuggestion {
                    duplicate_folder: dir_a.clone(),
                    primary_folder: dir_b.clone(),
                    duplicate_count: matches,
                    wasted_bytes: *folder_wasted.get(dir_a).unwrap_or(&0),
                    is_complete_duplicate: is_complete,
                    recommendation: rec,
                });
                break;
            }
        }
    }

    suggestions
}

/// Apaga com seguranca pastas vazias nao-protegidas.
pub fn delete_empty_directory(path: &Path, protect_system: bool) -> std::io::Result<bool> {
    if protect_system && is_system_protected(path) {
        return Ok(false);
    }
    if !path.is_dir() {
        return Ok(false);
    }
    let mut entries = fs::read_dir(path)?;
    if entries.next().is_none() {
        fs::remove_dir(path)?;
        return Ok(true);
    }
    Ok(false)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_system_protection() {
        assert!(is_system_protected(Path::new("C:\\Windows\\System32")));
        assert!(is_system_protected(Path::new("C:\\Program Files\\App")));
        assert!(is_system_protected(Path::new("C:\\")));
        assert!(!is_system_protected(Path::new("D:\\MinhasFotos\\Viagem")));
    }
}

