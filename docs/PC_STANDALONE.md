# Contrato de distribuição Windows nativo

O produto será um **jogo PC autônomo em Unity**, NÃO uma interface para rodar imagens de disco, emulador, ou um port .NET.

## Pacote final

```text
Vigilante8Offence3-<versao>-win-x64.zip
  Vigilante8Offence3.exe
  Vigilante8Offence3_Data/
  UnityPlayer.dll, GameAssembly.dll e demais dependências geradas pelo Unity
  release-manifest.json
Vigilante8Offence3-<versao>-win-x64.zip.sha256
```

O executável deve rodar diretamente no Windows após descompactar/instalar
todos os arquivos juntos. Unity não gera um único EXE autocontido: a pasta
`_Data` e bibliotecas são partes inseparáveis da aplicação. Nenhum arquivo
BIN/CUE ou instalação de Vigilante 8 de console deve ser solicitado ao jogador.

## Editor e ferramentas

- Versão-alvo: Unity 6000.6.7f1; Windows Build Support (IL2CPP).
- URP 17.6, Input System 1.20.1 e Unity Test Framework 1.8.
- Scene generator: `Vigilante 8 > Preparar cena PC nativa`.
- Build: `Vigilante 8 > Build Windows x64 IL2CPP com assets`.
- Batch: `-batchmode -nographics -quit -projectPath . -executeMethod Vigilante8.Editor.WindowsReleaseBuilder.BuildFromCommandLine`.

## Source assets are development inputs, not player setup

O autor informou possuir direitos/autorização sobre os dois títulos. 
Material produzido/extraído sob essa autorização deverá ser preparado como
modelos Unity, materiais, texturas, áudio, animação, arenas e prefabs específicos.
Todos são **compilados** com o jogo. Não há download de mídia externo em runtime.

O importador FBX existente é uma ferramenta auxiliar de **desenvolvimento**,
não um requisito do instalador. A recompilação de terceiros em .NET não é
convertida magicamente em um jogo Unity. O projeto ainda precisa de conteúdo
real e implementação completa de missões, armas, destruição e sons.

## Validação obrigatória

Antes de empacotar:
- 23 identidades sem duplicar personagens;
- 31 veículos únicos com Rigidbody, collider, mesh real, VehicleDrive e armas;
- 19 arenas com colisão e 4 spawns;
- cena Bootstrap.unity e PanelSettings salvos;
- URP configurado;
- BuildPipeline IL2CPP informa sucesso e o EXE/Data existem;
- ZIP é gerado e SHA256 é escrito.

O CI automático valida código, catálogos e integridade de arquivos, **não** executa a Unity.
Agora existem workflows manuais, todos dependentes de licença Unity real:

- `Unity 6.6 real Editor and PlayMode tests` executa testes EditMode e PlayMode de física no Editor.
- `Unity Windows x64 strict build and verification (manual only)` rejeita inicialmente a ausência de qualquer prefab oficial de carro ou arena (`audit_content.py --strict`), exige licença Unity e aprovação dos testes reais, invoca `WindowsReleaseBuilder.BuildFromCommandLine`, valida o ZIP gerado (EXE, `_Data`, UnityPlayer, GameAssembly, manifesto e SHA-256) e só disponibiliza um **artifact privado do Actions** em caso de sucesso. **Não publica GitHub Release automaticamente**.

O verificador independente é `python3 scripts/verify_windows_package.py --builds-dir Builds`.
Testes unitários criam pequenos arquivos de fixture temporários para exercitar esse verificador; esses arquivos não são jogos, não vão para a build e nunca viram pacote distribuído.

A validação do executável deve continuar com execução e testes de gameplay numa máquina Windows limpa antes de qualquer distribuição pública.

Status atual: **não existe release jogável**. O build é bloqueado
intencionalmente até que assets autorizados estejam integrados e os testes do
motor sejam realizados.


**Cobertura oficial entre versões:** 13 veículos do primeiro jogo, 18 do segundo, 23 identidades sem repetição e 19 fases ao incluir Super Dreamland 64 (bônus exclusivo N64). O elenco é verificado por nomes, personagens e plataformas em `tests/fixtures/official_content.json`, não apenas por contagem. A presença no catálogo não implica prefab jogável ou build homologado.
