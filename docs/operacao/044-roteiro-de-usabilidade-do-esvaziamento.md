# 044 — Roteiro de usabilidade: esvaziamento do item por identificador

> Operacional. 2026-11-08.

Preenche o modelo de [034](034-roteiro-de-usabilidade.md) para a tela de esvaziamento, na
issue 617. Quem usa a tela é `operador-16`, com a função Curadoria do acervo e um domínio
concedido; `operador-17`, com a função Auditoria, serve à tarefa 7. As duas são criadas só para o
ensaio e descartadas no fim. Os itens são de ensaio, com conteúdo sintético, e o esvaziamento não
se desfaz. Quem executa não é quem escreveu a tela.

## Antes de executar

| Campo | O que vai |
|---|---|
| Telas | Esvaziar item (`/esvaziamento`) e Confirmar o esvaziamento (`/esvaziamento/confirmar`), na issue 617 |
| Quem usa | Quem atende pedidos de remoção, sem treino na tela |
| Ponto de partida | Núcleo no ar num host de ensaio com o domínio concedido a `operador-16` (em produção a concessão só existe com a #112), três itens de ensaio (um já esvaziado), o número de cada um e um protocolo de ensaio |
| Sucesso | Esvazia o item certo na primeira tentativa, sabendo o que perde; não esvazia o item errado |
| Limite | 20 s e 2 passos para abrir a tela; 60 s e 5 passos para esvaziar um item; acima disso é difícil |

## Tarefas

1. **Achar a tela** pelo menu. Peça que diga, só pela tela, como se acha um item. Pergunte
   o que faria se só soubesse o nome da pessoa que pediu a remoção: a tela não busca por nome.
2. **Esvaziar um item.** Informar o número, ler o que o item perde e o que fica, seguir para a
   confirmação, escolher o motivo, digitar o protocolo e esvaziar. Pergunte antes de enviar o que
   a pessoa acha que acontece e se dá para voltar. A confirmação é uma página, sem diálogo.
3. **Ler o registro.** Depois do esvaziamento, achar quem executou, quando, o motivo e o protocolo.
   Pergunte quem é "Quem pediu (protocolo)": é o número do pedido, nunca o nome.
4. **Erros do formulário.** Enviar sem motivo e depois com um protocolo com espaço. O erro aparece
   junto ao campo, o que foi digitado fica e nada é esvaziado. Nunca digite nome de pessoa como
   protocolo: um nome sem espaço passa na regra, esvazia o item sem volta e vai para a cadeia.
5. **Item que não existe.** Informar um número sem item: a entrada o trata como qualquer outro, com
   "Sem esvaziamento registrado". Seguir, escolher o motivo, digitar o protocolo e enviar: só então
   a tela volta ao campo com o erro e nenhum registro. Anote se a pessoa entende a recusa depois de
   ter preenchido tudo, e se acha que o número pode estar errado.
6. **Item já esvaziado.** Informar o item já esvaziado. A tela avisa e não oferece esvaziar de novo.
7. **Sem acesso.** Entrar como `operador-17` e digitar o endereço da tela. A resposta é a de um
   endereço que não existe, sem sugerir que a tela existe.

## Variações

Repita as tarefas 1, 2 e 4 só pelo teclado, depois com tema escuro, transparência reduzida e
contraste alto do sistema, e com a janela na largura de um celular (390 px), onde nada rola para o
lado. Observe se a pessoa escolhe o motivo com as setas e se o botão de perigo, "Esvaziar o item
N", repete o número que ela pretendia.

## Registro

| Tarefa | Teclado | Tema | Tempo | Passos | Resultado | O que travou |
|---|---|---|---|---|---|---|
| 1 a 7, uma linha por tarefa e por variação | | | | | | |

Resultado "ajuda" ou "desistência" abre item novo na issue da tela, como em 034.

## Fora do roteiro

O protocolo de ensaio não vai a captura de tela, chat nem registro com dado de pessoa real. A
prova de que o registro, a cadeia e a 404 única estão certos é dos testes `EmptyingActTests` e
`EmptyingAccessTests`.
