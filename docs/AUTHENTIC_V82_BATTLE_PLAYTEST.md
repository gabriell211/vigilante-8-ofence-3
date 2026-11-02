# Combate de desenvolvimento com malhas originais (V82)

Abra o projeto no **Unity 6000.6.7f1**. No menu **Vigilante 8 > Migração > Dirigir em terreno real V82 (teste, não release)**, escolha um dos sete terrenos com Geo.asset preservado, o veículo do jogador, o primeiro adversário e **1–3 adversários**. Os demais oponentes são preenchidos deterministicamente com carros oficiais de pilotos distintos. Clique em **Criar combate real de teste (não release)** e ative Play no Editor.

O gerador chama `OriginalTerrainSourceImporter.ImportAll()` e `LegacyVehiclePlaytestBuilder.PrepareAll()`, exige malhas TMD/Geo e audiobancos originais e monta a cena `Assets/Scenes/OriginalNativeTerrainDriveTest.unity`. Esta cena não entra nas Build Settings e não é usada no executável Windows.

## Placar dinâmico de desenvolvimento

A cena criada agora possui `OriginalBattleSession`, um monitor **somente do Editor** que mostra o nome da arena de origem, o modelo e piloto originais de cada participante, a vida real de cada `VehicleDrive` e o total de IA ainda em combate. Ao perder toda a vida, a sessão indica derrota; quando todos os adversários ficam sem vida, indica vitória. Pressione **P** para pausar/despausar no Play Mode; o relógio de simulação é restaurado quando o monitor é desativado.

O HUD e suas condições de resultado **não substituem** as regras originais de pontuação, missões ou campanha. Não existem personagens ou carros fictícios: as etiquetas vêm do catálogo oficial e os HP vêm das instâncias vivas do teste. O build de produção continua proibindo `OriginalBattleSession` dentro dos prefabs das arenas.

## Metralhadora: primeiro estado de arma derivado de código original

Os **18 prefabs de veículos V82 para teste no Editor** agora têm `OriginalMachineGunSourceTest`. O estado de disparo é portado de `LegacySources/Vigilante2Unity/Assets/Scripts/MGun.cs` e confere a lógica do `UpdateW(1, 4, 12)`:

- A carga inicial (`maxHalfHealth` no código legado) é **1.280 (0x500)**; a primeira tentativa pressionada dispara.
- Nas tentativas seguintes, o contador `tags` governa a cadência (decremento até -1). Cada tiro atribui `tags = heat >> 8` e aumenta a carga em **32**; ao guardar a arma (`UpdateW(4)`) o código subtrai **64** sem baixar de 1.280.
- O `Bullet` legado recebe **7** pontos de dano normal (**14** com o poder de dano duplo, ainda não conectado ao protótipo) e vida útil de **45 ciclos originais**. No teste, o dano normal é 7 e a vida é representada provisoriamente como `45 * Time.fixedDeltaTime`.

Este é o **primeiro comportamento de arma baseado nos estados do código original**, não o sistema completo de armas do PlayStation. Os disparos ainda usam o projétil de física invisível do teste, a velocidade/ponto do cano não estão calibrados, a duração dos ciclos do PlayStation pode diferir do timestep Unity, e o som/efeito original e as armas especiais não foram integrados. A rotina existe somente no Editor, permanece fora dos prefabs oficiais e não altera as 31 entradas do catálogo.

## Foguete direcionado Rocket → Bastion (código de origem)

Os **18 carros V82 de desenvolvimento** agora incluem `OriginalBastionRocketLauncher`. No teste de combate sobre o terreno Geo.asset original, o jogador pode pressionar **E** (teclado) ou **LB** (controle) para disparar um foguete diagnóstico em direção ao adversário original vivo mais próximo. A seleção é atualizada por `OriginalBattleTargetCoordinator`, de modo que o alvo possa ser outro carro real de um dos 18 disponíveis.

