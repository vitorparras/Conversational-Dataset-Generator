# 025 — O bloco é recorte de leitura, não de significado

> Norma vigente. 2026-10-04.

**Material grande pode ser lido em blocos, mas o bloco nunca é tratado como o material inteiro: o
contexto vizinho é alcançável a partir dele, e a anotação registra em que escopo foi feita e se esse
contexto foi consultado.**

## Por que existe

O recorte carrega um risco que não existe em material comum: o sentido pode estar distribuído entre
blocos, e o recorte o oculta. O caso concreto é este. Num bloco, alguém diz que faria algo, com um
significado que, isolado, parece inócuo. Mais adiante, em outro bloco, essa pessoa não repete o que
disse, mas evidencia que vai usar o que foi combinado antes. Lido em separado, nenhum dos dois
blocos sustenta a classificação; lidos juntos, sustentam.

Registrar o escopo é o que mantém duas anotações comparáveis. Sem isso, uma pessoa pode ter visto o
que a outra não viu, e a divergência entre as duas não diz se discordaram do material ou se leram
materiais diferentes. É uma discordância que não existe, contada como se existisse.

## Critérios de aceitação

- Dado um material recortado em blocos, quando um bloco é exibido, então fica explícito que ele é
  parte, com sua posição no conjunto, e nenhuma validação o trata como o material inteiro.
- Dado um bloco aberto, quando quem anota precisa do que vem antes ou depois, então alcança esse
  contexto sem sair do fluxo de anotação e sem perder o que já marcou.
- Dado uma anotação gravada, quando se pergunta em que escopo ela foi feita, então a resposta diz
  sobre qual bloco, ou sobre o conjunto, e se o contexto vizinho foi consultado.
- Dado duas anotações do mesmo material feitas em escopos diferentes, quando a concordância é
  calculada, então a diferença de escopo aparece no resultado em vez de ser absorvida como
  discordância, conforme [013](013-concordancia-comparavel.md).
- Dado que o recorte mude — outro tamanho, outro ponto de corte —, quando ele muda, então as
  anotações já feitas continuam resolvendo para o texto que foi anotado, ou ficam marcadas como
  desatualizadas, conforme [009](009-anotacao-de-trecho-estavel.md).

## O que não exige

Não exige tamanho de bloco nem critério de corte, que são método e interface. Não exige recortar
todo material grande: recortar é conveniência de leitura, e material que caiba inteiro na tela
dispensa o recorte. Não exige que o contexto alcançável seja ilimitado — exige que exista e que o
quanto foi consultado fique registrado.
