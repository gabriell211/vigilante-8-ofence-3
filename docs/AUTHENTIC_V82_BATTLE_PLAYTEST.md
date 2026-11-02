# Combate de desenvolvimento com malhas originais (V82)

Abra o projeto no **Unity 6000.6.7f1**. No menu **Vigilante 8 > Migração > Dirigir em terreno real V82 (teste, não release)**, escolha um dos sete terrenos com Geo.asset preservado, o veículo do jogador, o primeiro adversário e **1–3 adversários**. Os demais oponentes são preenchidos deterministicamente com carros oficiais de pilotos distintos. Clique em **Criar combate real de teste (não release)** e ative Play no Editor.

O gerador chama `OriginalTerrainSourceImporter.ImportAll()` e `LegacyVehiclePlaytestBuilder.PrepareAll()`, exige malhas TMD/Geo e audiobancos originais e monta a cena `Assets/Scenes/OriginalNativeTerrainDriveTest.unity`. Esta cena não entra nas Build Settings e não é usada no executável Windows.

## Placar dinâmico de desenvolvimento

A cena criada agora possui `OriginalBattleSession`, um monitor **somente do Editor** que mostra o nome da arena de origem, o modelo e piloto originais de cada participante, a vida real de cada `VehicleDrive` e o total de IA ainda em combate. Ao perder toda a vida, a sessão indica derrota; quando todos os adversários ficam sem vida, indica vitória. Pressione **P** para pausar/despausar no Play Mode; o relógio de simulação é restaurado quando o monitor é desativado.

O HUD e suas condições de resultado **não substituem** as regras originais de pontuação, missões ou campanha. Não existem personagens ou carros fictícios: as etiquetas vêm do catálogo oficial e os HP vêm das instâncias vivas do teste. O build de produção continua proibindo `OriginalBattleSession` dentro dos prefabs das arenas.

## Limitações de homologação

- **18** veículos V82 têm geometria original disponível no teste. Os **13** veículos do primeiro jogo ainda dependem de mídia e migração final.
- O terreno usa a **malha original visível**, mas um **material neutro explicitamente diagnóstico**, pois as texturas da arena ainda não foram portadas. O terreno não é substituído por geometria genérica.
- Existem **sete** malhas nativas de terreno utilizáveis para inspeção e colisão derivada de malha. A malha de **Meteor Crater / Route 66** continua ausente; nenhuma arena está completa.
- Os carros usam física Unity provisória e projéteis invisíveis para diagnóstico. Armas especiais, animações, regras de dano, áudios classificados por função, materiais e spawns originais **não** foram homologados.
- A ferramenta recusa carros repetidos, pilotos duplicados, mais de três adversários e o terreno ausente. Spawns são inferidos por raycast em geometria real, não retirados de dados oficiais.
- A homologação real exige executar `.github/workflows/unity-editmode.yml` com licença Unity configurada. O CI normal verifica apenas sintaxe e contratos estruturais; um resultado verde não implica build do jogo.

**Não publicar** a cena, os protótipos ou um pacote como versão final. O gate estrito de `WindowsReleaseBuilder.ValidateContent()` continua exigindo 31 prefabs de veículos e 19 de arenas oficiais completos.
