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
- `python3 scripts/audit_upstream_snapshot.py` — verifica os scripts, armas, modelos, texturas, sons e cenas originais que já estão no repositório.\n- `python3 scripts/verify_imported_models.py` — valida os dois arquivos FBX reais contra o hash Git e checa sua geometria.\n- `python3 scripts/audit_content.py` — auditoria honesta da presença dos prefabs.
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
