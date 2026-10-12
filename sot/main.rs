mod dupfinder_engine;

use dupfinder_engine::{run_scan, EngineOptions, ScanMode};
use std::env;
use std::path::PathBuf;

fn main() {
    println!("=== DupFinder-Neo (Rust Reference CLI) ===");

    let args: Vec<String> = env::args().collect();
    if args.len() < 2 {
        println!("Uso: dupfinder-rust <diretorio1> [diretorio2] ...");
        println!("Exemplo: dupfinder-rust C:\\Users\\User\\Documents");
        return;
    }

    let mut options = EngineOptions::default();
    options.mode = ScanMode::Both;

    for arg in &args[1..] {
        options.search_paths.push(PathBuf::from(arg));
    }

    match run_scan(&options, |status| println!("[Status] {}", status)) {
        Ok(results) => {
            println!("\n=== RESULTADOS ===");
            println!("Total de grupos duplicados: {}", results.duplicate_groups.len());
            println!("Arquivos duplicados redundantes: {}", results.total_duplicate_files);
            println!("Espaco desperdicado: {:.2} MB", results.total_wasted_bytes as f64 / 1024.0 / 1024.0);

            println!("\n--- Sugestoes de Pastas Redundantes ({}) ---", results.folder_suggestions.len());
            for sug in &results.folder_suggestions {
                println!("* Pasta: {}", sug.duplicate_folder.display());
                println!("  Original: {}", sug.primary_folder.display());
                println!("  Sugestao: {}", sug.recommendation);
            }

            println!("\n--- Pastas Vazias Encontradas ({}) ---", results.empty_directories.len());
            for ed in &results.empty_directories {
                println!("* {} [Seguro: {}]", ed.path.display(), !ed.is_system_protected);
            }
        }
        Err(e) => {
            eprintln!("Erro durante a execucao: {}", e);
        }
    }
}

