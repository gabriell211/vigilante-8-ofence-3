# Unity 6.6 – compilação e testes reais

Além da auditoria Python de fontes, há um workflow separado:

`Actions > Unity 6.6 actual Editor compilation and EditMode tests > Run workflow`

Ele usa `game-ci/unity-test-runner@v4.4.0` (versão que inclui o ajuste
de memória compartilhada necessário para Unity 6.6+) e fixa o Editor em
`6000.6.7f1`. A execução compila os assemblies Unity e executa os
testes EditMode de malhas, áudios, identidades, assets e cena de inspeção.

## Por que não ativa automaticamente?

A execução requer licença Unity configurada nos Secrets do repositório
(`UNITY_LICENSE` para uma licença ativada, ou `UNITY_SERIAL` junto das
credenciais, conforme o tipo de licença). Não existe credencial disponível
automaticamente pela presença do código no GitHub.

O workflow é **somente manual**, portanto uma licença ausente não gera
CI vermelho nas alterações comuns nem é confundida com uma homologação.
A checagem Python continua rodando em push. Resultado aprovado no Python
**não significa** compilação ou execução real do Editor.

A galeria de inspeção dos veículos é gerada por uma função do Editor,
não pelo build de distribuição. Ela ainda precisa ser executada e
inspecionada visualmente, com o procedimento em `UNITY_ASSET_REVIEW.md`.

Uma release Windows de produção continua bloqueada até que todos os
31 prefabs de carros e 19 prefabs de arenas tenham assets finais, armas,
colisões e validações aprovadas.
