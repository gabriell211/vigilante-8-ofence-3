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

## Inventário de posições locais das oito cenas Unity originais

As oito cenas Unity preservadas em `LegacySources/Vigilante2Unity/Assets/Levels/` são fontes autênticas e volumosas, incluindo `Route66/ROUTE66.unity`, mesmo sem o `Route66Geo.asset` final. O novo comando `python3 scripts/audit_legacy_v82_scene_layouts.py --json` percorre os arquivos YAML **originais**, conferindo os oito IDs canônicos do catálogo e os hashes Git SHA-1 completos de cada arquivo. Não depende de instalar o Unity.

A auditoria exporta para cada documento: identificador serializado de `GameObject` e `Transform`, nome original, `m_LocalPosition` (X/Y/Z), `m_Father` e referências de `PrefabInstance`. São **dados locais da serialização**, com hierarquia de parentes preservada: coordenadas de mundo não são presumidas, prefab overrides não resolvidos não são tratados como objetos posicionados, e nenhum `SpawnPoint` fictício é criado.

Cada execução automática do CI do GitHub salva o artefato **`v82-original-local-transform-evidence`** com o arquivo JSON `v82-original-local-transform-evidence.json`, que permite examinar sistematicamente a hierarquia e preparar a adaptação real dos objetos. Ele complementa `LegacyStageManifests/scene-audit.json` (que já confere os oito arquivos e contagens gerais); não o substitui nem altera cenas de origem. O parser é somente leitura, não gera prefabs oficiais, não converte shaders e **não transforma as oito cenas legadas em arenas jogáveis na Unity 6**.

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


## Hierarquia TRS das cenas Unity originais (evidência de mundo, não arena pronta)

`python3 scripts/resolve_v82_scene_hierarchy.py --json` parte das **oito
cenas originais com hashes Git verificados** e resolve posições de mundo
apenas para os `Transform` e `RectTransform` cujas cadeias de
`m_Father`, `m_LocalRotation`, `m_LocalScale` e `m_LocalPosition` estão
explicitamente serializadas. A multiplicação usa matrizes afins 3×4,
preservando a rotação e a escala não uniforme dos ancestrais. Parent
ausente, ciclo ou valores inválidos geram `worldPosition: null` e
`resolutionStatus` específico: **nenhum zero é presumido**.

O CI mantém o JSON local anterior e publica agora também o artefato
`v82-original-world-transform-evidence` com nomes dos GameObjects,
IDs de origem, TRS local, posição de mundo resolvida e status de cada
elemento. Essas posições pertencem à **hierarquia explícita da cena
arquivada**, não comprovam se o objeto corresponde ao banco XOBF,
se é spawn de jogador, nem validam colisão ou prefab overrides. É
necessário cruzar GUIDs de MeshFilter/MonoBehaviour com o banco da fase
antes de instanciar objetos oficiais em uma arena Unity 6; o artefato
não é usado automaticamente no build e não desbloqueia a release Windows.


## Evidência de vínculo entre cena VigMesh e banco XOBF/TMD

