# 034 — Roteiro de usabilidade de uma tela

> Operacional. 2026-11-06.

Modelo que toda issue de tela (rótulo `interface`) copia para o corpo do PR e preenche com a tela
real. O teste de navegador prova o que uma máquina enxerga; este roteiro cobre o que ela não
enxerga: se uma pessoa, sem ajuda, consegue fazer o que a tela promete. Quem executa não é quem
escreveu a tela, usa a tela sem ler o código e anota o que viu, não o que achou.

## O que preencher antes de executar

| Campo | O que vai |
|---|---|
| Tela e issue | Rota, número da issue e a função de quem a usa |
| Tarefa | Uma frase, no vocabulário de quem usa: "enviar o arquivo", não "chamar a rota" |
| Ponto de partida | Estado do sistema e da sessão em que a tarefa começa |
| Sucesso | O que se vê ou se recebe quando a tarefa termina certa |
| Limite | Tempo e número de passos acima dos quais a tarefa conta como difícil |

## Passos de execução

1. Leia a tarefa em voz alta e não explique mais nada. Se a pessoa pergunta, anote a pergunta.
2. Meça o tempo e conte os cliques e as teclas até o sucesso ou até a desistência.
3. Repita a tarefa só pelo teclado, sem o mouse, e anote onde o foco se perdeu ou ficou escondido.
4. Repita com o tema escuro, com transparência reduzida e com contraste alto do sistema, e anote
   o que ficou ilegível ou sem borda.
5. Provoque o erro mais provável (campo vazio, arquivo recusado) e anote se a mensagem diz o que
   fazer, sem culpar a pessoa.

## Tabela de registro

| Tarefa | Teclado | Tema | Tempo | Passos | Resultado | O que travou |
|---|---|---|---|---|---|---|
| Nome da tarefa | só teclado | claro | 0 s | 0 | sucesso, ajuda ou desistência | Fato observado |

Resultado "ajuda" e "desistência" abrem item novo na issue da tela; o PR só fecha com todos
resolvidos ou com a recusa justificada pelo dono.

## O que o roteiro não substitui

A lista de revisão manual de acessibilidade, que cobre contraste, ordem de leitura e rótulos, e o
teste de navegador, que roda a verificação automática em toda tela. Os três se somam.