O comportamento é **derivado diretamente** de `LegacySources/Vigilante2Unity/Assets/Scripts/Rocket.cs`, em especial o estado inicial `Rocket` e a transição explícita `UpdateW(2)` para `Bastion`. O voo mantém a distinção original entre perseguição mais suave enquanto `physics2.M2 >= 211` e mais forte abaixo de 211, além de revalidar o alvo a cada **16 ciclos**. O modelo C# `OriginalBastionRocketFlight` aplica essas fases aos vetores de velocidade de um Rigidbody do Unity.

**Não é uma reprodução homologada de uma arma original:** o projétil permanece o collider invisível de diagnóstico já existente; o desenho, dano efetivo, explosões, som, inventário, quantidade de munição, conversão fixed-point e duração dos ciclos não foram portados. Os coeficientes e unidades de velocidade são adaptações explícitas; nenhuma munição fictícia é atribuída. No teste, o botão E/LB dispara um protótipo por pressionamento, e a metralhadora continua no Espaço. Os bots mantêm as armas de teste anteriores; o disparo do Bastion ainda é controlado pelo jogador, não pela IA.

Os testes NUnit EditMode verificam estados, fases e os 18 prefabs verdadeiros; testes PlayMode verificam orientação do Rigidbody e cancelamento de alvo destruído. O CI Python/Roslyn não substitui executar esses testes dentro da Unity. O gate do executável continua recusando o launcher e o controlador experimental.

## Míssil original Shell → Projectile (integração de teste)

O código original `LegacySources/Vigilante2Unity/Assets/Scripts/Missile.cs` registra os estados `Shell`, `Projectile` e `Halo`. O lançamento da configuração normal em `MissileL.cs` usa **60 pontos de dano**, e a conversão `UpdateW(arg1=5)` do projétil recebe **480 ciclos**. A perseguição usa ajuste do erro de trajetória com divisores **16 horizontalmente e 8 verticalmente**, junto com limites de incremento sobre as velocidades inteiras. O novo `OriginalMissileSourceFlight` conserva os dois primeiros estados e essas relações em um Rigidbody de desenvolvimento.

- Em uma cena criada com **18 modelos TMD de carros V82** e terreno Geo.asset preservado, o jogador pressiona **Q** no teclado ou **D-pad para cima** no controle para disparar contra o carro vivo mais próximo.
- O míssil passa pelo estado `Shell` e é ativado como `Projectile`; sua trajetória é corrigida até atingir um collider, perder o alvo ou esgotar o tempo de fonte.
- O componente `OriginalMissileSourceLauncher` é acoplado apenas aos prefabs de teste de carros originais. Não cria nomes fictícios, munição, modelos comerciais ou texturas artificiais.
- **Limitações:** a velocidade, o alvo espacial, o limite horizontal convertido em escala Unity e os efeitos de colisão ainda não replicam a aritmética fixed-point original. O modo `Halo`, os gráficos, áudio, recarga e munições não foram homologados. `480 * Time.fixedDeltaTime` é uma conversão de tempo provisória e não a taxa original do PlayStation. Os bots somente escolhem alvo; ainda não disparam esse míssil por IA.

O gate de release proíbe `OriginalMissileSourceLauncher` e `OriginalMissileSourceFlight` nas entidades comerciais. Os testes PlayMode de perseguição e expiração precisam de execução real na Unity licenciada, além do CI Python/Roslyn.

## Minas MineDr → Mine (física de desenvolvimento fiel ao comportamento documentado)

A versão preservada de `LegacySources/Vigilante2Unity/Assets/Scripts/MineDr.cs` inicializa a arma com **6 cargas** (`maxHalfHealth=6`) e consome uma por lançamento (`UpdateW(12)`). A classe `Mine.cs` mantém uma lista mundial com até **16 minas** e reage ao impacto com um veículo chamando `FUN_2B370(GameManager.DAT_A5C, screen)`. A constante original `DAT_A5C` é o vetor **(0, -0x20000, 0)** no sistema legado, que se traduz em impulso vertical ascendente na Unity.