A ferramenta de leitura \`python3 scripts/audit_v82_scene_mesh_references.py --json\`
cruza agora as **oito cenas arquivadas** com o \`VigMesh.cs.meta\`, cada
\`XOBF00.prefab.meta\` verdadeiro e os grupos \`_tmd_###\` que foram extraídos
dos bancos de objetos. Nenhuma ligação é criada só por semelhança de nomes:
ela exige o **GUID exato do componente VigMesh**, o **GUID exato do banco
XOBF**, o **tmdID** original e a existência real da malha exportada.

Para cada componente, o relatório conserva \`GameObject\`, \`fileID\`,
\`tmdId\`, \`originalTmdGroup\`, \`referencedXobfGuid\`, transformações
serializadas e um \`referenceStatus\` distinto para a referência correta,
peça ausente, banco divergente, parent não resolvido ou componente incompleto.
Os hashes Git de cena e banco são conferidos novamente antes da análise.
O resultado fica disponível no CI como artefato
\`v82-original-scene-mesh-crosswalk\`.

**Importante:** as transformações da cena Unity 2020 não provam as posições
reais da simulação PS1. \`VigObject.DAT_00\` contém coordenadas fixed-point
próprias, ainda não convertidas e aplicadas. A auditoria **não instancia
props nas arenas**, não afirma que os objetos tenham colisão ou áudio prontos
e não supõe pontos de spawn. Serve como vínculo de origem para a
próxima etapa de reconstrução em Unity 6.


## Coordenadas fixed-point reais de JUNC_DB — evidências, não spawns

A nova auditoria \`python3 scripts/audit_v82_junction_fixed_positions.py --json\`
lê os \`JUNC_DB\` das oito cenas com hashes originais verificados e cruza
\`DAT_18\` com o **fileID exato** de \`VigMesh\`, exigindo o mesmo
\`GameObject\`, GUID do banco \`XOBF00.prefab\` e a peça \`tmdID\` já
extraída. A origem de cada vetor é \`JUNC_DB.DAT_00\`, como três inteiros
assinados de 32 bits, não a posição \`Transform\` gravada na cena.

O método \`JUNC_DB.Update()\` original faz
\`(x / translateFactor, -y / translateFactor, z / translateFactor)\`.
A fonte C# de \`GameManager\` tem escala padrão **10.000**; o
\`GameManager\` explicitamente serializado em \`DEBUG.unity\` usa
**4.096**. Por isso o relatório preserva o vetor bruto e calcula
uma \`debugScalePreviewLocalPosition\` **apenas para exame**, sempre
identificada como conversão com a escala do DEBUG, nunca como
posição mundial certificada para combate. A escala efetiva da
partida ainda precisa de inspeção no fluxo de inicialização.

Cada junction declara \`referenceStatus\` para diferenciar peças
verdadeiras, ausência de \`DAT_18\`, banco diferente, peça não
exportada, dados inválidos e links para outro GameObject.
O CI publica o artefato \`v82-original-junction-fixed-positions\`.
O código original de junction também deforma a malha consultando
o terreno; portanto o \`TMD\` sozinho, mesmo identificado e
posicionado, ainda **não substitui** a cena final.
Nenhum prefab de release, colisão, posição de spawn ou cenário
homologado é criado por esta etapa.


## Revisão visual das posições de JUNC com as malhas reais (Editor, não release)

No Editor Unity 6000.6.7f1, use **Vigilante 8 > Migração >
Inspecionar posições JUNC originais com malhas TMD**, selecione uma das
oito fases oficiais e crie a cena
`Assets/Scenes/OriginalJunctionSourceReview.unity`.

O importador lê o próprio acervo legado, **sem Python ou acesso à
internet**, exigindo SHA Git original da cena, SHA do `XOBF`,
GUIDs dos scripts `JUNC_DB` e `VigMesh`, associação
`DAT_18` pelo fileID e o mesmo GameObject. Só monta peças TMD
**efetivamente extraídas** do banco, com `MeshFilter` e `MeshRenderer`,
sem criar objetos 3D substitutos. Para não simular hierarquias incompletas,
somente elementos `JUNC_DB` com Transform raiz são exibidos.

Os eixos seguem exatamente `JUNC_DB.Update()`, usando a escala do
GameManager serializada em `DEBUG.unity` (validada por GUID). Esta é
uma **prévia diagnóstica das coordenadas locais de origem**; a
malha de junction não recebeu a deformação original de `VigTerrain`,
o material URP é de inspeção e não representa a textura de PlayStation,
e não existe colisão, IA, áudio espacial ou spawn oficial.
O componente `OriginalJunctionSourcePreviewIdentity` marca cada
instância como não homologada. O validador estrito recusa sua inclusão
em prefabs de veículos ou arenas finais, e a cena nunca entra na
Build Settings da release Windows.

Os testes Python/Roslyn verificam contratos e sintaxe; verificação
visual, compilação Unity e EditMode continuam exigindo Editor real.


## Combate de inspeção com estruturas reais JUNC/TMD (opcional)

No Unity Editor, a janela **Vigilante 8 > Migração > Dirigir em terreno real
V82 (teste, não release)** agora oferece **Mostrar estruturas JUNC/TMD
originais (sem colisão)**. Com a opção marcada, a cena de teste existente
recebe um conjunto de objetos de origem real sobre um dos **sete** terrenos
\`Geo.asset\` preservados (o \`v82_meteor_crater\` ainda não possui o terreno).

A integração reutiliza o validador de hashes/GUIDs e \`DAT_18\`/TMD da
\`OriginalJunctionSourceReviewBuilder\`: tudo é verificado **antes** da cena
de combate ser substituída. As instâncias mantêm
\`OriginalJunctionSourcePreviewIdentity\`, possuem apenas \`MeshFilter\`
e \`MeshRenderer\` da geometria autêntica e **não criam Collider,
Rigidbody, componentes de arma ou personagens**. A opção começa desligada.
A colisão já existente dos testes contra o terreno nativo não muda.

**Limitação importante:** essas instâncias usam a conversão de
\`JUNC_DB.DAT_00\` com a escala preservada em \`DEBUG.unity\`, mas não
receberam a transformação dos vértices por \`JUNC_DB.LoadDB()\` e
\`VigTerrain.FUN_1B750()\`. A sobreposição espacial é **exploratória**:
não é aprovação da posição final de edifícios ou pontes, geometria
dinâmica, física do jogo original ou fase homologada. O Windows release
continua bloqueando qualquer prefab que contenha esses marcadores.


## Conexões de estradas originais: RSEG_DB -> JUNC_DB

A auditoria \`python3 scripts/audit_v82_road_segments.py --json\`
agora lê cada componente original \`RSEG_DB\` das **oito fases**
diretamente das cenas com hash Git verificado. Cada segmento aponta
pelos dois **fileIDs** serializados para objetos \`JUNC_DB\` da mesma cena.
Preserva \`DAT_0C\` (flags), os dois vetores \`DAT_10\`/\`DAT_14\`
(int32 little-endian) e os seis coeficientes inteiros de curva
\`DAT_20\` a \`DAT_34\`. Não cria conexões quando falta alguma origem.

Para revisão, os pontos 0%, 25%, 50%, 75% e 100% seguem a
expressão **X/Z de \`RSEG_DB.FUN_285E4\`** em aritmética fixed-point.
O relatório mantém todos os valores brutos e oferece apenas uma
conversão de inspeção na escala de \`DEBUG.unity\`. Quando a rotina
original depende de \`VigTerrain.FUN_1B750()\`, a altura **Y é nula**
em vez de gerar uma estrada numa altura inventada. Valores sujeitos
a overflow de int32 também são rejeitados em vez de induzir
falsa certificação.

O CI arquiva \`v82-original-road-segment-graph\`, incluindo
referências ausentes, status por fase e as amostras verificáveis.
**Ainda não são malhas rodoviárias, estradas dirigíveis, física ou
spawns homologados.** Essa evidência é a base para reconstruir os
segmentos/curvas reais antes de gerar geometria de cenário no Unity 6.


## Planta 2D das estradas RSEG no Unity Editor (não é cenário jogável)

**Vigilante 8 > Migração > Visualizar estradas RSEG originais em planta XZ**
abre a planta interativa das oito fases, incluindo o local sem
\`Geo.asset\` (Meteor Crater). Escolha a fase e pressione **Carregar curvas
RSEG originais verificadas**. O gráfico permite zoom e clique em cada
traçado para exibir seu \`fileID\` autêntico.

O leitor C# reutiliza o verificador de **SHA Git da cena e do banco
XOBF**, GUIDs exatos de \`JUNC_DB\` e \`RSEG_DB\`, confere os
\`fileIDs\` das duas extremidades e calcula cinco amostras X/Z com
\`RSEG_DB.FUN_285E4\`. Referências ausentes, entradas malformadas e
cálculos que podem transbordar int32 são rejeitados, nunca substituídos.
Trechos que dependem da altura real \`VigTerrain.FUN_1B750\` aparecem
em cor distinta. **A representação é uma planta de origem X/Z**, não uma
posição 3D validada: nenhuma coordenada Y é deduzida ou desenhada.

Esta visualização não cria GameObjects, Mesh, colisão, prefabs,
estradas artificiais ou conteúdo de release. É uma ferramenta de
análise para posteriormente importar a geometria real das vias,
as alturas do terreno e a colisão com fidelidade.


## Linhas 3D de origem RSEG com altura explícita (inspeção dentro do combate)

No Unity Editor, a janela de combate sobre terreno original contém a opção
**Ver curvas RSEG com Y original conhecido (somente visual)**, inicialmente
desmarcada. A nova função lê diretamente a cena legada autenticada por SHA
e os vínculos RSEG/JUNC; não depende de arquivos exportados manualmente.
A geometria visual é um traçado fino \`LineRenderer\`, **não uma malha de
estrada original**. A largura é somente da linha de inspeção.

Para segmentos cuja \`RSEG_DB.FUN_285E4\` indica flags 8, o Y vem da
primeira JUNC; para flags 1, o Y é interpolado a partir das duas JUNC,
seguindo a expressão int32 fixed-point original. Somente esses segmentos
geram cinco amostras XYZ verificáveis. Quando a altura requer
\`VigTerrain.FUN_1B750()\`, quando há overflow ou vínculo inválido,
**não aparece traçado 3D**; o registro X/Z continua disponível na planta
2D do Editor. A escala e os eixos dos traçados são de \`DEBUG.unity\`,
portanto **não certificam a posição em partidas reais**.

Essas linhas não introduzem Collider, Rigidbody, spawn, objetos substitutos
ou comportamento físico. Cada uma é identificada por
\`OriginalRoadSourcePreviewIdentity\`, explicitamente proibida dentro de
prefabs oficiais pelo validador da release Windows. Sem execução do
Unity Editor real, a inspeção visual e os EditMode tests permanecem por
homologar; o CI atual valida código, estrutura, auditoria e regressões.


## Alturas RSEG usando VigTerrain.FUN_1B750 preservado (evidência autêntica)

\`scripts/audit_v82_vigterrain_heights.py --json\` agora lê, diretamente
das cenas Unity arquivadas com SHA Git verificado, a componente
\`VigTerrain\` pelo GUID exato e seus arrays originais \`vertices\`
(ushort LE) e \`chunks\` (ushort LE). Verifica dimensão 1024 dos chunks,
quantidade de vértices por \`zoneCount\` e vínculos à fonte.
O método original \`VigTerrain.FUN_1B750(uint x, uint z)\` usa duas
interpolações por triângulo dependendo de \`(x & 65535) + (z & 65535)\`;
o parser reproduz os cálculos fixed-point com limite int32.
Ele **não** substitui a origem por um Raycast de MeshCollider, altura
de malha importada ou terreno inventado.

O relatório cruza cada RSEG/JUNC real com cinco amostras originais.
Quando as flags RSEG já estabelecem Y, preserva esse ramo.
Quando exigem \`VigTerrain\`, usa a altura dos vértices originais
do mapa, **somente se o componente de terreno estiver disponível e
íntegro**. Se a cena não tiver os arrays, falta um chunk ou o cálculo
não for seguro, a altura permanece \`null\` e o relatório sinaliza
a causa. O CI exporta \`v82-original-vigterrain-road-heights\`.

**Ainda não foi incorporado à visualização 3D no Unity**: a etapa
atual é um relatório independente, verificável e testado, que fornece
a referência de altura para portar a mesma rotina em C#.
Não comprova terreno deformado em execução, largura/malhas de estrada,
colisão ou posições de spawn do jogo final.


## Integração Unity Editor: altura original VigTerrain para RSEG

\`OriginalVigTerrainSourceGrid\` lê os arrays \`chunks\` e \`vertices\` da
componente \`VigTerrain\` de cenas originais com hash Git verificado,
GUID original e \`zoneCount\` consistente. O cálculo C# reimplementa
\`FUN_1B750\` sem Raycast nem geometria inventada. A prévia das curvas
RSEG do teste de combate agora também aceita alturas amostradas do
terreno original quando as flags da via exigirem \`VigTerrain\`.

As amostras de X/Z agora ficam também em \`Vector2Int\` para preservar
os inteiros fixos exatos antes de amostrar a altura (os \`Vector2\`
de visualização poderiam arredondar coordenadas acima de 2^24).
O teste de regressão exige que o ponto real de Steel Mill com origem
\`(59919356,90376320)\` retorne **Y=3006532**, idêntico ao valor
arquivado em JUNC. As duas metades da interpolação têm testes
independentes, além da verificação de dados incompletos.

Os traçados continuam \`LineRenderer\` de inspeção, não malhas de
estrada, colisão original, spawns oficiais nem arena pronta para
release. Homologação visual e EditMode dependem do Editor Unity.


## Traçado de inspeção RSEG com 33 amostras originais

A linha 3D exibida na cena de combate de teste agora contém **33 pontos** (de
`t=0` a `t=4096`, com passo de 128) por segmento, calculados novamente
pela equação inteira original de `RSEG_DB.FUN_285E4`. Os cinco pontos
originais da planta 2D continuam preservados para compatibilidade e
comparação; os pontos intermediários **não** são interpolações artificiais
dessas cinco posições.

Quando `RSEG_DB` exige `VigTerrain.FUN_1B750`, o Editor consulta a
malha original serializada em cada um dos 33 pontos. Trechos com erro de
referência, overflow na curva ou qualquer falha de consulta da altura
não recebem um traçado 3D parcial ou uma altura aproximada. As linhas
continuam sem colisão e sem malha de estrada, não são posições de
release, e exigem validação visual pelo Unity Editor.
