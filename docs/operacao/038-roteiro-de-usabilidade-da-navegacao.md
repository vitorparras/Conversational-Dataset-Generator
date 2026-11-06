# 038 — Roteiro de usabilidade: navegação e página inicial

> Operacional. 2026-11-06.

Preenche o modelo de [034](034-roteiro-de-usabilidade.md) para o esqueleto das telas: o cabeçalho
com o menu, a página inicial e a página de um endereço que não leva a lugar nenhum, na issue 594.
As pessoas de teste são `operador-08`, com a função de Administração técnica, e `operador-09`, com
a função de Anotação e nenhum domínio concedido, criadas só para o ensaio, com a senha combinada
para o ensaio e descartadas no fim. Quem executa não é quem escreveu a tela.

## Antes de executar

| Campo | O que vai |
|---|---|
| Telas | Página inicial (`/`), menu do cabeçalho e página de endereço inexistente, na issue 594 |
| Quem usa | Qualquer pessoa cadastrada do projeto, sem treino |
| Ponto de partida | Núcleo no ar, as duas pessoas ativas, navegador com a entrada feita |
| Sucesso | Chega à tela que queria pelo menu; entende por que a página está vazia, quando está |
| Limite | 20 s e 2 passos da página inicial até uma tela do menu; acima disso é difícil |

## Tarefas

1. **Ver o que há para fazer.** Entre como `operador-08`. Pergunte o que a pessoa acha que pode
   fazer ali e peça que abra a tela de Auditoria. Anote se ela usa o menu ou a lista da página.
2. **Saber onde está.** Já dentro da Auditoria, peça que diga qual item do menu mostra a tela em
   que ela está. Ela não pode precisar ler o endereço do navegador.
3. **Página inicial vazia.** Entre como `operador-09`. A página tem de dizer que não há nada a
   fazer, por que, e a quem pedir. Pergunte o que ela faria agora. Não pode haver menu, seletor de
   domínio nem aviso de erro.
4. **Endereço que não existe.** Peça que digite um endereço errado. A página diz que o endereço
   não leva a uma página e oferece um passo para voltar ao início, sem repetir o que foi digitado.
5. **Tela de outra função.** Como `operador-09`, peça que digite o endereço da Administração. A
   resposta tem de ser a mesma da tarefa 4, palavra por palavra: a pessoa não pode deduzir que a
   tela existe.
6. **Pular o menu**, só pelo teclado: do início da página, chegar ao conteúdo em um passo.

## Variações

Repita as tarefas 1, 3 e 6 só pelo teclado, depois com tema escuro, transparência reduzida e
contraste alto do sistema, e com a janela na largura de um celular (390 px), onde o menu fica numa
faixa abaixo da marca e do Sair e não pode obrigar a rolar para o lado.

## Registro

| Tarefa | Teclado | Tema | Tempo | Passos | Resultado | O que travou |
|---|---|---|---|---|---|---|
| 1 a 6, uma linha por tarefa e por variação | | | | | | |

Resultado "ajuda" ou "desistência" abre item novo na issue da tela, como em 034.

## Fora do roteiro

A troca de domínio fica para a issue 112, quando existir a primeira concessão: até lá a fonte de
domínios está vazia e o seletor não aparece. A prova de que a tela negada e a inexistente são
iguais byte a byte é do teste de processo `ScreenGateTests`, não de quem executa.
