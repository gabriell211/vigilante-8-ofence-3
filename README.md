# Vigilante 8: Offence 3

**Projeto Windows x64, nativo em Unity 6.6.** Reúne os personagens, veículos e arenas de *Vigilante 8* e *Vigilante 8: 2nd Offense*, com uma identidade por personagem e sem repetir o mesmo carro.

> **Objetivo da distribuição:** instalar e jogar diretamente no PC. Sem emulador, BIN/CUE, seleção de discos ou importação de arquivos pelo jogador. Os recursos definitivos devem ser empacotados no build Unity.

## Situação real da implementação

| Área | Estado verificável |
| --- | --- |
| Catálogo unificado | **23 personagens, 31 veículos, 19 arenas**; IDs únicos e aliases |
| Código de combate | Direção arcade, tiros, dano, câmera, inimigos IA, controles e seleção dinâmica |
| Pipeline Windows | Gerador de cena, validação de prefabs e empacotamento IL2CPP/ZIP/SHA-256 |
| Modelos, texturas e arenas reais | **Acervo Unity original importado** para `LegacySources/Vigilante2Unity/Assets`: veículos, prefabs, texturas, áudios e cenas do 2nd Offense. **Migração para prefabs ativos Unity 6.6 ainda pendente** |
| Áudio, armas especiais e campanhas | Pendentes de implementação e homologação |
| Build executável | **Ainda não compilado/homologado no Unity** |

**Acervo original do Vigilante2Unity completo no repositório, 0 prefabs jogáveis Unity 6.6 homologados.** Além dos dois FBX de referência, agora há a árvore original completa do projeto Unity 2020 (scripts, texturas, armas, áudio, cenas e prefabs). Os arquivos foram preservados fora da pasta ativa `Assets/` para não introduzir erros de compilação no projeto Unity 6.6. Os recursos referenciados por caminhos legados devem ser associados na migração. **Catálogo ≠ asset integrado.** Os nomes e relações de veículos estão cadastrados; isso ainda não coloca suas malhas e texturas dentro do jogo. O script `python3 scripts/audit_content.py` informa exatamente quantos prefabs estão materialmente presentes. Não será publicada uma release com modelos fictícios ou arquivos inexistentes.

## Conteúdo dos dois jogos

- Cada personagem aparece **uma única vez** no seletor; veículos de jogos diferentes continuam disponíveis quando forem realmente diferentes.
- O pipeline foi estruturado para empacotar os **31 veículos e 19 arenas** no executável Windows.
- A referência de conteúdo abrange os dois jogos, sem depender deles no computador do jogador.
- Você informou possuir os direitos/autorização para usar os arquivos. A próxima etapa de integração exige que os **arquivos de mídia reais estejam acessíveis no ambiente de desenvolvimento** (eles não aparecem automaticamente pela autorização).

## Ferramentas e verificação

- Unity **6000.6.7f1**, URP, Input System e IL2CPP.
- `python3 scripts/validate_catalog.py` — integridade de personagens, carros e arenas.
- `python3 -m unittest discover -s tests -v` — regressões estruturais.
- `python3 scripts/audit_upstream_snapshot.py` — verifica os scripts, armas, modelos, texturas, sons e cenas originais que já estão no repositório.
- `python3 scripts/verify_imported_models.py` — valida os dois arquivos FBX reais contra o hash Git e checa sua geometria.
- `python3 scripts/audit_content.py` — auditoria honesta da presença dos prefabs.
- `python3 scripts/audit_content.py --strict` — bloqueia a etapa de release se faltar um só prefab.
- `python3 scripts/verify_pc_contract.py` — verifica o contrato do código de PC; não substitui execução no Editor.

No Unity, o comando **Vigilante 8 > Preparar cena PC nativa** cria a cena inicial e **Vigilante 8 > Build Windows x64 IL2CPP com assets** valida os recursos e empacota o Windows. O resultado deverá conter o `.exe`, a pasta `_Data` e as dependências geradas pelo Unity; não é um EXE de arquivo único.

Leia [pipeline de conteúdo](docs/IMPORT_PIPELINE.md) e [distribuição nativa](docs/PC_STANDALONE.md). A documentação descreve o ambiente de desenvolvimento, **não tarefas que serão exigidas do jogador**.

## Material efetivamente aproveitado do GitHub

