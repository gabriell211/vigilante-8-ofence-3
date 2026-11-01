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
