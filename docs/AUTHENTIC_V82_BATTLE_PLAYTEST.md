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
- Os carros usam física Unity provisória e projéteis invisíveis para diagnóstico (com estados da MGun e do Rocket/Bastion baseados nos arquivos legados). As demais armas especiais, animações, regras completas de dano, áudios classificados por função, materiais e spawns originais **não** foram homologados.
- A ferramenta recusa carros repetidos, pilotos duplicados, mais de três adversários e o terreno ausente. Spawns são inferidos por raycast em geometria real, não retirados de dados oficiais.
- A homologação real exige executar `.github/workflows/unity-editmode.yml` com licença Unity configurada. O CI normal verifica apenas sintaxe e contratos estruturais; um resultado verde não implica build do jogo.

**Não publicar** a cena, os protótipos ou um pacote como versão final. O gate estrito de `WindowsReleaseBuilder.ValidateContent()` continua exigindo 31 prefabs de veículos e 19 de arenas oficiais completos.