No teste Editor, pressione **R** (teclado) ou **D-pad para baixo** (controle) para lançar uma mina atrás do carro original. Ela cai pelo Rigidbody, arma ao tocar o solo, reage ao impacto com outro carro e é consumida. A origem permite comprovar o **impulso**, mas **não um número de dano de vida**: não chamamos `TakeDamage` nem atribuímos eliminações falsas. O componente `OriginalMineSourceTrap` limita o registro a 16 instâncias ativas e `OriginalMineSourceLauncher` respeita as seis cargas de cada carro original.

**Limites:** as minas usam colliders físicos invisíveis para homologação de colisão, nunca um objeto 3D genérico apresentado como original; ponto de queda, massa, impulso Unity, aderência ao terreno e duração são adaptações de teste. As minas especiais `Bearhug`, `Hovermine` e `CactusPatch`, efeitos visuais, dano sobre rodas e partículas permanecem pendentes. Nada é distribuído como arma final: o build Windows recusa os dois componentes.

## Minas especiais originais de MineDr: Bearhug, Hovermine e CactusPatch

O sistema de **seis cargas** de `MineDr.cs` agora permite testar as três variantes registradas no script original, além da mina básica. Elas usam o **mesmo inventário** por carro original, sem cargas ou armas fictícias:

| Tecla | Variante da fonte | Consumo | Comportamento rastreado |
|---|---|---:|---|
| **R** | `Mine` (`UpdateW(12)`) | 1 | Queda, contato e impulso vertical |
| **T** | `Brearhug`/Bearhug (`0x132`) | 2 | Atração magnética persistente ao tocar veículo |
| **G** | `Hovermine` (`0x133`) | 2 | Busca de veículo a cada **31** ciclos, deslocamento e impulso no contato |
| **V** | `CactusPatch` (`0x134`) | mín(6, cargas disponíveis), exigindo pelo menos 2 | Depois de armar, colisão espalha a quantidade de minas-filhas igual ao número de cargas consumidas |

No repositório original, o arquivo se chama `Bearhug.cs`, mas a classe preservada foi escrita `Brearhug` — esse nome está documentado para garantir rastreabilidade. A regra de `CactusPatch.cs` exige `tags != 0` antes da dispersão e o número de fragmentos vem de `maxHalfHealth`. A `Hovermine.cs` procura alvos a cada 31 ciclos e contém a condição `DAT_19 > 10` após dez buscas.

Os cálculos espaciais do PlayStation usam inteiros e `VigTerrain`; a busca de 35 m, as velocidades, o impulso e a distribuição física dos fragmentos desta implementação são **adaptações provisórias do teste**, não valores originais homologados. `Brearhug` não recebeu dano de vida inventado, `Hovermine` não recebeu modelos visuais genéricos apresentados como oficiais e os fragmentos `Mine` do `CactusPatch` também são colliders invisíveis. O registro global de até 16 minas de teste continua válido. Nenhuma destas variantes entra no build Windows final: o gate recusa `OriginalSpecialMineSourceBehavior` nos prefabs oficiais.

## Munição e dano da RocketL diretamente da fonte

O script arquivado `LegacySources/Vigilante2Unity/Assets/Scripts/RocketL.cs` prova os valores necessários para eliminar os disparos ilimitados dos protótipos:

- `UpdateW(1)` inicializa `maxHalfHealth = 10`: **10 cargas por veículo**.
- O tiro padrão `UpdateW(12)` reduz **uma carga**. A opção `UpdateW(10, 0x422)`, usada para criar um foguete com `flags |= 0x41000000`, exige pelo menos 2 e reduz **duas cargas**. Este modo está associado ao teste de aderência RoadRunner.
- `FUN_4336C` cria o foguete com **80 pontos de dano-base** (160 com dano duplo, ainda não integrado) e **240 ciclos de vida** durante o voo normal.

