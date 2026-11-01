# Original 2nd Offense — integração de física para 18 carros

Este é um caminho de **integração executável no Unity Editor**, separado
das prévias 3D e de uma release Windows validada. Não há carros gerados
proceduralmente como substituição da arte original.

## O que o gerador faz

1. Importa as malhas reais TMD extraídas em OBJ e os materiais vinculados
   aos BMPs originais, produzindo os 18 prefabs de inspeção
2. Associa as amostras WAV originais de cada carro pelos índices reais
3. Gera 18 prefabs de **teste de física**, mantendo malhas, materiais e
   áudio originais; adiciona Rigidbody, BoxCollider e VehicleDrive
4. Cria um ponto de disparo e um projétil invisível de diagnóstico
   (NÃO é a arma especial original), para verificar controle e colisões
5. Opcionalmente, cria uma cena de testes de física com chão **invisível**,
   câmera, iluminação e carro selecionado. É um ambiente técnico, não
   uma arena inventada para o jogo.

Menus disponíveis no Editor:

- `Vigilante 8 > Migração > Preparar todos os 18 carros para teste de física`
- `Vigilante 8 > Migração > Criar cenário técnico de física do carro selecionado`

Para escolher outro carro, selecione um prefab em
`Assets/GameContent/PlaytestVehiclesV82/` antes de gerar a cena. O teste
abre a cena `Assets/Scenes/LegacyVehiclePhysicsTest.unity`.

## O que não está pronto

Os 18 prefabs de teste são salvos em
`Assets/GameContent/PlaytestVehiclesV82/`, **fora de Resources**.
Eles não aparecem como carros homologados no menu de produção nem passam
pelo build gate. A marca `LegacyVehiclePlaytest` é explicitamente
proibida na release.

Ainda faltam as transformações PS1 das peças, física fiel por carro,
armas especiais originais, posicionamentos das rodas, animações de dano,
eventos de áudio, homologação de texturas/UV, e cenários do Unity 6.
O solo de testes é invisível e não substitui nenhuma das fases oficiais.

## Testes reais do Unity

`LegacyPlayableVehicleIntegrationTests` gera os 18 prefabs dentro do
Editor, abre o cenário técnico e verifica IDs, malha, áudio, controle e
separação do build final. Esses testes estão no repositório; **só são
executados quando o workflow Unity com licença é acionado**, e não
pelo CI Python/Roslyn de sintaxe.

A auditoria das oito cenas Unity originais é feita separadamente por
`scripts/audit_legacy_stages.py` e `scripts/verify_legacy_stages.py`.
Esse inventário de arquivos não demonstra que os cenários estejam
jogáveis ou que seus objetos tenham sido migrados para Unity 6.

## Oficina visual integrada

Abra **Vigilante 8 > Migração > Oficina - testar os 18 veículos originais**.
Escolha o carro pelo nome oficial, clique em **Integrar os 18 modelos**
e depois **Abrir teste de direção (Play Mode)**. O Editor monta uma
cena técnica com o veículo selecionado; modelos sem a fonte importada
geram erro explícito, e nenhuma cena de desenvolvimento é incluída na release.
