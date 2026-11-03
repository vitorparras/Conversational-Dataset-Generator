# 003 — Rotação da chave

> Operacional. 2026-10-04.

Rotacionar é passar a cifrar sob rótulo novo sem perder a capacidade de decifrar o que foi cifrado
sob os anteriores. As duas metades importam: trocar a chave e descartar o rótulo antigo é apagar o
acervo com outro nome — e é por isso que a
[decisão 0013](../decisoes/0013-chave-de-cifra-por-dominio.md) não admite descarte de rótulo.

## Quando

- Em prazo fixo, declarado pela instalação, para que a rotação seja rotina exercitada e não operação
  de emergência. Nenhuma decisão fixa esse prazo: a escolha é desta operação
- A cada troca de quem detém a cópia de guarda, porque quem sai continua sabendo o que sabia
- À suspeita de exposição da cópia — aí a rotação é parte da resposta a incidente, não manutenção
- Antes de migrar o acervo para outra máquina

## Como

Cada dado cifrado carrega, em claro, o rótulo com que foi cifrado. Rotacionar acrescenta rótulo
novo, que passa a valer para o que vier; os anteriores seguem deriváveis da mesma chave mestra.

A derivação leva o domínio ao lado da finalidade, então a rotação pode ser de um domínio só, sem
tocar nos demais. Rotação de domínio não é rotação de acervo, e dizer qual delas foi feita importa:
a primeira não protege o material dos outros domínios.

Recifrar o acervo sob o rótulo novo é opcional e, quando feito, é operação registrada: lê sob o
rótulo antigo e grava sob o novo, item por item, deixando rastro de quantos itens mudaram.

## Os comandos

Dois comandos de execução única, como o `create-domain`, com a credencial do dono do banco. Antes,
acrescente ao arquivo de rótulos os dois da geração nova (`texto-bruto/<id>/v<n>` e
`mapa-reversao/<id>/v<n>`): o comando confere, não cria. A cadeia registra quem, quando, qual
domínio e qual rótulo (`DomainLabelRotated`, `DomainLabelRevoked`).

- `rotate-domain-label --domain <id>` passa o domínio à geração seguinte. O já cifrado segue legível
  pelo rótulo que carrega e o gravado depois usa o novo. Não recifra nada
- `revoke-domain-label --label <rótulo>` impede que o rótulo sele algo novo; ele segue registrado,
  na cópia de guarda e decifrando o que selou. Revogar a única geração em uso é recusado

Saída 0: feito. 17: rótulo novo ausente do arquivo (nomeado, nada gravado). 24: recusa sem gravar.
25: banco sem resposta. A revogação é lida ao subir; um processo no ar só a respeita depois de
reiniciado.

## Rótulo não se descarta

Nem depois de recifrar tudo. O conjunto só cresce, e todos os rótulos seguem na cópia de guarda de
[002](002-guarda-da-chave.md), com o mesmo cuidado do que está em uso.

A tentação é real: rótulo que ninguém usa parece sobra. Mas a contagem que afirma que ninguém o usa
é da aplicação, e basta um registro esquecido — num lote antigo, num item recusado — para que
descartá-lo signifique dado ilegível com a chave em mãos. O custo de guardar é uma linha a mais num
arquivo cifrado; o de errar não tem volta.

## O que a rotação não resolve

Não torna confidencial o que já saiu. Se a cópia de guarda vazou, o material cifrado sob os rótulos
que ela carregava está exposto desde então, e rotacionar protege só o que vier depois. O escopo do
que vazou se apura pelo registro de auditoria, não pela data da rotação.
