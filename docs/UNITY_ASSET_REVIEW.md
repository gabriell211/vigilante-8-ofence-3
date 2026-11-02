# Auditoria visual das fontes oficiais no Unity 6.6

Para inspecionar os modelos reais de Vigilante 8: 2nd Offense e seus
materiais, o Editor oferece:

**Vigilante 8 > Migração > Gerar galeria 3D dos 18 carros oficiais**

O comando importa os OBJ extraídos dos TMDs, associa os materiais BMP
identificados, lê automaticamente o catálogo das amostras WAV originais,
cria prefabs de inspeção e monta uma cena com os 18 modelos distintos.

Arquivos gerados no Editor:

- `Assets/GameContent/PreviewMaterialsV82/` — materiais URP das fontes
- `Assets/GameContent/PreviewPrefabsV82/` — prefabs de inspeção
- `Assets/Scenes/OriginalVehicleReview.unity` — galeria de conferência visual

Esses arquivos **não entram no build de produção** porque os prefabs não
possuem `OfficialAssetIdentity`, `VehicleDrive` ou armas homologadas,
e a cena da galeria não deve integrar `EditorBuildSettings.scenes`.

A conversão ainda não recupera todas as transformações originais das peças,
reflexos específicos, atlas/UV em todos os modos PS1, efeitos de danos
ou os comportamentos das armas. Portanto **não é uma prova da aparência
final nem um carro jogável**. Nenhum modelo fictício é criado.
O primeiro Vigilante 8 ainda depende das mídias 3D que não estão nos
repositórios públicos usados como fontes.

Auditoria estrutural no CI: `scripts/verify_extracted_geometry.py`,
`scripts/verify_tmd_previews.py`, `scripts/verify_legacy_textures.py`
e `scripts/verify_legacy_audio.py`. Testes C# de Editor requerem uma
instalação do Unity e não são executados pelo job Python do GitHub.

## Importação geométrica determinística

A importação agora utiliza `OriginalObjMeshBuilder` no Editor Unity para ler
diretamente o OBJ original: cada grupo `g` de TMD vira um `Mesh` próprio,
cada `usemtl` define o submesh, e os vértices/índices `vt` são mantidos
conforme o material real. Esse processo **não depende da heurística do
ModelImporter** para nomear materiais. Os assets de malha gerados ficam em
`Assets/GameContent/PreviewMeshesV82/<id>/`, fora de `Resources`.

O contrato de origem é conferido no CI por
`python3 scripts/verify_unity_mesh_sources.py`: 18 OBJ, triangulação,
grupos, UVs, MTL, BMPs, IDs e integridade da geometria TMD. Testes de
importação e renderização no Unity Editor continuam obrigatórios antes
de tratar qualquer modelo como utilizável na versão Windows.


## Galeria 3D dos oito bancos de objetos originais do 2nd Offense

O comando Unity **Vigilante 8 > Migração > Gerar galeria 3D dos bancos de objetos das 8 fases V82**
converte os arquivos OBJ TMD de `Assets/GameContent/ExtractedStageObjectsV82/` em malhas reais
por peça e prefabs de inspeção (`Assets/GameContent/PreviewStageObjectsV82/`).
A galeria aparece em `Assets/Scenes/OriginalStageObjectReview.unity`.

O importador exige a correspondência exata dos IDs de oito fases, nomes dos
grupos TMD, vértices, faces e contagens do manifesto. Não gera terreno, obstáculos
fictícios, colisões ou spawns. Um material URP neutro é usado **somente para
inspeção de geometria**, pois os shaders/texturas PS1 das fases ainda não estão
integrados. O marcador `OriginalStageObjectPreviewIdentity` impede sua
utilização como arena jogável e como asset de release.

O CI regular valida integridade e sintaxe; os testes Unity EditMode de importação,
geometria e galeria ainda exigem o Editor/licença real.

## Bancos WAV originais nas oito fases V82

A importação `Vigilante 8 > Migração > Importar objetos 3D reais das 8 fases V82` agora **também vincula** os bancos de amostras WAV originais (`OriginalAudioBank`) aos oito prefabs de inspeção `Assets/GameContent/PreviewStageObjectsV82/<id>.prefab`. Antes de escrever, a rotina confere IDs oficiais, índices distintos, paths, GUIDs originais e importação de cada `AudioClip`. Nenhum som genérico é sintetizado, e o banco de cada fase preserva seus índices de origem.

Para conferir uma amostra, abra **Vigilante 8 > Migração > Inspecionar WAVs reais das 8 fases V82**. Se necessário, clique em **Importar objetos e anexar os 8 bancos WAV originais**. Escolha a fase e **Abrir WAV** em uma amostra; o clipe será selecionado no Inspector do Unity, onde o preview nativo permite a audição.

Esta é uma **ligação fiel entre fonte de áudio e banco da fase**, não um mapeamento verificado das funções de som durante o jogo. Motor, armas, explosões, ambientação, posição espacial, disparos automáticos e trilhas originais ainda exigem estudo da execução de origem. Os bancos permanecem fora de `Assets/Resources/Arenas`, não criam fases jogáveis e não liberam a build Windows. O manifesto contempla 26 bancos (18 carros, 8 fases) com 142 referências de índices, entre 235 WAV originais arquivados, incluindo outros sons compartilhados.

## Malhas nativas de terreno original do 2nd Offense

Novo comando Unity: Vigilante 8 > Migração > Gerar galeria 3D de 7 terrenos V82 originais.
Importa sete arquivos Geo.asset originais, preservando SHA Git, bytes e GUIDs. Gera
prefabs em Assets/GameContent/PreviewTerrainV82 e a galeria de inspeção em
Assets/Scenes/OriginalTerrainReview.unity. A etapa exige execução real no Editor.
Não são fases completas: faltam posicionamento de objetos, materiais PS1,
colisões, scripts e pontos de spawn. O terreno de Meteor Crater não foi
identificado e não é substituído por um modelo genérico.

## Playtest de carros reais em terreno original (somente desenvolvimento)

O comando Unity **Vigilante 8 > Migração > Dirigir em terreno real V82
(teste, não release)** permite escolher dinamicamente uma das 7 malhas Geo.asset,
um dos 18 carros originais e um segundo carro para oponente. O Editor importa
as malhas TMD reais e os 7 terrenos nativos, calcula pontos de teste por
raycast nos triângulos originais e gera uma cena com controles, física e IA.

Cena: Assets/Scenes/OriginalNativeTerrainDriveTest.unity, **sempre fora do
build Windows**. As posições inferidas NÃO são os spawns oficiais, a colisão
MeshCollider NÃO reproduz VigTerrain e o projétil é o protótipo de teste,
não uma arma especial original. Não usa piso, carros nem arenas fictícios.
