# Identidade visual — Vigilante 8 Offence 3

**Nome oficial do novo projeto:** Vigilante 8 Offence 3.

O novo menu foi desenhado em código nativo Unity UI Toolkit. A referência
estrutural é o *Vigilante 8: 2nd Offense*: título forte, destaque do piloto
e do veículo, navegação por setas/botões e elementos visuais de combate
arcade. **Não há cópia estática de screenshots ou texturas da interface
original.** O desenho e os componentes do terceiro jogo são próprios.

## Direção de arte

- Fundo grafite industrial, cartões metálicos e faixa de risco dourada
- Identificação própria **OFFENCE 3**, com detalhes laranja e azul elétrico
- Botões grandes com estados hover e foco visíveis, inclusive por gamepad
- Leitura responsiva com rolagem em telas pequenas e proporção widescreen
- Informações do piloto/veículo/arena e estado dos arquivos disponíveis
- Navegador com filtros do primeiro jogo, 2nd Offense e ambas as versões
- Garagem com passagem direta do catálogo para a seleção de combate

## Navegação

**Menu inicial:** Um Jogador / Arcade → garagem; catálogo de carros e
arenas; opções; controles; sobre; sair.

**Seleção:** cada carro, piloto ou arena tem botão próprio na categoria
correspondente. Seleções são validadas contra as relações oficiais e as
preferências do PC. O próximo combate só é liberado se os prefabs oficiais
e adversários estiverem realmente disponíveis.

**Menus e modos clássicos:** Quest, Sobrevivência e Multijogador aparecem
como contexto histórico e pendências, não como modos liberados por botões
sem implementação. A sequência de telas de pausa e resultados permanece
interativa e controla `Time.timeScale` corretamente.

## Qualidade e limitações

O código visual é testado com contratos Python e testes editmode C#.
O CI Roslyn confirma sintaxe, mas **não demonstra que a interface tenha
sido renderizada ou testada no Editor**. É necessário validar navegação,
contraste, resolução, áudio, consumo e entrada de controle com Unity 6.6.
A release PC ainda está bloqueada enquanto os recursos oficiais de
veículos, armas e arenas não forem integralmente homologados.
