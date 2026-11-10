# 046 — Roteiro de usabilidade: lista de lotes

> Operacional. 2026-11-09.

Preenche o modelo de [034](034-roteiro-de-usabilidade.md) para a lista de lotes (`/lotes`, issue
608), conferido com a tela da lista de lotes no main (#765). As pessoas de teste são
`operador-18`, com a função de Curadoria do acervo e concessão em um domínio, `operador-19`, com a
mesma função e nenhum domínio concedido, e `operador-20`, com a de Auditoria, criadas só para o
ensaio, com a senha combinada para o ensaio e descartadas no fim. Quem executa não é quem escreveu a
tela. Aprovar ou reprovar lote é da revisão por amostra, não desta tela.

## Antes de executar

| Campo | O que vai |
|---|---|
| Telas | Lotes (`/lotes`), com o filtro no endereço e a paginação |
| Quem usa | Curadoria do acervo, sem treino na tela |
| Ponto de partida | Núcleo no ar; 21 lotes do domínio concedido (aceitos, em curso e rejeitados) e um de outro domínio |
| Sucesso | Acha o lote, entende o estado e o motivo de uma recusa, e filtra sem perder o filtro |
| Limite | 30 s e 2 passos do início até a lista; filtrar por estado, 30 s e 4 passos |

## Tarefas

1. **Achar a lista.** Como `operador-18`, pergunte onde estão os lotes enviados. Tem de achar
   "Lotes" no menu, sem ajuda.
2. **Ler um lote.** Peça domínio, quem enviou, quando e o tamanho do lote mais novo. Os horários vêm
   em UTC, e a legenda diz isso. Não há texto do material nem nome de arquivo, e o lote de outro
   domínio não aparece. Peça também o resumo (SHA-256) inteiro: tem de achar "mostrar inteiro".
3. **Entender uma recusa.** Num lote rejeitado, pergunte por que foi recusado. Tem de ler o motivo
   em palavras, junto do estado, e não um código.
4. **Filtrar por estado.** Abra "Filtrar por estado e período", marque "Rejeitados" e "Filtrar".
   Copie o endereço numa aba nova: o filtro tem de voltar igual, e Voltar tem de desfazê-lo.
5. **Filtrar por período.** Escreva um dia final antes do inicial: a tela tem de dizer, junto do
   campo, que o período ficou de fora. O campo só aceita data; para ler a frase de data inválida,
   troque `de=` no endereço por um texto que não é data.
6. **Nenhum resultado.** Com um filtro sem lotes, pergunte o que a tela diz. Tem de ler "Nenhum
   lote com esse filtro" como "mude o filtro", e achar "Limpar filtro".
7. **Páginas.** Sem filtro, peça o lote mais antigo: são 20 por página, e tem de achar a página 2.
   Com um filtro em uso, a página 2 tem de manter o filtro.
8. **Sem concessão.** Como `operador-19`, digite `/lotes`. Tem de ler que não há concessão e quem
   concede, sem ver erro; "Lotes" não aparece no menu.
9. **Outra função.** Como `operador-20`, digite `/lotes`. A resposta tem de ser a de um endereço
   que não existe.

## Variações

Repita as tarefas 1, 4 e 7 só pelo teclado (a ordem é o atalho para o conteúdo, o nome do sistema,
a contagem de avisos, o menu, o filtro, cada campo e cada botão; o foco nunca fica encoberto pelo
cabeçalho), depois com tema escuro, transparência reduzida e contraste alto do sistema, e com a
janela na largura de um celular (390 px), onde o menu vem recolhido e a tabela empilha cada lote em
linhas, sem rolar de lado. Anote se o domínio de trabalho do cabeçalho muda a lista: não muda, e a
frase de abertura diz isso.

## Registro

| Tarefa | Teclado | Tema | Tempo | Passos | Resultado | O que travou |
|---|---|---|---|---|---|---|
| 1 a 9, uma linha por tarefa e por variação | | | | | | |

Resultado "ajuda" ou "desistência" abre item novo na issue da tela, como em 034.