`OriginalRocketSourceInventory` é agora o único inventário para as duas modalidades de foguetes de desenvolvimento nos **18 carros TMD originais**; a UI mostra as cargas restantes e impede gastar valores negativos ou disparar sem recursos. O comportamento Bastion de teste usa a carga unitária do disparo padrão como convenção provisória enquanto o mapeamento completo da opção de salvas (`0x424`) não estiver homologado. O RoadRunner preserva os 240 ciclos para a fase aérea e, **somente quando adere**, inicia um segundo contador original de 360 ciclos até a explosão retardada de -40.

Os valores 10/1/2/80/240/360 são da fonte, não dados inventados. A frequência real dos ciclos, balística, efeitos, escudos e geometria oficial continuam pendentes, e as fontes são expressamente limitadas ao Unity Editor de teste. Nenhuma geração de EXE final é desbloqueada.

## Foguete RoadRunner com aderência e explosão retardada (código original)

`LegacySources/Vigilante2Unity/Assets/Scripts/Rocket.cs` possui o estado `RoadRunner`, ativado quando o foguete da variante especial atinge um veículo (`flags & 0x1000000`). O foguete é preso ao transform do alvo, `physics2.M2` recebe **360 ciclos** e, ao terminar, chama `Vehicle.FUN_3A020(-40, ... , true)`. A rotina `FUN_3A020` usa o sistema real de escudos e subdivisões dos carros antigos, ainda não convertido totalmente para `VehicleDrive`.

A implementação de teste `OriginalRoadRunnerSourceCharge` mantém os estados **Rocket → RoadRunner → Expired**. Um impacto no carro original prende o projétil invisível ao alvo, **não aplica dano imediato**, e após 360 ciclos de `FixedUpdate` aplica uma única vez o equivalente provisório de **40 pontos**. Se o carro atingido morrer antes, a carga é descartada sem produzir uma segunda eliminação. O sistema de `Projectile` preserva o veículo proprietário para contabilizar a autoria real do eventual abate.

Durante a batalha de diagnóstico, pressione **Y** (teclado) ou **X** (botão oeste do controle) para disparar o modo RoadRunner. O estado é somente Editor, nos 18 carros TMD autênticos do V82, e o gate Windows recusa qualquer prefab de produção que contenha `OriginalRoadRunnerSourceLauncher` ou `OriginalRoadRunnerSourceCharge`.

**Limites:** o formato 3D oficial do foguete não foi importado, as regras de escudos, segmentação de carcaça, movimento e tempo original dos ciclos PS1 ainda não estão homologadas. Portanto, o valor de dano é preservado do código fonte, mas sua aplicação direta a `VehicleDrive.Health` é uma aproximação de desenvolvimento, não arma final certificada.

## Míssil Halo (variante oficial MissileL 0x442) — estado de desenvolvimento

O `MissileL.cs` original inicializa `maxHalfHealth = 12`. Sua ramificação `UpdateW(10, 0x442)` exige **mais de uma carga restante**, reduz `maxHalfHealth` em **uma unidade** e gera um projétil com flag especial `0x1000000`. A transição `Missile.UpdateW(5)` o coloca no estado **Halo**, com duração original de **480 ciclos**. O código `Missile.UpdateW(0)` usa rotação de yaw `FUN_24700(0, 0x100, 0)` e colisão com `type == 8` não aciona a destruição comum.

Nos 18 carros originais de teste, **H** (teclado) ou **D-pad direita** (controle) lança o Halo diagnóstico. `OriginalMissileSourceInventory` registra as doze cargas iniciais e só consome uma para `0x442` quando há pelo menos duas, sem permitir valores negativos. O `OriginalMissileSourceFlight` adiciona o estado Halo separado do projétil guiado; a trajetória varia em yaw por `0x100` unidades de ângulo fonte por ciclo (aproximação de **22,5°** em espaço Unity), e o collider ignora outros `Projectile` de teste como correspondência provisória da classe `type 8`. Não cria malha, munição, som ou efeitos falsamente oficiais.

