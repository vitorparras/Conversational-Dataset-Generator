# 045 — Roteiro de usabilidade: avisos

> Operacional. 2026-11-08.

Preenche o modelo de [034](034-roteiro-de-usabilidade.md) para a lista de avisos da pessoa
(`/avisos`) e o aviso de escrita suspensa na tela dos domínios (issue 595, parte 1). As pessoas de
teste são `operador-16`, com a função de Administração técnica, e `operador-17`, com a de Auditoria,
criadas só para o ensaio, com a senha combinada para o ensaio e descartadas no fim. Quem executa
não é quem escreveu a tela.

## Antes de executar

| Campo | O que vai |
|---|---|
| Telas | Avisos (`/avisos`), o aviso no cabeçalho e no início, e os Domínios com a escrita suspensa |
| Quem usa | Qualquer pessoa que entrou, sem treino na tela |
| Ponto de partida | Núcleo no ar; um ambiente com a cadeia íntegra e um com a cadeia quebrada por fixture |
| Sucesso | Acha o aviso, entende o que aconteceu e o que continua valendo, e o marca como lido |
| Limite | 30 s e 2 passos do início até o aviso; marcar como lido, 15 s e 1 passo |

## Tarefas

1. **Saber que há aviso.** Como `operador-17`, depois de `operador-16` repor a senha dela com duas
   sessões abertas, entre de novo. Pergunte se há algo novo para ela. Tem de achar a contagem no
   cabeçalho ou o aviso no início, sem ajuda.
2. **Ler o aviso da senha.** Na lista, pergunte quem repôs a senha e quantas sessões foram
   encerradas. A tela diz "por quem administra o sistema" e o número; não diz o nome de quem fez.
3. **Marcar como lido.** Peça que tire o aviso da lista. Tem de achar o botão "Marcar como lido",
   ver a confirmação e a contagem cair. Pergunte se o aviso foi apagado: a tela diz que continua
   registrado.
4. **Lista vazia.** Depois de marcar o último, pergunte o que a tela diz. Tem de ler "Nenhum aviso
   novo" como "nada a fazer", não como erro.
5. **Cadeia quebrada.** No ambiente com a cadeia quebrada, como `operador-16`, abra os domínios.
   Pergunte o que mudou: a escrita está suspensa e a leitura continua. Tem de notar que a tela não
   traz botão nem elo, e que não diz qual evento falhou.
6. **O mesmo aviso sem acesso aos domínios.** Como `operador-17`, no mesmo ambiente, abra a lista de
   avisos. Tem de achar o aviso de escrita suspensa com as mesmas palavras, mesmo sem alcançar os
   domínios.
7. **Aviso que não é seu.** Peça que digite o endereço de marcar como lido de um aviso de outra
   pessoa. A resposta tem de ser a de um endereço que não existe.

## Variações

Repita as tarefas 1 e 3 só pelo teclado (a ordem é o nome do sistema, a contagem de avisos, Sair, a
tabela e cada botão), depois com tema escuro, transparência reduzida e contraste alto do sistema, e
com a janela na largura de um celular (390 px), onde a tabela rola dentro da sua região. Anote se
o botão "Marcar como lido" cabe sem quebrar em duas linhas na largura de 1280 px.

## Registro

| Tarefa | Teclado | Tema | Tempo | Passos | Resultado | O que travou |
|---|---|---|---|---|---|---|
| 1 a 7, uma linha por tarefa e por variação | | | | | | |

Resultado "ajuda" ou "desistência" abre item novo na issue da tela, como em 034.

## Fora do roteiro

O aviso de concessão a vencer, para quem concedeu (CA-1), entra na parte 2, quando a concessão
existir. Nenhum aviso sai do sistema por mensagem: só aparece aqui.
