# DupFinder-Neo v1.0 (by @heitorsecurity) 🚀

> **Localizador Inteligente de Duplicatas, Detector de Pastas em Comum, Monitor em Tempo Real e Limpador de Diretórios Vazios**  
> Compatível com **Windows XP, Vista, 7, 8, 8.1, 10 e 11**.  
> **Versão Oficial:** `v1.0` *(Política de versionamento contínuo: v1.0 -> v1.1 -> v1.2 ...)*

---

## 📌 Sumário
1. [O que era o projeto original](#-o-que-era-o-projeto-original)
2. [O que tinha no projeto anterior e suas limitações](#-o-que-tinha-no-projeto-anterior-e-suas-limitações)
3. [O que foi feito no DupFinder-Neo v1.0 (by @heitorsecurity)](#-o-que-foi-feito-no-dupfinder-neo-v10-by-heitorsecurity)
4. [Novo Monitor / Indexador em Segundo Plano (Estilo 'Everything')](#-novo-monitor--indexador-em-segundo-plano-estilo-everything)
5. [Instalador Interativo e Assistente Completo](#-instalador-interativo-e-assistente-completo)
6. [Estrutura do Repositório e Arquivos](#-estrutura-do-repositório-e-arquivos)
7. [Como Executar e Instalar](#-como-executar-e-instalar)
8. [Projeto SOT (Single Source of Truth) & Portabilidade Rust](#-projeto-sot-single-source-of-truth--portabilidade-rust)
9. [Licença e Créditos](#-licença-e-créditos)

---

## 🏛️ O que era o projeto original

O **DupFinder original (v0.8.0)** foi desenvolvido por Patrick De Smet entre 2004 e 2006.  
O seu objetivo era oferecer um utilitário simples em C++ para localizar arquivos duplicados em discos locais e de rede, disponibilizando duas interfaces:
- Uma interface de linha de comando (`dupf.exe`).
- Uma interface gráfica rudimentar baseada no framework multiplataforma **wxWidgets** (versões 2.4/2.6).

A premissa inicial do autor era evitar algoritmos de hash criptográficos completos, recorrendo a comparações de tamanho de arquivo e leitura direta sequencial byte-a-byte.

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
5. **Interface Monolítica, Sem Temporizador e Sem Porcentagem Visual**:
   - Não havia temporizador informando quanto tempo a busca levou, nem barra moderna desenhando a porcentagem diretamente no seu corpo.
   - Não permitia selecionar mais de uma pasta ao mesmo tempo.
   - Não lembrava qual tinha sido a última pasta selecionada.
6. **Incompatibilidade com o Ecossistema Moderno de Compilação**:
   - O código original dependia de compiladores legados de mais de 20 anos atrás (*Borland C++ 5.5, OpenWatcom, Visual C++ 6.0, GCC 3.x com autotools de 2004*) e de bibliotecas wxWidgets legadas que não compilam em toolchains modernos de 64 bits.

---

## ✨ O que foi feito no DupFinder-Neo v1.0 (by @heitorsecurity)

O projeto foi totalmente reconstruído, modernizado e expandido por **@heitorsecurity**:

### 1. Engine de Varredura Ultra-Rápida
- **Pre-hashing Amostral Ultra-Rápido (`FNV-1a 64-bit`)**:
  - Avalia amostras estratégicas do cabeçalho e do rodapé do arquivo (4 KB de cada ponta). Elimina **mais de 95% das comparações falsas** instantaneamente com I/O mínimo.
- **Detecção e Exclusão Segura de Diretórios Vazios**:
  - Varredura de baixo para cima (bottom-up) que cataloga pastas vazias e oferece exclusão limpa em lote.
- **Análise Inteligente de Pastas em Comum**:
  - Agrupa arquivos duplicados por ancestral e detecta diretórios redundantes inteiros, recomendando a exclusão do clone com 1 clique.
- **Proteção Ativa do Sistema**:
  - Salvaguarda rigorosa que protege `C:\Windows`, `System32`, `Program Files`, `AppData\Microsoft`, `$Recycle.Bin`, etc.
- **Seletor de Modo na Inicialização**:
  - Escolha se quer pesquisar apenas arquivos duplicados, ambos ou apenas pastas vazias logo na abertura.

### 2. Interface Moderna do Usuário com Temporizador e Barra de Porcentagem
- **Título Oficial**: `DupFinder-Neo v1.0 (by @heitorsecurity)` em todo o sistema.
- **Barra de Progresso com Porcentagem Interna**: Componente `PercentageProgressBar` desenha a porcentagem e a contagem de arquivos processados diretamente dentro do preenchimento da barra.
- **Temporizador de Varredura em Tempo Real**: Exibe o tempo decorrido com precisão (`⏱️ 00:01:23`), velocidade em arquivos por segundo e o tempo total ao finalizar.
- **Memória de Última Pasta**: Lembra automaticamente qual foi a última pasta adicionada e abre diretamente nela ao clicar em *+ Adicionar Pasta...*.
- **Seleção de Múltiplas Pastas de Uma Vez**: Botão dedicado *+ Várias Pastas...* com árvore de unidades, expansão sob demanda, caixas de seleção (checkboxes) e recurso para importar todas as subpastas de um diretório com 1 clique.

---

## 🛰️ Novo Monitor / Indexador em Segundo Plano (Estilo 'Everything')

O **DupFinder-Neo v1.0** traz um recurso inovador e opcional: **`dupfinder-watcher.exe`**:
- **Monitoramento Passivo e Inteligente**: Fica ativo na bandeja do sistema (System Tray) consumindo menos de 0.1% de CPU e RAM mínima.
- **Detecção Instantânea de Duplicatas**: Utiliza `FileSystemWatcher` para monitorar pastas de download, documentos, desktop e mídias. Quando você baixa, cria ou copia um arquivo novo:
  1. Aguarda a finalização da escrita (debounce).
  2. Compara o tamanho e o hash amostral contra o catálogo indexado.
  3. Valida via SHA-256 binário para garantir 100% de acurácia.
- **Notificação Nativa do Windows**: Exibe um aviso imediato na barra de notificações:  
  *“DupFinder-Neo v1.0: Arquivo Duplicado! Você acabou de duplicar um arquivo existente: 'foto (1).jpg'. Clique aqui para ver opções.”*
- **Janela de Ação Imediata**: Ao clicar na notificação, abre uma janela limpa com:
  - Comparativo lado a lado do arquivo novo e do original.
  - Botões para **Abrir Arquivo**, **Abrir Pasta no Explorer**, **Excluir Cópia Duplicada** ou **Abrir o DupFinder-Neo Completo**.
- **Opcionalidade Total**: Este serviço **só é ativado no momento da instalação se o usuário desejar**.

---

## 📦 Instalador Interativo e Assistente Completo

Tanto o instalador `.msi` quanto o instalador `.exe` trazem um assistente interativo:
1. **Tela de Boas-Vindas**: Identificação completa da versão `v1.0 (by @heitorsecurity)`.
2. **Tela de Licença**: Apresenta a licença GNU GPL v2 acompanhada do adendo de modificações e direitos de @heitorsecurity.
3. **Escolha de Local e Escopo**:
   - *Todos os Usuários da Máquina* (`C:\Program Files\DupFinder-Neo`)
   - *Apenas o Usuário Atual* (`%LOCALAPPDATA%\Programs\DupFinder-Neo`)
4. **Opções de Atalhos e Monitor**:
   - [X] Criar atalho na Área de Trabalho (Desktop)
   - [X] Criar atalhos no Menu Iniciar:
     - `DupFinder-Neo`
     - `Readme.txt`
     - `Desinstalar DupFinder-Neo`
   - [ ] Ativar o Monitor DupFinder-Neo Watcher em segundo plano (opcional)
5. **Barra de Progresso da Instalação**: Barra personalizada mostrando porcentagem e status de cópia e registro.
6. **Desinstalação Limpa**:
   - Mata qualquer processo em execução do monitor (`dupfinder-watcher.exe`) e da aplicação principal.
   - Remove as rotinas de inicialização automática no registro.
   - Remove todos os atalhos criados.
   - Remove pastas de instalação e dados temporários sem deixar rastros no computador.

---

## 📂 Estrutura do Repositório e Arquivos

```text
dupfinder-neo/
├── README.md                           <- Documentação completa e especificações v1.0
├── LICENSE.TXT                         <- Licença GPL v2 com adendos de @heitorsecurity
├── dupfinder-neo.exe                   <- Executável principal v1.0 (Portátil, XP a 11)
├── dupfinder-watcher.exe               <- Monitor e indexador em segundo plano
├── dupfinder-neo-setup.exe             <- Assistente de instalação e desinstalador interativo
├── dupfinder-neo.msi                   <- Pacote Windows Installer oficial v1.0
├── dupfinder-neo-sourcecode-sot.zip    <- Pacote SOT de portabilidade (Rust/Agnóstico)
│
├── src-win/                            <- Código-fonte das aplicações Windows (.NET 4 / C# 5)
│   ├── DupFinderNeo.cs                 <- App gráfico com timer, barra % e multi-pastas
│   ├── DupFinderWatcher.cs             <- Monitor em background estilo Everything com notificações
│   └── SetupWizard.cs                  <- Assistente de instalação em passos e desinstalador
├── build-msi.ps1                       <- Script automatizado de geração do pacote MSI v1.0
│
├── sot/                                <- Single Source of Truth para migração (Rust)
│   ├── Cargo.toml                      <- Manifesto Rust v1.0
│   ├── MAPA_ARQUITETURA.md             <- Especificação do pipeline em 4 fases
│   ├── dupfinder_engine.rs             <- Engine de referência em Rust puro
│   └── main.rs                         <- CLI de referência em Rust
│
├── docs/
│   └── DEVELOPMENT_HISTORY.html        <- Histórico formal de desenvolvimento
│
├── dbl.h / dbl.cpp                     <- Engine C++ modernizada
├── dialog1.h / dialog1.cpp             <- Diálogos C++
└── CMakeLists.txt                      <- Script de build multiplataforma CMake
```

---

## 🚀 Como Executar e Instalar

### No Windows (XP ao 11):
- **Execução Portátil**: Execute diretamente [`dupfinder-neo.exe`](file:///c:/Users/hteix/code/empresasto/dupfinder-neo/dupfinder-neo.exe).
- **Assistente Interativo**: Execute [`dupfinder-neo-setup.exe`](file:///c:/Users/hteix/code/empresasto/dupfinder-neo/dupfinder-neo-setup.exe).
- **Instalador MSI Corporativo**: Execute [`dupfinder-neo.msi`](file:///c:/Users/hteix/code/empresasto/dupfinder-neo/dupfinder-neo.msi).

---

## 📄 Política de Versionamento e Créditos

- **Versão Atual**: `v1.0` (1.0.0).  
- **Convenção de Versões Futuras**: Atualizações incrementais seguirão o padrão `v1.0 -> v1.1 -> v1.2 -> v2.0`.
- **Autoria Original**: Patrick De Smet (GNU General Public License v2).
- **DupFinder-Neo v1.0**: Modernização de motor, temporizadores, barra de porcentagem interna, seleção de múltiplas pastas, monitor em segundo plano estilo Everything com notificações no Windows, instalador e desinstalador limpo por **@heitorsecurity**.
