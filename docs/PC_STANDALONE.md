# Contrato de distribuição Windows nativo

O produto será um **jogo PC autônomo em Unity**, NÃO uma interface para rodar imagens de disco, emulador, ou um port .NET.

## Pacote final

```text
Vigilante8Offence3-<versao>-win-x64.zip
  Vigilante8Offence3.exe
  Vigilante8Offence3_Data/
  UnityPlayer.dll e demais dependências geradas pelo Unity
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
- 18 arenas com colisão e 4 spawns;
- cena Bootstrap.unity e PanelSettings salvos;
- URP configurado;
- BuildPipeline IL2CPP informa sucesso e o EXE/Data existem;
- ZIP é gerado e SHA256 é escrito.

O GitHub Actions atualmente roda testes do catálogo e contrato estático; isso
**não substitui** compilar no Unity e abrir o EXE em Windows de teste.

Status atual: **não existe release jogável**. O build é bloqueado
intencionalmente até que assets autorizados estejam integrados e os testes do
motor sejam realizados.
