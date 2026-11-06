# 036 — Roteiro de usabilidade: entrada e saída

> Operacional. 2026-11-06.

Preenche o modelo de [034](034-roteiro-de-usabilidade.md) para a tela de entrada e para a ação de
sair do cabeçalho. A pessoa de teste é `operador-07`, criada só para o ensaio, com a senha
combinada para ele e descartada no fim. Quem executa não é quem escreveu a tela.

## Antes de executar

| Campo | O que vai |
|---|---|
| Telas | Entrada (`/entrada`) e cabeçalho com Sair, nas issues 592 e 593 |
| Quem usa | Qualquer pessoa cadastrada do projeto, sem treino |
| Ponto de partida | Núcleo no ar, `operador-07` ativa, navegador sem sessão aberta |
| Sucesso | Entra com a primeira tentativa certa; sai e vê a entrada com o foco no nome |
| Limite | 30 s e 4 passos para entrar; 2 passos para sair; acima disso é difícil |

## Tarefas

1. **Entrar** com `operador-07` e a senha certa. Anote o que a pessoa lê antes de digitar.
2. **Errar a senha** e corrigir. A recusa diz o que fazer, sem dizer se o nome existe, e o nome
   digitado continua no campo.
3. **Nome que não existe.** A recusa tem de ser a mesma da tarefa 2, palavra por palavra.
4. **Cadastro vazio.** Com nenhuma pessoa ativa, a tela diz que o cadastro está vazio e aponta o
   passo no servidor. Pergunte à pessoa o que ela faria agora.
5. **Espera.** Depois de várias tentativas erradas, a tela diz que há espera. A pessoa não pode
   deduzir quantas tentativas faltam nem se o nome existe.
6. **Sair** pelo cabeçalho, só pelo teclado. O foco deve cair no primeiro campo da entrada.

## Variações

Repita as tarefas 1, 2 e 6 só pelo teclado, depois com tema escuro, transparência reduzida e
contraste alto do sistema, e com a janela na largura de um celular (390 px).

## Registro

| Tarefa | Teclado | Tema | Tempo | Passos | Resultado | O que travou |
|---|---|---|---|---|---|---|
| 1 a 6, uma linha por tarefa e por variação | | | | | | |

Resultado "ajuda" ou "desistência" abre item novo na issue da tela, como em 034.

## Fora do roteiro

A senha de teste não vai a captura de tela, chat nem registro. A prova de que ela não aparece no
endereço, no log e nas respostas é do teste de navegador, não de quem executa.
