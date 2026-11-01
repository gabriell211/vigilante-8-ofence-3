# Vigilante 8 Offence 3 — Unity PC project

Unificação orientada a dados do conteúdo de **Vigilante 8 (1998)** e **Vigilante 8: 2nd Offense (1999)** para um projeto de Windows em **Unity 6.6 (6000.6.7f1)**, **URP 17.6** e **Input System 1.20.1**.

## Estado honesto da implementação

- ✅ Catálogo **23 identidades**, **31 veículos distintos**, **18 arenas PlayStation**; sem duplicatas no seletor.
- ✅ Relação entre personagem e os veículos de cada jogo, incluindo identidades compartilhadas e nomes alternativos.
- ✅ Validação automática contra duplicatas, aliases conflitantes e referências inválidas.
- ✅ Ferramenta Unity para importar **modelos FBX** de uma fonte local autorizada, gerar prefabs nomeados e impedir build se faltar conteúdo.
- ⛔ Não inclui os modelos, texturas, sons, campanhas ou jogabilidade completa dos jogos comerciais. Não existe build jogável nem homologação Unity até que os dados legítimos sejam importados e testados.

## Abrir no Unity

1. Use **Unity Hub > Add project from disk** neste diretório.
2. Instale o Editor `6000.6.7f1` e o módulo **Windows Build Support (IL2CPP)**.
3. Forneça seus próprios modelos preparados para Unity em um diretório **fora do repositório**:
   - `Vehicles/<assetId>.fbx`
   - `Arenas/<assetId>.fbx`
   - `Textures/` (opcional; ajuste os materiais URP depois da importação)
4. No Unity, execute **Vigilante 8 > Importar conteúdo local (FBX)** e escolha esse diretório.
5. Inspecione meshes, materiais, colisores, pivôs, escala e licenças antes de ativar qualquer build. O validador de build bloqueia conteúdo ausente.

Os nomes `assetId` estão em [roster.json](Assets/Resources/Catalog/roster.json). **Não** comite os arquivos extraídos ou conteúdo protegido de jogos comerciais no GitHub.

## Política de duplicatas

Cada pessoa possui **um único id**. `Slick Clyde` = `Lord Clyde`; `Houston 3` = `Houston`. **Dave** e **Dave's Cultsmen** são entradas diferentes: o segundo representa outro grupo, não uma skin do primeiro. Quem aparece nos dois jogos pode ter **dois veículos**, sempre com nomes e IDs únicos. Evoluções/Hotrod são estados e skins dos veículos, não carros duplicados.

## Mapas

Inclui 10 arenas do primeiro jogo no PlayStation e 8 do segundo. `Super Dreamland 64`, exclusiva do Nintendo 64, fica fora deste primeiro catálogo PlayStation para não prometer dados não incluídos.

## Verificação automática

```bash
python3 scripts/validate_catalog.py
python3 -m unittest discover -s tests -v
```

## Referências e direitos

Fontes de referência: [Vigilante-8-recomp](https://github.com/GTTeancum/Vigilante-8-recomp) (recompilação PC, **não** é um conversor Unity), [releases](https://github.com/GTTeancum/Vigilante-8-recomp/releases/tag/v1.0). O projeto original depende de dados fornecidos pelo dono do jogo.

**Vigilante 8** e **2nd Offense**, personagens, assets e marcas pertencem a seus respectivos titulares. Este repositório não redistribui material comercial de terceiros nem incorpora automaticamente código de licenças desconhecidas.
