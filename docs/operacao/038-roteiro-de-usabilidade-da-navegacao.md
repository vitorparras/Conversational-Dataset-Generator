# 038 — Roteiro de usabilidade: navegação e página inicial

> Operacional. 2026-11-06.

Preenche o modelo de [034](034-roteiro-de-usabilidade.md) para o que da navegação já roda no
núcleo: a página inicial de quem não tem nada a fazer e a página de um endereço que não leva a lugar
nenhum, na issue 594. A pessoa de teste é `operador-09`, com a função de Anotação e nenhum domínio
concedido, criada só para o ensaio, com a senha combinada para o ensaio e descartada no fim. Quem
executa não é quem escreveu a tela.

## Antes de executar

| Campo | O que vai |
|---|---|
| Telas | Página inicial (`/`) e página de endereço inexistente, na issue 594 |
| Quem usa | Qualquer pessoa cadastrada do projeto, sem treino |
| Ponto de partida | Núcleo no ar, `operador-09` ativa, navegador com a entrada feita |
| Sucesso | Entende por que a página está vazia; volta ao início de um endereço errado |
| Limite | 20 s e 2 passos para voltar ao início de um endereço errado; acima disso é difícil |

## Tarefas

1. **Página inicial vazia.** Entre como `operador-09`. A página tem de dizer que não há nada a
   fazer, por que, e a quem pedir. Pergunte o que ela faria agora. Não pode haver menu, seletor de
   domínio nem aviso de erro.
2. **Endereço que não existe.** Peça que digite um endereço errado. A página diz que o endereço
   não leva a uma página e oferece um passo para voltar ao início, sem repetir o que foi digitado.
3. **Pular o cabeçalho**, só pelo teclado: do início da página, chegar ao conteúdo em um passo.

## Variações

Repita as tarefas 1 e 3 só pelo teclado, depois com tema escuro, transparência reduzida e
contraste alto do sistema, e com a janela na largura de um celular (390 px), onde a página não
pode obrigar a rolar para o lado.

## Registro

| Tarefa | Teclado | Tema | Tempo | Passos | Resultado | O que travou |
|---|---|---|---|---|---|---|
| 1 a 3, uma linha por tarefa e por variação | | | | | | |

Resultado "ajuda" ou "desistência" abre item novo na issue da tela, como em 034.

## Fora do roteiro

Ainda não há tela de função no núcleo, só a página inicial e a saída, e por isso o menu não aparece
para ninguém. Ficam para a issue da primeira tela de função, hoje a #599 (tela de pessoas), que
traz o primeiro item de menu: ver o que há para fazer pelo menu, saber qual item marca a tela em que
se está, a resposta igual de uma tela de outra função e de um endereço que não existe, e a faixa do
menu a 390 px. A troca de domínio fica para a issue 112, quando existir a primeira concessão: até
lá a fonte de domínios está vazia e o seletor não aparece. A prova de que a tela negada e a
inexistente são iguais byte a byte é do teste de processo `ScreenGateTests`, não de quem executa.
