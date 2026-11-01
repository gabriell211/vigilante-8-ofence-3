# Vigilante 8: Offence 3

**Projeto Windows x64, nativo em Unity 6.6.** Reúne os personagens, veículos e arenas de *Vigilante 8* e *Vigilante 8: 2nd Offense*, com uma identidade por personagem e sem repetir o mesmo carro.

> **Objetivo da distribuição:** instalar e jogar diretamente no PC. Sem emulador, BIN/CUE, seleção de discos ou importação de arquivos pelo jogador. Os recursos definitivos devem ser empacotados no build Unity.

## Situação real da implementação

| Área | Estado verificável |
| --- | --- |
| Catálogo unificado | **23 personagens, 31 veículos, 18 arenas**; IDs únicos e aliases |
| Código de combate | Direção arcade, tiros, dano, câmera, inimigos IA, controles e seleção dinâmica |
| Pipeline Windows | Gerador de cena, validação de prefabs e empacotamento IL2CPP/ZIP/SHA-256 |
| Modelos, texturas e arenas reais | **Integração pendente: assets ainda não presentes nesta main** |
| Áudio, armas especiais e campanhas | Pendentes de implementação e homologação |
| Build executável | **Ainda não compilado/homologado no Unity** |

**Catálogo ≠ asset integrado.** Os nomes e relações de veículos estão cadastrados; isso ainda não coloca suas malhas e texturas dentro do jogo. O script `python3 scripts/audit_content.py` informa exatamente quantos prefabs estão materialmente presentes. Não será publicada uma release com modelos fictícios ou arquivos inexistentes.

## Conteúdo dos dois jogos

- Cada personagem aparece **uma única vez** no seletor; veículos de jogos diferentes continuam disponíveis quando forem realmente diferentes.
- O pipeline foi estruturado para empacotar os **31 veículos e 18 arenas** no executável Windows.
- A referência de conteúdo abrange os dois jogos, sem depender deles no computador do jogador.
- Você informou possuir os direitos/autorização para usar os arquivos. A próxima etapa de integração exige que os **arquivos de mídia reais estejam acessíveis no ambiente de desenvolvimento** (eles não aparecem automaticamente pela autorização).

## Ferramentas e verificação

- Unity **6000.6.7f1**, URP, Input System e IL2CPP.
- `python3 scripts/validate_catalog.py` — integridade de personagens, carros e arenas.
- `python3 -m unittest discover -s tests -v` — regressões estruturais.
- `python3 scripts/audit_content.py` — auditoria honesta da presença dos prefabs.
- `python3 scripts/audit_content.py --strict` — bloqueia a etapa de release se faltar um só prefab.
- `python3 scripts/verify_pc_contract.py` — verifica o contrato do código de PC; não substitui execução no Editor.

No Unity, o comando **Vigilante 8 > Preparar cena PC nativa** cria a cena inicial e **Vigilante 8 > Build Windows x64 IL2CPP com assets** valida os recursos e empacota o Windows. O resultado deverá conter o `.exe`, a pasta `_Data` e as dependências geradas pelo Unity; não é um EXE de arquivo único.

Leia [pipeline de conteúdo](docs/IMPORT_PIPELINE.md) e [distribuição nativa](docs/PC_STANDALONE.md). A documentação descreve o ambiente de desenvolvimento, **não tarefas que serão exigidas do jogador**.
