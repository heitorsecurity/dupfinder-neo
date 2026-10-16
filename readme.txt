================================================================================
DupFinder-Neo v1.4 (by @heitorsecurity)
================================================================================

O PROGRAMA:
-----------
O DupFinder-Neo e um localizador ultra-rapido de arquivos duplicados, detector de
pastas espelhadas em comum e limpador seguro e robusto de pastas vazias,
compativel com Windows XP, Vista, 7, 8, 8.1, 10 e 11.

COMPONENTES E AUTORIA:
----------------------
1. Motor Original DupFinder:
   Desenvolvido originalmente por Patrick De Smet (GNU GPL v2 ou posterior).
2. Motor de Pastas Vazias (Remove Empty Directories - RED):
   Desenvolvido por Jonas John & hxseven (https://github.com/hxseven/Remove-Empty-Directories/).
   Incorpora deteccao de pastas contendo apenas residuos do SO (desktop.ini,
   Thumbs.db, .DS_Store, arquivos de 0 bytes) e ordenacao bottom-up.
3. Modernizacao, Arquitetura e Extensoes DupFinder-Neo:
   Desenvolvido por @heitorsecurity (GNU GPL v3.0).
   - Pre-hashing amostral FNV-1a 64-bit ultra-rapido.
   - Limpador robusto Win32 com neutralizacao de atributos (ReadOnly, Hidden, System).
   - Suporte a caminhos longos (\\?\).
   - Cache persistente em duas camadas (resultados e hashes).
   - Monitor em tempo real estilo 'Everything' (dupfinder-watcher.exe).
   - Interface com redimensionamento automatico e barra com porcentagem interna.
   - Instalador interativo e MSI nativo com desinstalacao limpa.

INSTALACAO:
-----------
No Windows, utilize o instalador oficial dupfinder-neo.msi ou dupfinder-neo-setup.exe.

USO:
----
Interface Grafica (dupfinder-neo.exe):
1. Adicione as pastas desejadas clicando em "+ Adicionar Pasta..." ou "+ Varias Pastas...".
2. Selecione o modo de varredura (Duplicatas, Pastas Vazias ou Ambos).
3. Clique em "Iniciar Varredura".
4. Ao concluir, marque os itens desejados ou utilize as sugestoes inteligentes.
5. Clique para excluir as duplicatas marcadas ou pastas vazias com seguranca.

LICENCA:
--------
Licenciado sob a GNU General Public License Versao 3.0 (GNU GPL v3.0).
Consulte o arquivo LICENSE.TXT para ler o texto integral da licenca.
