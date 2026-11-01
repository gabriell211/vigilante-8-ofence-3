# Importação dos dois jogos — sem assets genéricos

O catálogo registra nomes, relações e IDs **sem copiar mídia protegida**.

## Contrato dos arquivos externos

Diretório local (fora do Git):

```text
ConteudoLocal/
  Vehicles/v8_rattler.fbx
  Vehicles/v82_thunderbolt.fbx
  ... [todos os 31 nomes definidos pelo JSON]
  Arenas/v8_canyonlands.fbx
  Arenas/v82_meteor_crater.fbx
  ... [todas as 18 arenas]
  Textures/ ... imagens PNG/TGA/JPG/EXR da sua fonte autorizada
```

O importador detecta **todos** os IDs exigidos e rejeita ausência antes de escrever. Arquivos privados são copiados somente para diretórios ignorados pelo Git. Os modelos precisam chegar **já convertidos para FBX**, sem engenharia reversa feita nesta etapa.

## Homologação que ainda precisa acontecer

1. Importação dos arquivos reais de cópias legítimas e identificação de todos os materiais/UV.
2. Revisão individual da topologia, pivôs, rodas, armas, normais, rigs, animações e colisores.
3. Verificação das arenas com colisão, destruição, física e lógica de missão.
4. Compatibilidade de hotrod, transformação, habilidades especiais, sons, músicas e missões.
5. Sistemas de combate, menus, salvamentos, IA, multiplayer, otimização de renderização e testes Windows.
6. Revisão legal dos assets, do código-fonte usado e do eventual pacote distribuível.

Nenhum modelo comercial, áudio, cinema ou textura é baixado automaticamente. A recompilação .NET de terceiros **não** se converte diretamente em projeto Unity. CI aqui valida a integridade do catálogo, **não** certifica que o jogo completo funciona.
