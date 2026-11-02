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
- Informações do piloto vinculado automaticamente ao carro, veículo, arena e estado dos arquivos disponíveis
- Navegador de carros e arenas, com filtros opcionais do primeiro jogo, 2nd Offense ou ambos
- Garagem com passagem direta do catálogo para a seleção de combate

## Navegação

**Menu inicial:** Um Jogador / Arcade → garagem; catálogo de carros e
arenas; opções; controles; sobre; sair.

**Seleção:** há uma única lista de todos os 31 carros, sem etapas de
seleção de piloto ou facção. O personagem é identificado pelo veículo,
e as arenas são escolhidas à parte. As preferências do PC são validadas
contra as relações oficiais. O próximo combate só é liberado se os prefabs oficiais
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

## Garagem 3D oficial

A garagem possui um painel **VEÍCULO OFICIAL / VISUALIZAÇÃO 3D**
que usa `Offence3VehicleShowroom` para instanciar exclusivamente um
prefab oficial carregado de `Resources/Vehicles` e com identidade
verificada. A câmera renderiza para uma textura própria, com rotação
suave do carro e sem interferir na física da partida.

Quando o modelo real não existe ou não corresponde ao ID oficial, a
visualização 3D é ocultada e o menu informa a ausência do arquivo.
**Nunca é exibido um cubo, carro genérico ou modelo inventado.** A
prévia é desligada fora da garagem e a RenderTexture é liberada no
descarregamento da interface.

A função está implementada em C#, com contratos automatizados.
A aparência final depende do shader, materiais e prefabs da cena
Unity 6.6 e precisa de inspeção visual no Editor; não representa
homologação de todos os 31 modelos.
