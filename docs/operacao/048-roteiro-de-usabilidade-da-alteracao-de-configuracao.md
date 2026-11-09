# 048 — Roteiro de usabilidade: alteração de configuração

> Operacional. 2026-11-09.

Preenche o modelo de [034](034-roteiro-de-usabilidade.md) para a alteração de configuração pela
tela (`/configuracao/alteracoes`, issue 634, parte 2c), conferido com a decisão 0053 e com a tela
do commit 999e7193, que ainda não estão no main. As pessoas de teste são `operador-23` e
`operador-24`, com a função de Administração técnica, e `operador-25`, com a de Auditoria,
criadas só para o ensaio, com a senha combinada para o ensaio e descartadas no fim. Quem executa
não é quem escreveu a tela. O aplicador, que grava o arquivo e reinicia o núcleo, não tem tela.

## Antes de executar

| Campo | O que vai |
|---|---|
| Telas | Alterar configuração: a proposta, a recusa no campo, a lista de pendentes e a decisão |
| Quem usa | Administração técnica, duas pessoas, sem treino na tela |
| Ponto de partida | Núcleo no ar, a configuração em vigor à vista em outra aba; uma proposta antiga, já vencida |
| Sucesso | Propõe, entende que ainda não vale, e outra pessoa decide; ninguém decide a própria |
| Limite | 30 s e 2 passos do início até a tela; propor, 90 s e 5 passos; decidir, 30 s e 2 passos |

## Tarefas

1. **Achar a tela.** Como `operador-23`, peça que altere o tempo de inatividade da sessão. Tem de
   achar "Alterar configuração" no menu e ler que cada chave traz o valor em vigor.
2. **Propor.** Chave "Tempo de inatividade da sessão", valor 00:20:00, motivo "Ajuste
   operacional", "Enviar para aprovação". Pergunte se já vale: a tela diz que aguarda outra pessoa
   e, até lá, vale o valor em vigor. Confira na configuração em vigor que nada mudou.
3. **Valor recusado.** Proponha o valor "vinte". A recusa vem junto do campo "Valor novo", diz que a
   partida a recusaria e por quê, com a chave e o valor; os campos voltam preenchidos e a lista de
   pendentes não ganha linha. Sem escolher o motivo, o erro vem no campo do motivo.
4. **Não decidir a própria.** Na lista, a proposta de `operador-23` mostra "Proposta sua: outra
   pessoa decide", sem Aprovar nem Recusar. Pergunte por que não há botão e quem decide.
5. **Aprovar.** Como `operador-24`, a lista diz "1 alteração aguardando decisão". Peça a chave, o
   valor, o motivo, quem propôs e quando (UTC). Aprovar: a tela diz que o aplicador grava e
   reinicia o sistema, e que até lá vale o valor em vigor. Pergunte o que vale agora.
6. **Recusar.** Com uma segunda proposta, `operador-24` recusa. Tem de ler "Nada muda na
   configuração", e a lista vazia ("Nenhuma alteração aguardando decisão") como "nada a fazer".
7. **Decisão repetida.** Abra a mesma proposta em duas abas e decida nas duas. A segunda tem de
   dizer que outra pessoa já decidiu, sem erro de servidor.
8. **Prazo vencido.** Com a proposta antiga, tente decidir. Tem de ler que o prazo venceu e
   achar o caminho: propor de novo. A lista não a mostra mais.
9. **Outra função.** Como `operador-25`, digite o endereço da tela. A resposta tem de ser a de um
   endereço que não existe, e o menu não traz a entrada.

## Variações

Repita as tarefas 2 e 5 só pelo teclado (a ordem é o atalho para o conteúdo, o nome do sistema, a
contagem de avisos, o menu, Chave, Valor novo, Motivo, Enviar e, em cada proposta, Aprovar e
Recusar; o foco nunca fica encoberto pelo cabeçalho), depois com tema escuro, transparência
reduzida e contraste alto do sistema, e com a janela de um celular (390 px), onde o menu vem
recolhido. Anote se a espera "Conferindo a alteração…" aparece e se o segundo envio fica bloqueado.

## Registro

| Tarefa | Teclado | Tema | Tempo | Passos | Resultado | O que travou |
|---|---|---|---|---|---|---|
| 1 a 9, uma linha por tarefa e por variação | | | | | | |

Resultado "ajuda" ou "desistência" abre item novo na issue da tela, como em 034.