Os FBX **Tsunami** e **Blue Burro Bus** foram versionados em `Assets/GameContent/SourceModels/Vigilante2Unity/`. O inventário com origem, hashes e limitações está em `Assets/GameContent/imported-sources.json`. São meshes reais, não carros genéricos ou prefabs prontos. O upstream [Vigilante2Unity](https://github.com/stefanvranjes/Vigilante2Unity) usa Unity 2020.3.3f1; seus shaders/materiais precisam de conversão para a URP moderna. A recompilação [Vigilante-8-recomp](https://github.com/GTTeancum/Vigilante-8-recomp) deliberadamente não versiona as mídias comerciais base. A integração das demais mídias depende da sua presença real nas fontes, não só de nomes de arquivo no código.

## Acervo original importado em massa

**Acervo realmente versionado:** 7.937 arquivos no snapshot Unity do 2nd Offense (191.352.015 bytes) e 4.549 arquivos no snapshot de referência Classic (incluindo o manifesto de origem). A importação preserva os projetos legados, inclusive arquivos `.csproj` antes ignorados; nenhuma dessas árvores é compilada diretamente pela Unity 6.6.



O workflow [Import complete Vigilante2Unity source archive](.github/workflows/import-v82-source.yml) executou e copiou a árvore original do projeto [stefanvranjes/Vigilante2Unity](https://github.com/stefanvranjes/Vigilante2Unity) para [LegacySources/Vigilante2Unity/Assets](LegacySources/Vigilante2Unity/Assets), com revisão de origem fixada em `d20883df0d5da3ba517142fb4d486db1f5e673fb`.

A árvore contém **scripts C# de veículos e armas** (incluindo MGun, Missile, Rocket, Mortar, Mine, FlameThrower, Cannon e armas especiais), **prefabs originais**, materiais, milhares de texturas, WAV e cenas de arena. Os arquivos permanecem preservados sob seus nomes e hierarquia originais, com metadados `.meta`; não são representações geradas ou placeholders.

**Próxima etapa técnica:** converter os scripts de 2020, materiais PSX e prefabs de origem para sistemas compiláveis na Unity 6.6, sem colidir com o código nativo do jogo. O catálogo do primeiro Vigilante 8 continua cadastrando todos os veículos, mas materiais comerciais desse jogo não são automaticamente incluídos no repositório [GTTeancum/Vigilante-8-recomp](https://github.com/GTTeancum/Vigilante-8-recomp), que os ignora no Git. Portanto, a importação de fonte do segundo título não prova que **31 veículos e 19 arenas** já sejam jogáveis no projeto final.

## Vigilante 8 Classic — fontes gráficas e ferramentas incluídas

O acervo público do [GTTeancum/Vigilante-8-recomp](https://github.com/GTTeancum/Vigilante-8-recomp) foi integralmente arquivado em [LegacySources/Vigilante8ClassicRecomp](LegacySources/Vigilante8ClassicRecomp), fixado na revisão `54805112186f970b05777540627ab5bdd084ddb7`. Essa árvore complementa o Unity 2020 com código de renderização, conversores, bibliotecas, referências de geometria, mods e texturas de interface.

**Duas auditorias** são obrigatórias no CI: `python3 scripts/audit_upstream_snapshot.py` (Unity original do segundo jogo) e `python3 scripts/audit_recomp_snapshot.py` (recompilação Classic dos dois jogos).

**O que NÃO deve ser confundido:** os repositórios públicos não incluem todos os arquivos comerciais base que a recompilação utiliza. Os dois acervos têm scripts, texturas e outros recursos reais, mas ainda é preciso migrar componentes para prefabs modernos da Unity 6.6 e homologar as armas, cenários e veículos. **Nenhum ZIP ou EXE do jogo completo foi gerado nesta etapa.**


**Cobertura oficial entre versões:** 13 veículos do primeiro jogo, 18 do segundo, 23 identidades sem repetição e 19 fases ao incluir Super Dreamland 64 (bônus exclusivo N64). O elenco é verificado por nomes, personagens e plataformas em `tests/fixtures/official_content.json`, não apenas por contagem. A presença no catálogo não implica prefab jogável ou build homologado.


## Mapeamento de modelos oficiais e bloqueio de duplicatas

O inventário [official-asset-sources.json](docs/official-asset-sources.json) associa
os **18 veículos do 2nd Offense** aos respectivos prefabs legados, as **8 arenas**
às cenas Unity originais e **20 scripts de armas** às fontes importadas. O validador
`scripts/verify_official_asset_sources.py` confirma que os arquivos físicos
existem e informa a cobertura real no Unity 6.

O bloqueio de build também exige `OfficialAssetIdentity` em cada prefab final,
verifica se o ID corresponde ao veículo/arena correto e rejeita reutilizar a
mesma malha principal para representar carros ou fases oficiais diferentes.
Para identificar um prefab autorado já pronto, selecione-o no Unity e use
**Vigilante 8 > Vincular identidade oficial ao prefab selecionado**.

**Importante:** importar um arquivo do Unity 2020 ou vinculá-lo pelo nome não
conclui sua migração. Ainda será preciso adaptar física, materiais, armamentos
especiais, scripts, campanhas, animações e testar em Windows antes de produzir
uma versão jogável. O CI testa integridade e cobertura do acervo, não a
compilação ou o funcionamento do jogo.

## Migração de geometria, texturas e áudio do 2nd Offense

A fase atual já inclui os seguintes dados **reais** do repositório original:

- **18/18 veículos do segundo jogo:** geometria PS1 extraída em `Assets/GameContent/ExtractedV82/`, com verificação de triângulos e hashes da fonte
- **790 BMPs originais:** importados com GUIDs de textura preservados em `Assets/GameContent/LegacyTextureBank/`
- **18 prévias OBJ/MTL:** a geometria real possui **677 materiais ligados diretamente às texturas** nos modos TMD conhecidos, em `Assets/GameContent/TexturedPreviewV82/`
- **235 WAVs originais:** importados com GUIDs preservados em `Assets/GameContent/LegacyAudioV82/`; os 18 veículos têm **88 amostras indexadas**, as oito arenas possuem **54**, e os demais efeitos permanecem em seus bancos originais
- **Catálogo para o Unity:** `Assets/GameContent/LegacyAudioV82/catalog.json` vincula os sons às identidades oficiais por dados reais, sem IDs de veículos codificados manualmente

### Ferramentas no Unity

O menu **Vigilante 8 > Migração > Gerar galeria 3D dos 18 carros oficiais**
prepara materiais URP, cria prefabs de inspeção, vincula os WAVs e monta
a cena `OriginalVehicleReview.unity`. A galeria serve exclusivamente à
inspeção visual e à migração. Consulte [o procedimento](docs/UNITY_ASSET_REVIEW.md).

**Não confundir com uma release jogável:** a estrutura de peças, transformações,
efeitos especiais, sistemas originais das armas, colisões, 13 modelos do primeiro
jogo e arenas definitivas continuam pendentes. Os projetos públicos de origem
não disponibilizam todos os dados 3D comerciais do primeiro jogo. Os testes Python
verificam cópias e integridade do conteúdo; **ainda não houve compilação nem
teste PlayMode/EditMode homologado com o Editor Unity 6.6**.


### Confiabilidade do importador Unity

O conversor `OriginalObjMeshBuilder.cs` lê os polígonos, UVs e nomes
de materiais diretamente dos arquivos originais, preservando os grupos
de peças. O CI confirma essas relações nos 18 carros. Essa etapa evita
erros causados pela interpretação automática de `.mtl` do Unity, mas
ainda não substitui teste visual do motor e não libera o EXE.

### Compilação real em Unity 6.6

O workflow manual [Unity 6.6 Editor tests](.github/workflows/unity-editmode.yml) executa o Editor e testes EditMode com GameCI, quando houver licença Unity disponível. O CI automático atual verifica apenas contratos, origem e integridade dos assets. Leia [instruções de homologação do Editor](docs/UNITY_CI.md).

## Menu nativo do jogo

O UI Toolkit já implementa **menu principal, garagem com seleção oficial, opções de volume/gráficos/tela cheia, controles, pausa, vitória/derrota, repetir partida e sair**, com valores dinâmicos do catálogo. A disponibilidade de cada combate depende dos prefabs reais homologados. Configurações e últimas escolhas são persistidas no PC. Veja [documentação do menu](docs/GAME_MENU.md). A validação automática não substitui abrir o projeto no Editor Unity 6.6.

## Oficina: veículos reais dirigíveis em modo de desenvolvimento

A ferramenta **Vigilante 8 > Migração > Oficina - testar os 18 veículos originais**
permite selecionar cada carro oficial do segundo jogo e gerar protótipos
de **direção e colisão no Unity 6** a partir das malhas TMD reais, BMPs e
WAVs já importados. Usa `VehicleDrive` e `PlayerVehicleInput`,
Rigidbody e colisores calculados do próprio modelo. O disparo é um
**projétil invisível de diagnóstico**: ainda não é a arma especial original.

Os prefabs são salvos em
`Assets/GameContent/PlaytestVehiclesV82/`, **fora de Resources**.
O cenário técnico tem somente chão invisível e **não é arena oficial**.
O build Windows recusa os marcadores de protótipo e cenas de teste, para
impedir publicação de um jogo incompleto. Veja
[manual da oficina e testes](docs/LEGACY_VEHICLE_PLAYTEST.md).

As **8 cenas reais do segundo jogo** foram auditadas por SHA-256,
estrutura Unity e referências em
`Assets/GameContent/LegacyStageManifests/scene-audit.json`, somando
44,3 MB de fontes originais. Esses cenários ainda dependem de portar
LevelManager/VigMesh, objetos interativos, colisores e spawns ao Unity 6;
inventariar as cenas não as torna jogáveis.

**Não está tudo pronto para uma release:** os 13 modelos exclusivos do
primeiro jogo não estão integrados como recursos Unity 6 e os 19
cenários continuam sem prefabs jogáveis certificados. O CI automático
confere o código e as fontes; a compilação/renderização real exige
executar os testes no Editor Unity.
