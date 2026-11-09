# 051 — O que o aplicador faz, o que ele recusa e o que fica registrado

> Operacional. 2026-11-09. Decisão [0053](../decisoes/0053-configuracao-alterada-pela-tela.md).
> A conta está em [049](049-aplicador-da-configuracao.md), o serviço em
> [050](050-arquivo-e-servico-do-aplicador.md).

## O que ele faz e o que diz

Para cada alteração aprovada e ainda não em vigor, na ordem da aprovação, ele confere na cadeia
que ela fecha, que quem submeteu e quem aprovou são pessoas distintas, ativas e com a
administração técnica, que a chave está na lista do código e que o carregador aceita o valor.

- Uma aprovação que outra mais nova da mesma chave superou deixa de ser pendente: vale a última.
- Aplicando, grava o arquivo de forma atômica, guarda `appsettings.json.<momento>.previous` e, ao
  fim, reinicia o núcleo uma vez. Na partida, o núcleo registra na cadeia que está em vigor.
- Valor que o arquivo já tem não é gravado e não pede reinício.

O que ele não aplica fica no log do servidor (a saída de erro, que o gerenciador guarda), com o
motivo e as duas sequências, nunca nome nem endereço de pessoa: `ChainBroken`, `SameAuthor`,
`SubmitterNotAllowed`, `ApproverNotAllowed`, `KeyOnlyOnServer`, `ValueRefused` e `FileUnreadable`.

Códigos de saída: 0 (aplicou ou não havia o que aplicar), 37 (recusado, nada gravado) e 38 (o
banco não respondeu ou o núcleo não reiniciou). No 38 por falta de reinício o arquivo já tem o
valor: reinicie o núcleo pelo gerenciador de serviço.

## A recusa que fica registrada

O aplicador **só lê o banco e não grava a recusa**: quem a grava é o núcleo, na partida e a cada 5
minutos, com as mesmas regras (o mesmo código de julgamento). Os motivos se separam assim:

- **Definitivos**, que não mudam sozinhos: `SameAuthor`, `SubmitterNotAllowed`,
  `ApproverNotAllowed`, `KeyOnlyOnServer` e `ValueRefused`. O núcleo grava na cadeia um evento
  de alteração não aplicada, de autor sistema, uma vez por alteração, com o código do motivo e a
  sequência da aprovação, sem nome nem endereço de pessoa. A alteração sai da lista de pendentes,
  e o aplicador deixa de tentar e de dizê-la no log.
- **Passageiros**: `ChainBroken` e `FileUnreadable`, além de um banco que não responde. Nada é
  gravado, a alteração segue pendente e o aplicador tenta de novo na próxima execução.

Quem administra vê a alteração como não aplicada, com o motivo em palavras, na tela da alteração
(a leitura é `ApprovedChanges.RefusedAsync`, que devolve a chave, o código do motivo e a sequência
da aprovação). Uma alteração que já está em vigor não pode ser registrada como não aplicada.
