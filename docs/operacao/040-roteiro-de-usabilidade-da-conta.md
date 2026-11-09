# 040 — Roteiro de usabilidade: minha conta, troca da senha e sessões

> Operacional. 2026-11-08.

Preenche o modelo de [034](034-roteiro-de-usabilidade.md) para a tela Minha conta, a troca da
própria senha e o encerramento de sessões, nas issues 597 e 598. A pessoa de teste é
`operador-10`, criada só para o ensaio, com a senha combinada para o ensaio e descartada no fim;
a segunda sessão é aberta pelo mesmo navegador numa janela anônima, com a mesma pessoa. Quem
executa não é quem escreveu a tela.

## Antes de executar

| Campo | O que vai |
|---|---|
| Telas | Minha conta (`/minha-conta`), troca da senha (`/minha-conta/senha`) e confirmação de encerrar sessão (`/minha-conta/sessoes/encerrar` e `/encerrar-outras`), nas issues 597 e 598 |
| Quem usa | Qualquer pessoa cadastrada do projeto, sem treino |
| Ponto de partida | Núcleo no ar, `operador-10` ativa com duas sessões abertas (a do teste e uma anônima), navegador na página inicial |
| Sucesso | Acha a conta pelo cabeçalho; troca a senha na segunda tentativa no máximo; encerra a sessão certa sem encerrar a atual |
| Limite | 20 s e 2 passos para achar a conta e a lista de sessões; 60 s e 4 passos para trocar a senha; 30 s e 3 passos para encerrar uma sessão; acima disso é difícil |

## Tarefas

1. **Achar a conta.** Da página inicial, chegar às sessões abertas. Pergunte qual sessão é a
   dela e como soube. A tela marca a sessão atual com texto ("Esta sessão"), e não só com cor.
2. **Trocar a senha.** Entrar em Trocar senha, informar a senha de hoje e uma nova. Antes de
   enviar, pergunte o que a pessoa espera que aconteça com a outra janela. A tela diz que as
   outras sessões são encerradas e que a atual continua.
3. **Errar a senha atual.** A recusa diz o que fazer, tem uma só frase para a senha errada e
   para a espera, mantém o foco no campo e não repete nenhuma das senhas.
4. **Encerrar uma sessão.** Na lista, escolher "Encerrar" na sessão que não é a atual. A
   confirmação é uma página (sem diálogo) com o botão de encerrar e o de manter. Pergunte o que
   acontece com a sessão encerrada: a outra janela precisa entrar de novo.
5. **Encerrar todas as outras**, e depois **manter**, numa segunda rodada: a pessoa desiste na
   confirmação e vê que nada mudou.
6. **Espera na troca.** Depois de várias tentativas erradas, a tela diz que há espera e não
   deixa deduzir quantas tentativas faltam.

## Variações

Repita as tarefas 1, 2 e 4 só pelo teclado, depois com tema escuro, transparência reduzida e
contraste alto do sistema, e com a janela na largura de um celular (390 px). No celular, cada
sessão aparece em bloco, uma abaixo da outra, com "Início", "Última atividade" e "Ação" escritos
acima de cada valor, e o "Encerrar" fica à vista sem rolar para o lado. Observe se a pessoa
distingue as sessões e acha o "Encerrar" da sessão certa. Se ela precisar de ajuda, o resultado
é "ajuda" e abre item novo na issue.

## Registro

| Tarefa | Teclado | Tema | Tempo | Passos | Resultado | O que travou |
|---|---|---|---|---|---|---|
| 1 a 6, uma linha por tarefa e por variação | | | | | | |

Resultado "ajuda" ou "desistência" abre item novo na issue da tela, como em 034.

## Fora do roteiro

A senha de teste não vai a captura de tela, chat nem registro. A prova de que ela não aparece na
página, no endereço e no log, e de que a sessão encerrada deixa de valer na hora, é dos testes
`PasswordScreenTests`, `SessionEndTests` e dos de processo da conta, não de quem executa.