**Detalhe importante de fidelidade:** em `MissileL.UpdateW(12)`, o tiro **comum** não decrementa explicitamente `maxHalfHealth` nessa função. Como seu consumo efetivo ainda não foi identificado em toda a cadeia original, o disparo normal da tecla **Q** não utiliza esse contador de forma inventada. A regra de consumo verificada aqui é somente a do especial `0x442`. A vinculação de `DAT_84`, impulso sobre o dono, trajetória fixed-point original, natureza exata dos objetos `type 8`, FX e ajuste de frequência de ciclos ainda precisam de homologação. O build comercial continua bloqueando o componente experimental.

## Eliminações reais entre carros originais

O projétil do teste físico agora informa ao `VehicleDrive` atingido qual veículo disparou tanto no dano direto quanto na explosão em área. O evento `Eliminated` informa o responsável **exatamente uma vez** quando ocorre um golpe fatal. Dano inválido (`NaN`, infinito, não positivo), morte ambiental sem agressor e ataques vindos de fora da partida não concedem pontos artificiais. A API `TakeDamage(float)` continua disponível para compatibilidade.

O monitor `OriginalBattleSession` acompanha os KOs de cada carro/piloto oficial instanciado, inclui eliminações entre bots e apresenta a última eliminação com nomes do catálogo. O pontuador usa a identidade dos objetos e não nomes de tela, impedindo trocar créditos entre carros. Os nomes e a geometria dos veículos permanecem originais; **a física dos projéteis e os parâmetros de dano continuam provisórios**, não representam armas especiais ou pontuação oficial do PlayStation.

## IA de perseguição sobre o terreno original

A detecção de obstáculos distingue superfícies dirigíveis de paredes por inclinação da normal da malha; usa consultas físicas sem alocação por decisão. IA interrompe os controles quando o veículo-alvo foi destruído e evita disparos com geometria entre os carros. Estas são **correções de comportamento na física Unity de desenvolvimento**, não equivalência confirmada com a IA, navegação ou armas do PlayStation. A verificação completa depende de PlayMode no Unity Editor com as sete malhas importadas.

## Combate livre com seleção de alvos

`OriginalBattleTargetCoordinator` verifica todos os veículos **originais efetivamente instanciados** e atribui a cada adversário controlado pela IA o veículo adversário vivo mais próximo. Portanto, dois bots podem combater entre si, e a destruição de um alvo provoca troca dinâmica para outro participante. Distâncias iguais respeitam a ordem estável do elenco da cena. Carros repetidos e pilotos duplicados continuam proibidos, e o componente é exclusivo do Editor e bloqueado pelo gate Windows.

A heurística de alvo mais próximo **não afirma replicar o algoritmo oficial** do 2nd Offense; ela é uma etapa funcional para testar o combate e a integridade dos veículos com física Unity provisória.

## Limitações de homologação

- **18** veículos V82 têm geometria original disponível no teste. Os **13** veículos do primeiro jogo ainda dependem de mídia e migração final.
- O terreno usa a **malha original visível**, mas um **material neutro explicitamente diagnóstico**, pois as texturas da arena ainda não foram portadas. O terreno não é substituído por geometria genérica.
- Existem **sete** malhas nativas de terreno utilizáveis para inspeção e colisão derivada de malha. A malha de **Meteor Crater / Route 66** continua ausente; nenhuma arena está completa.
- Os carros usam física Unity provisória e projéteis invisíveis para diagnóstico (com estados da MGun, do Rocket/Bastion e do Missile Shell/Projectile baseados nos arquivos legados). As demais armas especiais, animações, regras completas de dano, áudios classificados por função, materiais e spawns originais **não** foram homologados.
- A ferramenta recusa carros repetidos, pilotos duplicados, mais de três adversários e o terreno ausente. Spawns são inferidos por raycast em geometria real, não retirados de dados oficiais.
- A homologação real exige executar `.github/workflows/unity-editmode.yml` com licença Unity configurada. O CI normal verifica apenas sintaxe e contratos estruturais; um resultado verde não implica build do jogo.

**Não publicar** a cena, os protótipos ou um pacote como versão final. O gate estrito de `WindowsReleaseBuilder.ValidateContent()` continua exigindo 31 prefabs de veículos e 19 de arenas oficiais completos.
