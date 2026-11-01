# Vigilante 8 Offence 3 — Windows PC (Unity 6.6)

Projeto Unity para gerar um **jogo nativo Windows x64**. O jogador instala/extrai o pacote do jogo e executa `Vigilante8Offence3.exe`. **O jogo não pede BIN, CUE, disco de PS1, emulador, launcher externo nem o Editor Unity.**

O projeto unifica os dados dos dois títulos: **23 personagens/identidades únicos**, **31 veículos distintos** e **18 arenas PlayStation**. Aparições repetidas mantêm uma identidade e seus veículos diferentes continuam selecionáveis.

## Código implementado

- Menu de seleção gerado dinamicamente a partir de `Assets/Resources/Catalog/roster.json`.
- Direção arcade por Rigidbody, ajustes próprios de cada carro, teclado e gamepad.
- Disparo de projéteis com colisão e dano, saúde, câmera e três oponentes IA.
- Preparação automática da cena inicial no Unity Editor.
- Validador que **impede a release** se algum dos 31 carros ou 18 cenários estiver ausente, sem geometria, colisão, armas ou spawns.
- Build Windows IL2CPP, ZIP, manifesto e SHA-256.

## Conteúdo real ainda necessário

**Os modelos, texturas, sons, músicas e arenas licenciados ainda não estão anexados ao repositório.** O projeto não inclui assets inventados nem substitutos genéricos. A estrutura e o código foram adicionados, mas **não existe EXE compilado ou jogabilidade homologada** nesta fase.

O dono do projeto declarou possuir os direitos/autorização sobre os jogos. O conteúdo precisa estar acessível na pasta do projeto para entrar no pacote: arquivos originais de mídia não são obtidos automaticamente a partir dessa declaração.

## Build

1. Abra no **Unity 6000.6.7f1**, com **Windows Build Support IL2CPP**.
2. Configure um URP Asset (with Universal Renderer) no Project Settings > Graphics.
3. No menu do Editor, execute **Vigilante 8 > Preparar cena PC nativa**.
4. Configure os prefabs reais em `Assets/Resources/Vehicles/<assetId>.prefab` e `Assets/Resources/Arenas/<assetId>.prefab`.
5. Use **Vigilante 8 > Validar conteúdo real e pronto para build**.
6. Use **Vigilante 8 > Build Windows x64 IL2CPP com assets**.

A ação de build gera `Builds/Windows-x64/Vigilante8Offence3.exe`, a pasta Unity `Vigilante8Offence3_Data/`, um manifesto, um ZIP e o arquivo `.sha256`. O EXE depende da pasta `_Data` e bibliotecas do runtime Unity, mas de **nenhum arquivo dos jogos clássicos**.

Validação de catálogo sem o Unity:

```sh
python3 scripts/validate_catalog.py
python3 -m unittest discover -s tests -v
python3 scripts/verify_pc_contract.py
```

Leia também [Pipeline de assets](docs/IMPORT_PIPELINE.md) e [Contrato de distribuição](docs/PC_STANDALONE.md).

**Ainda não homologado:** texturas HD definitivas, física de cada carro, armas especiais, missões, salvamento, multiplayer e APK. O foco é Windows.
