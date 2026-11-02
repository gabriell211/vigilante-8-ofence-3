# Arquivos BIN recebidos em novembro de 2026

As imagens locais em `Vigilante 8/` foram verificadas como faixas de dados PlayStation MODE2/2352. Elas permanecem fora do Git e não entram no build do jogador.

| Fonte | Identificação no disco | Arquivos ISO | SHA-256 da faixa 01 |
| --- | --- | ---: | --- |
| Vigilante 8 | `SLUS_005.10`, volume `VIGILANTE8` | 81 | `f39b7ba3cb3bbdc4c19ba121a9af8af425cfd4e1706fbe6ff0af850ea96bfe49` |
| 2nd Offense Europe | `SLES_021.62`, volume `V8_SECOND_OFFENSE` | 129 | `2c7b80d4e1da44f53c3b8e792be5704bb4916a34c13b586f404d871ac16bfad3` |

O primeiro disco contém `COMMON.EXP`, `SHELL/VEHICLES.EXP`, dez pares `TERRAIN/*.DLL`/`*.EXP`, `QUEST.BIN`, sons e vídeos. Seus 12 arquivos `REDBOOK/*.RAW` apontam para faixas de áudio fora da faixa de dados. O CUE fornecido lista as 13 faixas. O segundo disco contém oito fases do segundo jogo e dez do primeiro (`LEVELS/` e `LEVELS/V8/`), `SHARED/COMMON.EXP`, `SHARED/HOTRODS.EXP`, `QUEST.BIN`, sons e vídeos. As 17 faixas BIN estão presentes; não foi fornecido um CUE para esse disco.

## Extração feita

`scripts/inspect_psx_disc.mjs` valida o sistema ISO9660 e extrai arquivos comuns. Ele deixa de fora `.XA`, `.STR` e áudio CDDA porque esses fluxos precisam de decodificação por setor. As cópias locais ficaram em `Assets/GameContent/Private/DiscV8/` e `DiscV82Europe/`, pastas ignoradas pelo Git. Foram extraídos 46 e 84 arquivos comuns, respectivamente.

`scripts/export_v8_disc_geometry.mjs` decodificou os 16 bancos XOBF de `COMMON.EXP` do primeiro jogo. Os bancos 0–12 são os 13 corpos dos veículos, segundo a tabela nativa preservada em `LegacySources/Vigilante8ClassicRecomp/tools/blender_addons/tests/build_v8_to_v82_guest_roster.py`; o banco 13 é a biblioteca de rodas e os bancos 14–15 são efeitos. As malhas OBJ e o manifesto com hashes e IDs estão em `Assets/GameContent/ExtractedV8/`. Os 13 corpos somam 223 grupos, 5.838 vértices e 9.184 triângulos.

`scripts/export_v8_stage_geometry.mjs` decodificou 20 bancos XOBF de objetos das dez arenas do primeiro jogo em `Assets/GameContent/ExtractedV8Stages/`. O manifesto associa cada banco ao ID oficial. Os dados de terreno `TERR`, a posição dos objetos e a colisão ainda não foram convertidos.

O 2nd Offense já tinha 18 malhas de veículos em `Assets/GameContent/ExtractedV82/` e fontes das oito arenas no projeto legado. A imagem europeia fornecida agora também gerou 20 bancos XOBF em `Assets/GameContent/ExtractedV82Disc/` (18 corpos, biblioteca de transformação e banco compartilhado) e 16 bancos de objetos das oito arenas em `Assets/GameContent/ExtractedV82DiscStages/`. A ordem dos veículos vem de `_VEHICLE` e `GameManager.vehicleConfigs` preservados no acervo. Os totais de vértices/triângulos batem com a importação anterior em 16 dos 18 carros; Xanadu RV e Moon Trekker diferem em oito triângulos cada. Esses arquivos da edição europeia ficam separados para não substituir silenciosamente a fonte anterior.

Para repetir a leitura com os mesmos BINs:

```powershell
node scripts/inspect_psx_disc.mjs 'Vigilante 8/Vigilante 8 (Track 01).bin' --extract 'Assets/GameContent/Private/DiscV8' --json
node scripts/inspect_psx_disc.mjs 'Vigilante 8/Vigilante 8 - Second Offense (Europe)/Vigilante 8 - Second Offense (Europe) (Track 01).bin' --extract 'Assets/GameContent/Private/DiscV82Europe' --json
node scripts/export_v8_disc_geometry.mjs 'Assets/GameContent/Private/DiscV8/COMMON.EXP' 'Assets/GameContent/ExtractedV8' v8
node scripts/export_v8_stage_geometry.mjs 'Assets/GameContent/Private/DiscV8/TERRAIN' 'Assets/GameContent/ExtractedV8Stages'
node scripts/export_v8_disc_geometry.mjs 'Assets/GameContent/Private/DiscV82Europe/SHARED/COMMON.EXP' 'Assets/GameContent/ExtractedV82Disc' v82
node scripts/export_v82_stage_geometry.mjs 'Assets/GameContent/Private/DiscV82Europe/LEVELS' 'Assets/GameContent/ExtractedV82DiscStages'
node --test tests/test_psx_disc_tools.mjs
```

Os exportadores imprimem manifestos JSON; para substituir os manifestos versionados, redirecione cada saída para o `manifest.json` do respectivo diretório após criá-lo.

## O que falta para o jogo

Os OBJ preservam apenas vértices, triângulos e grupos. Ainda precisam de hierarquia de peças, rodas posicionadas, texturas, animação, estados de dano, colisão, armas e prefabs ativos na Unity. Os bancos de arenas precisam de terreno, posicionamento, objetos interativos, colisão e pontos de início. O bônus N64 Super Dreamland 64 não está nesses discos.

O pipeline de missões e a referência RecompOne do segundo jogo foram escritos para a edição americana `SLUS_008.68`. A imagem recebida é europeia `SLES_021.62`; endereços e tabelas internas precisam ser comprovados nessa edição antes de usá-los. O Unity Editor `6000.6.7f1` e o Unity Hub foram instalados localmente, e a licença foi ativada. A primeira compilação apontou uma referência de namespace ausente no código do jogo e quatro arquivos `.meta` ausentes dos pacotes URP internos da instalação do Editor. A referência do jogo foi corrigida no projeto; os metadados URP foram repostos na instalação local e no cache. Os testes Unity precisam ser repetidos após esse reparo. A ausência de Python funcional ainda impede executar os importadores existentes. Os BINs resolvem a falta de parte das fontes; ainda não há release jogável.
