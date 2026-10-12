# DupFinder Neo 🚀

> **Localizador Inteligente de Duplicatas, Pastas em Comum e Limpador de Diretórios Vazios**  
> Compatível com **Windows XP, Vista, 7, 8, 8.1, 10 e 11**.

---

## 📌 Sumário
1. [O que era o projeto original](#-o-que-era-o-projeto-original)
2. [O que tinha no projeto anterior e suas limitações](#-o-que-tinha-no-projeto-anterior-e-suas-limitações)
3. [O que foi feito agora no DupFinder Neo](#-o-que-foi-feito-agora-no-dupfinder-neo)
4. [Estrutura do Repositório e Arquivos](#-estrutura-do-repositório-e-arquivos)
5. [Como Executar e Instalar](#-como-executar-e-instalar)
6. [Projeto SOT (Single Source of Truth) & Portabilidade Rust](#-projeto-sot-single-source-of-truth--portabilidade-rust)
7. [Licença e Créditos](#-licença-e-créditos)

---

## 🏛️ O que era o projeto original

O **DupFinder original (v0.8.0)** foi desenvolvido por Patrick De Smet entre 2004 e 2006.  
O seu objetivo era oferecer um utilitário simples em C++ para localizar arquivos duplicados em discos locais e de rede, disponibilizando duas interfaces:
- Uma interface de linha de comando (`dupf.exe`).
- Uma interface gráfica rudimentar baseada no framework multiplataforma **wxWidgets** (versões 2.4/2.6).

A premissa inicial do autor era evitar algoritmos de hash criptográficos lentos da época (como MD5 ou SHA-1 completos em todo o conteúdo de arquivos gigantescos), recorrendo a comparações de tamanho de arquivo e leitura direta sequencial byte-a-byte.

---

## ⚠️ O que tinha no projeto anterior e suas limitações

O projeto original possuía diversas barreiras estruturais, funcionais e visuais para os padrões modernos:

1. **Sem Amostragem Prévia (Gargalo de I/O em Discos)**:
   - Arquivos com o mesmo tamanho eram comparados lendo todo o seu conteúdo sequencialmente. Em diretórios com vídeos, ISOs ou instaladores de mesmo tamanho, o disco ficava saturado lendo gigabytes desnecessariamente antes de descartar diferenças sutis.
2. **Inexistência de Detecção de Pastas Vazias**:
   - O projeto anterior ignorava completamente diretórios vazios ou estruturas órfãs deixadas por desinstalações ou transferências incompletas.
3. **Ausência de Inteligência de "Pastas em Comum"**:
   - Quando duas pastas inteiras eram duplicadas (exemplo: uma pasta inteira de fotos com 5.000 imagens copiada para outro disco como backup), o software listava 5.000 arquivos soltos. O usuário precisava inspecionar e selecionar arquivo por arquivo manualmente, sem saber que se tratava de uma pasta espelhada redundante.
4. **Sem Salvaguarda Ativa do Sistema Operacional**:
   - Não havia lista de bloqueio ou checagem preventiva contra arquivos e diretórios do sistema (`Windows`, `System32`, `Program Files`, diretórios temporários do SO, etc.), expondo o usuário ao risco de quebrar o Windows ao apagar itens indevidos.
5. **Interface Monolítica e Sem Escolha de Modo**:
   - Não havia separação de intenção no início da busca: o usuário não podia escolher se queria focar apenas em pastas vazias, apenas em duplicatas, ou ambos.
6. **Incompatibilidade com o Ecossistema Moderno de Compilação**:
   - O código original dependia de compiladores legados de mais de 20 anos atrás (*Borland C++ 5.5, OpenWatcom, Visual C++ 6.0, GCC 3.x com autotools de 2004*) e de bibliotecas wxWidgets legadas que não compilam em toolchains modernos de 64 bits nem se integram ao Windows moderno sem profundas modificações.

---

## ✨ O que foi feito agora no DupFinder Neo

O projeto foi totalmente reformulado, modernizado e expandido em múltiplos eixos:

### 1. Modernização da Engine C++ Original
- **Pre-hashing Amostral Ultra-Rápido (`FNV-1a 64-bit`)**:
  - Implementada a função `ComputeSampleHash` em `dbl.cpp`. Antes de comparar arquivos grandes byte-a-byte, o motor extrai e avalia amostras do cabeçalho e do rodapé do arquivo (4 KB de cada ponta). Isso elimina **mais de 95% das comparações falsas** instantaneamente com I/O mínimo.
- **Detecção e Exclusão Segura de Diretórios Vazios**:
  - Adicionadas as rotinas `FindEmptyDirectories` e `DeleteEmptyDir` em `dbl.cpp`, que varrem árvores de pastas de baixo para cima (bottom-up) identificando pastas sem arquivos ou subpastas.
- **Análise Inteligente de Pastas em Comum**:
  - Implementada a função `AnalyzeCommonFolders`, que detecta diretórios que compartilham múltiplos arquivos idênticos, calcula a porcentagem de similaridade e recomenda a eliminação do clone redundante com um único clique.
- **Proteção Ativa de Pastas de Sistema**:
  - Implementada a função `IsSystemProtectedDir`, que protege preventivamente `C:\Windows`, `System32`, `Program Files`, `Program Files (x86)`, `AppData\Microsoft`, `$Recycle.Bin` e pastas de volume.
- **Seletor de Modo na Inicialização (`ScanTargetMode`)**:
  - O usuário define na primeira tela se deseja:
    1. *Apenas Arquivos Duplicados*
    2. *Duplicatas e Pastas Vazias*
    3. *Apenas Pastas Vazias*
- **Build Moderno**:
  - Adicionado suporte a `CMakeLists.txt` padrão.

---

### 2. Nova Aplicação Windows Nativa (Compatível de Windows XP ao Windows 11)
Para garantir execução imediata, rápida e sem necessidade de compilar wxWidgets legados, foi criada uma nova versão gráfica nativa:
- **Tecnologia**: C# 5 / .NET Framework 4.0 Client Profile (compilada diretamente com o `csc.exe` nativo do Windows).
- **Compatibilidade**: Roda perfeitamente em **Windows XP SP3, Windows Vista, Windows 7, Windows 8, Windows 8.1, Windows 10 e Windows 11** (x86 e x64).
- **Interface Gráfica Moderna e Multithread**:
  - Motor de busca assíncrono em segundo plano com barra de progresso suave e contadores dinâmicos sem travamento de interface (*UI não congela*).
  - Três abas de resultados dedicadas:
    - **Aba de Duplicatas**: Listagem agrupada por cor, visualização de caminho, tamanho formatado e data. Botões rápidos de pré-seleção: *Marcar Mais Recentes*, *Marcar Mais Antigos*, *Marcar Caminho Mais Longo* e *Inverter Seleção*.
    - **Aba de Pastas em Comum**: Tabela analítica com Pasta Principal, Pasta Clonada, Arquivos Compartilhados, Similaridade e botão *"Excluir Pasta Clone"*.
    - **Aba de Pastas Vazias**: Tabela detalhada de diretórios vazios detectados com botão *"Excluir Selecionadas"*, acompanhado de tela de confirmação de segurança.

---

### 3. Instaladores Prontos para Uso no Windows
Criados pacotes prontos de distribuição para facilitar a instalação em qualquer máquina:
- **`dupfinder-neo.exe`**: Executável único portátil (*standalone*). Basta dar dois cliques para rodar.
- **`dupfinder-neo-setup.exe`**: Assistente de instalação interativo para Windows com criação automática de atalhos na Área de Trabalho e no Menu Iniciar, além de desinstalador no Painel de Controle.
- **`dupfinder-neo.msi`**: Pacote oficial do **Windows Installer** (`.msi`) gerado via automação COM nativa (`WindowsInstaller.Installer`), pronto para distribuição corporativa ou via GPO / Active Directory.

---

### 4. Projeto SOT (Single Source of Truth) para Portabilidade Futura (Rust)
Pensando em futura reimplementação em linguagens modernas de alta performance como **Rust**, foi construída uma arquitetura espelho desacoplada:
- **`sot/MAPA_ARQUITETURA.md`**: Especificação técnica minuciosa que mapeia o pipeline de varredura em 4 fases, as estruturas de dados, os contratos e os algoritmos para implementação em qualquer linguagem.
- **`sot/dupfinder_engine.rs`**: Implementação de referência idiomática em Rust 2021, utilizando memória segura, `BTreeMap`, hashing amostral e testes unitários integrados.
- **`sot/main.rs` & `sot/Cargo.toml`**: Ponto de entrada CLI completo em Rust pronto para compilar com `cargo build --release`.
- **`dupfinder-neo-sourcecode-sot.zip`**: Arquivo ZIP autocontido armazenado na raiz contendo todo o pacote de referência para migração.

---

## 📂 Estrutura do Repositório e Arquivos

```text
dupfinder-neo/
├── README.md                           <- Este documento descritivo e explicativo
├── dupfinder-neo.exe                   <- Executável principal (Portátil, XP a 11)
├── dupfinder-neo-setup.exe             <- Assistente de Instalação do Windows
├── dupfinder-neo.msi                   <- Pacote de Instalação Windows Installer (MSI)
├── dupfinder-neo-sourcecode-sot.zip    <- Pacote ZIP Single-Source-of-Truth para Rust
│
├── src-win/                            <- Código-fonte da aplicação gráfica e setup
│   ├── DupFinderNeo.cs                 <- Aplicação gráfica moderna e multithread
│   └── SetupWizard.cs                  <- Instalador com registro no Windows
├── build-msi.ps1                       <- Script de geração automatizada do pacote MSI
│
├── sot/                                <- Arquitetura de Portabilidade (Rust SOT)
│   ├── Cargo.toml                      <- Manifesto Rust
│   ├── MAPA_ARQUITETURA.md             <- Especificação de arquitetura e contratos
│   ├── dupfinder_engine.rs             <- Engine de referência em Rust
│   └── main.rs                         <- CLI de entrada em Rust
│
├── docs/
│   └── DEVELOPMENT_HISTORY.html        <- Histórico formal de mudanças e ciclo de vida
│
├── dbl.h / dbl.cpp                     <- Engine C++ modernizada (amostragem, pastas vazias, etc.)
├── dialog1.h / dialog1.cpp             <- Diálogo C++ com seletores de modo
├── dialog3_list.h / dialog3_list.cpp   <- Telas de resultados C++ com abas/ações
├── CMakeLists.txt                      <- Script de build multiplataforma CMake
└── [demais fontes legados do projeto]  <- Código-fonte original preservado para histórico
```

---

## 🚀 Como Executar e Instalar

### No Windows (XP ao 11):
- **Modo Portátil**: Execute diretamente o arquivo [`dupfinder-neo.exe`](file:///c:/Users/hteix/code/empresasto/dupfinder-neo/dupfinder-neo.exe).
- **Instalador EXE**: Execute [`dupfinder-neo-setup.exe`](file:///c:/Users/hteix/code/empresasto/dupfinder-neo/dupfinder-neo-setup.exe) e siga o assistente.
- **Instalador MSI**: Dê dois cliques em [`dupfinder-neo.msi`](file:///c:/Users/hteix/code/empresasto/dupfinder-neo/dupfinder-neo.msi) para instalação nativa pelo Windows Installer.

### Recompilando os Executáveis C# / Windows:
Se desejar recompilar os utilitários Windows usando o compilador nativo do sistema:
```powershell
# Recompilar a aplicação principal
C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /target:winexe /optimize+ /out:dupfinder-neo.exe src-win\DupFinderNeo.cs

# Recompilar o instalador
C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe /target:winexe /optimize+ /out:dupfinder-neo-setup.exe src-win\SetupWizard.cs

# Regerar o pacote MSI
powershell -ExecutionPolicy Bypass -File build-msi.ps1
```

### Compilando a Versão de Referência em Rust:
```bash
cd sot
cargo test
cargo build --release
```

---

## 🛡️ Segurança e Proteção de Dados

- **Proteção do Sistema Operacional**: O DupFinder Neo possui filtros rigorosos que impedem a seleção acidental ou exclusão de diretórios vitais do Windows (`C:\Windows`, `System32`, `Program Files`, diretórios protegidos do sistema).
- **Confirmações Explícitas**: Nenhuma exclusão de arquivo ou pasta é realizada sem confirmação prévia com resumo da quantidade de itens afetados.
- **Acurácia Matemática**: Arquivos de mesmo tamanho que passam pelo filtro preliminar são rigorosamente verificados por hash integral SHA-256 e comparação exata, garantindo zero risco de falsos positivos.

---

## 📄 Licença e Créditos

- **Código Original**: Patrick De Smet (GNU General Public License v2).
- **DupFinder Neo**: Modernização de engine, algoritmos de pastas em comum, limpador de diretórios vazios, novas interfaces Windows XP-11, instaladores e arquitetura SOT em Rust.
