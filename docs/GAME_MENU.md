# Menu do Vigilante 8 Offence 3 — Unity 6.6 (PC)

A interface é implementada em `Assets/Scripts/Gameplay/MatchBootstrap.cs`
com Unity UI Toolkit, sem servidor ou página HTML. A cena Bootstrap salva
um `UIDocument` ligado a um `PanelSettings`; o próprio runtime constrói
as telas usando o catálogo oficial em `Resources/Catalog/roster.json`.

## Fluxo funcional

- **Menu principal** — marca, contagens reais do catálogo e ações de jogo,
  opções, controles, informações de acervo e saída do programa
- **Garagem / iniciar combate** — filtra os 23 personagens, seus 31 carros
  oficiais e as 19 arenas, sem repetir o mesmo piloto no elenco
- **Verificação de partida** — só permite iniciar quando carro e arena
  selecionados possuem prefabs com identidade oficial, malhas, colisores,
  física, armas e 4 spawns; exige três outros carros jogáveis, de pilotos
  distintos. Recursos incompletos são explicitamente identificados
- **Opções** — volume global, qualidade conforme perfis configurados no
  Unity e modo tela cheia do executável Windows; salvos com PlayerPrefs
- **Controles** — comandos efetivamente implementados para teclado e
  gamepad, sem prometer recursos ainda não programados
- **Pausa** — ESC ou botão Start; pausa via `Time.timeScale`, permite
  continuar, alterar opções ou voltar para o menu liberando a partida
- **Resultado** — tela de vitória ou derrota ligada à saúde real dos
  veículos; pode repetir partida, escolher outro veículo ou voltar ao menu

O jogo restaura o último personagem, carro e arena escolhidos somente
quando ainda constam do catálogo oficial. Entradas vazias, malformadas ou
que não existem não podem ser persistidas.

## Garantias e limitações

O menu e a seleção estão implementados em C#, mas a **compilação real
do Editor, inspeção visual, navegação no gamepad, performance e testes
de combate ainda devem ser executados no Unity 6.6**. O CI automático
verifica a sintaxe C# com Roslyn, os contratos estáticos do menu e os
assets de origem. O workflow manual GameCI realiza o teste do Unity
quando houver licença configurada.

**Não há release Windows jogável neste estágio**, porque continuam
faltando prefabs finais dos 31 veículos e 19 arenas, armas especiais
e integração completa das fases oficiais. Não há carros ou arenas
criados artificialmente para contornar esse bloqueio.

## Navegador de conteúdo oficial

O menu **CATÁLOGO OFICIAL** inclui filtros por **Vigilante 8**,
**2nd Offense** e **todas as versões**, com categorias para **carros,
pilotos e arenas**. Cada item leva diretamente à seleção correspondente
da garagem e conserva a identidade oficial; as fases exclusivas do N64
são identificadas. Os recursos ainda não portados aparecem com status
**MIGRAÇÃO PENDENTE**, sem botões de jogar enganosos.

O serviço `OfficialMenuSelections` define a ordenação e a propriedade
de cada carro. Os testes Unity EditMode verificam todos os **23 pilotos,
31 carros e 19 arenas** e asseguram que não seja possível selecionar um
veículo associado ao piloto errado.
