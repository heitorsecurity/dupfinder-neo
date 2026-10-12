# Mapa Arquitetural e Especificacao Tecnica (SOT - Single Source of Truth)
**Projeto:** DupFinder-Neo (by @heitorsecurity)  
**Versao:** v1.0 (1.0.0)  
**Data:** 2026-10-12  
**Finalidade:** Guia completo de arquitetura, tipos de dados, algoritmos e modelo de implementacao para portabilidade agnostica de linguagem (especialmente focado em **Rust**, Go ou C++ moderno).

---

## 1. Visao Geral e Principios Fundamentais

O **DupFinder-Neo v1.0 (by @heitorsecurity)** e um motor de deteccao de duplicatas, monitoramento em tempo real e saneamento de diretorios projetado com cinco principios:
1. **Agilidade Extrema:** Evitar o calculo desnecessario de hashes pesados (SHA-256/MD5) sobre arquivos unicos ou que diferem nos primeiros blocos via amostragem FNV-1a 64-bit.
2. **Acuracia Matematica Absoluta (Zero Falso Positivo):** Todo arquivo considerado duplicado passa por validacao completa antes de ser classificado.
3. **Inteligencia de Pastas (Common Folders):** Em vez de tratar arquivos duplicados de forma isolada, o sistema reconhece quando pastas inteiras sao copias redundantes de outras e sugere a exclusao do lote.
4. **Higienizacao Segura de Pastas Vazias:** Permite localizar e remover pastas orfas vazias, aplicando protecao estrita contra pastas criticas do sistema operacional.
5. **Monitoramento em Tempo Real Opcional (Estilo 'Everything'):** Servico leve em background com deteccao instantanea de arquivos duplicados recem-criados e alerta nativo no Windows.

---

## 2. Pipeline de Processamento (Fluxo de Dados)

```text
[Diretorios Raiz]
       │
       ▼
[Fase 1: Varredura de Sistema de Arquivos]
       ├── Катаloga arquivos (Caminho, Tamanho, Data)
       └── Катаloga pastas vazias (com checagem de protecao de sistema)
       │
       ▼
[Fase 2: Poda de Tamanhos Unicos]
       └── Agrupa por tamanho exato em bytes.
       └── Descarta imediatamente tamanhos com apenas 1 arquivo.
       │
       ▼
[Fase 3: Pre-Hashing Rapido de Blocos (Prefix/Suffix)]
       └── Le amostra dos primeiros 4.096 bytes (e ultimos 4.096 bytes).
       └── Calcula hash FNV-1a de 64 bits.
       └── Descarta arquivos cujas amostras diferem.
       │
       ▼
[Fase 4: Hash Criptografico Completo (SHA-256)]
       └── Executa hash integral nos candidatos que passaram na Fase 3.
       └── Agrupa duplicatas confirmadas: (Tamanho + SHA-256).
       │
       ▼
[Fase 5: Analise Estrutural de Pastas em Comum]
       └── Mapeia duplicatas entre pares de pastas (Pasta A vs Pasta B).
       └── Detecta arvores redundantes e gera sugestoes inteligentes de delecao.
       │
       ▼
[Fase 6: Apresentacao / Acoes de Limpeza]
       ├── Aba 1: Duplicatas (Delecao / Hardlink / Symlink)
       ├── Aba 2: Pastas em Comum (Marcacao em lote)
       └── Aba 3: Pastas Vazias (Exclusao segura)
```

---

## 3. Modelos de Dados (Data Contracts)

### 3.1. Modo de Execucao
```rust
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ScanMode {
    DuplicatesOnly,
    Both,
    EmptyDirsOnly,
}
```

### 3.2. Candidato a Arquivo
```rust
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
```

### 3.3. Grupo de Duplicatas
```rust
#[derive(Debug, Clone)]
pub struct DuplicateGroup {
    pub size: u64,
    pub hash: String,
    pub files: Vec<FileCandidate>,
}
```

### 3.4. Informacao de Pasta Vazia
```rust
#[derive(Debug, Clone)]
pub struct EmptyDirectoryInfo {
    pub path: PathBuf,
    pub is_system_protected: bool,
    pub last_modified: SystemTime,
}
```

### 3.5. Sugestao de Pasta em Comum
```rust
#[derive(Debug, Clone)]
pub struct CommonFolderSuggestion {
    pub duplicate_folder: PathBuf,
    pub primary_folder: PathBuf,
    pub duplicate_count: usize,
    pub wasted_bytes: u64,
    pub is_complete_duplicate: bool,
    pub recommendation: String,
}
```

---

## 4. Algoritmos e Detalhes de Implementacao

### 4.1. Fast Sample Hashing (FNV-1a 64-bit)
* **Objetivo:** Filtrar rapidamente arquivos do mesmo tamanho que possuem conteudo diferente logo nos primeiros bytes.
* **Constantes:**
  * Offset Basis: `14695981039346656037` (`0xcbf29ce484222325`)
  * Prime: `1099511628211` (`0x100000001b3`)
* **Amostragem:**
  * Primeiros $\min(\text{size}, 4096)$ bytes.
  * Se $\text{size} > 8192$, busca os ultimos 4096 bytes do arquivo e continua o hash.

### 4.2. Heuristica de Pastas em Comum (Common Folders)
* Para cada par de pastas $(A, B)$ que compartilham duplicatas:
  * Conta o numero de arquivos identicos: $M = |Files(A) \cap Files(B)|$.
  * Se $M == |Files(A)|$, a pasta $A$ e uma **copia completa (100% redundante)** de $B$.
  * Criterio de preferencia para pasta primaria:
    * Caminho com menor profundidade de diretorio (`dir.components().count()`).
    * Em caso de empate, caminho com menor comprimento de caracteres.
  * O sistema sugere a exclusao dos arquivos duplicados contidos em $A$, preservando os originais em $B$.

### 4.3. Regra de Seguranca de Pastas do Sistema
* Um diretorio e considerado **Protegido pelo Sistema** se corresponder a:
  * Raizes de volume (ex: `C:\`, `/`).
  * Padroes restritos de Windows:
    * `\windows`
    * `\system32`
    * `\syswow64`
    * `\winsxs`
    * `\program files`
    * `\program files (x86)`
    * `\programdata`
    * `\$recycle.bin`
    * `\system volume information`
    * `\recovery`
    * `\$winreagent`
    * `\boot`
    * `\appdata\local\microsoft`
    * `\appdata\roaming\microsoft`
  * Padroes restritos de Linux/Unix:
    * `/bin`, `/sbin`, `/usr`, `/lib`, `/etc`, `/var`, `/boot`, `/proc`, `/sys`, `/dev`
* Pastas protegidas **nunca** sao listadas para exclusao ou removidas pelo motor.

---

## 5. Como Reconstruir em Rust

Para criar este projeto em Rust a partir desta especificacao:
1. Crie o projeto: `cargo new dupfinder-rust`
2. Adicione ao `Cargo.toml`:
   ```toml
   [dependencies]
   sha2 = "0.10"
   walkdir = "2.4"
   rayon = "1.8" # opcional para hashing paralelo
   ```
3. Copie o arquivo `dupfinder_engine.rs` deste diretorio SOT para `src/lib.rs` ou `src/main.rs`.
4. Compile: `cargo build --release`.

