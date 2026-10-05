# 004 — Cadeia de custódia

> Norma vigente. 2026-10-04.

Três coisas estão sob custódia, e é a terceira que costuma ser esquecida.

O **material de entrada**, como chegou: o conteúdo original, de que fonte veio, em que formato, com
que resumo criptográfico, em que momento foi aceito e por quem.

O **registro de eventos**: tudo que o sistema fez com esse material — ingestão, sanitização,
aprovação de lote, acesso, reversão de pseudônimo, exportação.

As **anotações**, que são o produto. Anotação não é opinião descartável sobre o material: é o dado
que o projeto entrega, e um dataset cuja anotação pode ter sido trocada sem registro não vale mais
que o texto cru.

## Imutável é a sequência, não o estado

Anotação se corrige, divergência se arbitra, taxonomia se revisa. Nada disso cabe num acervo em que
o estado atual é imutável — e é por isso que o imutável é outra coisa: **a sequência de eventos.**

Corrigir uma anotação grava evento novo dizendo o que passou a valer, quem mudou e quando. A
anotação vigente é a projeção dos eventos, nunca a origem deles. Não existe caminho no sistema que
sobrescreva ou apague evento.

## Quem impõe

O banco, não a aplicação — e essa diferença é parte do motivo de a
[decisão 0002](../decisoes/0002-stack.md) ter escolhido o banco que escolheu.

A tabela de eventos é acrescentável por concessão de coluna e gatilho, de modo que atualização e
remoção falham no servidor. Cada evento carrega o resumo criptográfico do anterior, o que torna
alteração no meio da sequência detectável e não apenas proibida. A gravação acontece em transação
serializável, porque duas gravações simultâneas sobre o mesmo elo produziriam cadeia inválida em
silêncio. E o registro de acesso é gravado por extensão do servidor: a aplicação não tem como deixar
de registrar o que ela própria faz.

Garantia equivalente escrita na aplicação cairia na primeira sessão direta no banco e no primeiro
script de migração — e cairia sem deixar marca, que é o pior modo de uma garantia cair.

## Onde a camada do banco falha

Quem é dono da tabela, ou tem papel de superusuário, desabilita gatilho. A política de linha com
`FORCE` da [decisão 0006](../decisoes/0006-dominios-coexistem.md) vale até contra o dono; o gatilho,
não. Então a custódia depende de o papel da aplicação não ser dono nem superusuário, e de a
credencial de dono existir apenas no procedimento de migração, sob guarda separada.

Essa última parte é processo humano, não mecanismo. É o elo mais fraco da cadeia, e precisa estar
escrito em [operação](../operacao/), que é onde se registra quem tem o quê.

## A cadeia prova consistência, não autenticidade do todo

Cadeia válida diz que nada foi alterado dentro daquele banco. Não diz que o banco é o mesmo: uma
base inteira substituída por outra, coerente consigo mesma, passa na verificação sem reclamar.

Por isso o topo da cadeia — o resumo do último evento — é registrado periodicamente **fora** do
banco e fora do alcance de quem o administra. Sem esse registro externo, a
[verificação](005-verificacao-da-custodia.md) responde a pergunta errada. Onde esse registro mora
nenhuma decisão vigente diz, e isso está pendente.
