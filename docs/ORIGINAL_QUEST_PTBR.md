# Missões originais dos dois Vigilante 8 — português brasileiro

Este projeto **não cria novas missões**. O objetivo de Quest é preservar as
campanhas reais dos jogos, alterando **somente a apresentação textual para PT-BR**.

## Evidência já disponível

O acervo preservado em
`LegacySources/Vigilante8ClassicRecomp/notes/v82_original_quest_integration.md`
documenta uma auditoria de **12 campanhas / 48 missões** do Vigilante 8 e
**15 campanhas / 120 missões** do Vigilante 8: 2nd Offense. Os personagens
não elegíveis para campanha permanecem excluídos segundo os ponteiros
originais (não se deve criar desafios para completar o elenco).

O decodificador original
`LegacySources/Vigilante8ClassicRecomp/tools/recompone-v8-2/audit_quest_sources.py`
extrai o conteúdo de `QUEST.BIN` e das rotas originais. Essas imagens de
origem e o resultado `quest-source-audit.json` **não estão versionados neste
repositório**, portanto ainda não é possível apresentar todos os textos ou
afirmar que as 168 missões estão integradas/jogáveis no Unity.

## Importação fiel dos arquivos originais

1. Em um ambiente com seus arquivos autorizados, executar o decodificador
   legado e obter `quest-source-audit.json`, com os dois hashes SHA-256,
   48 + 120 entradas, atores, briefing e registros de rota nativos.
2. Criar o arquivo de trabalho para tradução:

   ```sh
   python3 scripts/import_original_quest_ptbr.py --audit quest-source-audit.json --template quest-ptbr-template.json
   ```

3. Traduzir **apenas** as propriedades `ptBr` de cada entrada, mantendo
   imutáveis os campos `sha256` e `source`. Conservar nomes próprios e
   preservar o significado dos objetivos originais.
4. Gerar o catálogo que o Unity consegue ler sem BIN/CUE durante o jogo:

   ```sh
   python3 scripts/import_original_quest_ptbr.py --audit quest-source-audit.json --translations quest-ptbr-template.json --v8-quest-bin v8-quest.bin --v82-quest-bin V8_2_LOOSE/QUEST.BIN --output Assets/Resources/Missions/original_quests_ptbr.json
   ```

O importador exige os dois QUEST.BIN originais extraídos para conferir o SHA-256 contra a auditoria (nenhum BIN/CUE é utilizado no jogo final), além de tradução presente de **todos** os textos originais e
recusa arquivos com entradas faltantes, fonte alterada, SHA-256 inválido,
rotas nativas erradas, ator não identificado ou número de missões inventado.
A saída conserva valores originais de ID, ordem de missão, faixa dos
objetos para proteger/destruir, variantes de tela, atores, equipe e hex
do registro de rota. A tradução **não altera regras, spawn ou progressão**.

O menu Unity `MISSÕES ORIGINAIS / QUEST` exibe o acervo quando o JSON
for importado. Caso contrário, informa claramente que os textos oficiais
ainda não foram fornecidos. A consulta é somente leitura e **não dispõe
de um botão falso de iniciar missões**.

## Ainda pendente

- Disponibilização do resultado real do decodificador e tradução/revisão
  individual dos briefings para PT-BR.
- Portar o carregamento do gameplay Quest, objetivos de cenário,
  alvos/atores, critérios de vitória, rotas, progresso e desbloqueios.
- Homologação Unity Editor e Windows com missões completas do V8 e V82.

**O CI cobre somente esquema, traduções, integridade de catálogo e sintaxe.**
Exemplos sintéticos em testes são fixtures isoladas e nunca viram missões do jogo.
