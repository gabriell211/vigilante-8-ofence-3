# Conteúdo real dos dois jogos — importação Unity

**Separação de ambientes:** arquivos originais e ferramentas de importação são trabalho de desenvolvimento. A versão distribuída ao jogador é inteiramente nativa: inclui meshes, materiais, sons, scripts e arenas dentro dos dados compilados do Unity. O usuário não fornece nenhum arquivo externo.

## Identidade e deduplicação

A lista canônica está em `Assets/Resources/Catalog/roster.json`. O mesmo personagem só aparece uma vez no menu, mas dois carros distintos pertencentes a ele permanecem selecionáveis. O jogo jamais cria veículos proceduralmente como substitutos dos originais.

## Organização

- `Assets/Resources/Vehicles/<assetId>.prefab`: um por veículo, modelo e materiais reais, Collider, Rigidbody e VehicleDrive com ponto de disparo e prefab de projétil.
- `Assets/Resources/Arenas/<assetId>.prefab`: uma por arena, geometrias reais com Collider e ArenaSpawnPoints com quatro Transform configurados.
- `Assets/Resources/Catalog/roster.json`: relacionamento de personagens, carros e arenas.

A ferramenta do Unity Editor **Vigilante 8 > Importar modelos locais FBX (somente desenvolvimento)** é opcional. Ela prepara prefabs a partir de FBX já autorizados e convertidos; não substitui a necessidade de materiais, armamento, física e spawns reais.

Se tiver os arquivos originais sob sua licença, podem ser convertidos em assets compatíveis com Unity na fase de desenvolvimento. Os arquivos de origem e arquivos de disco não fazem parte do produto e não são necessários pelo usuário.

## Requisitos para uma release válida

- Todos os 31 modelos e 18 arenas presentes, com geometria, colisão e materiais aprovados.
- Cada carro com controle, armas, dano e física testados.
- Cada arena com pelo menos quatro pontos de spawn, limites e colisores.
- UI dinâmica com todos os personagens e veículos corretos.
- Testes de performance, estabilidade, áudio, missões e menus no Windows.
- Build Unity IL2CPP bem-sucedido e smoke test com ZIP instalado em máquina limpa.

O build gate verifica a existência e estrutura mínima dos assets. Ele **não** prova qualidade artística, completude das campanhas ou ausência de bugs. Nunca marcar o projeto como homologado sem executar testes no Unity.


**Cobertura oficial entre versões:** 13 veículos do primeiro jogo, 18 do segundo, 23 identidades sem repetição e 19 fases ao incluir Super Dreamland 64 (bônus exclusivo N64). O elenco é verificado por nomes, personagens e plataformas em `tests/fixtures/official_content.json`, não apenas por contagem. A presença no catálogo não implica prefab jogável ou build homologado.
