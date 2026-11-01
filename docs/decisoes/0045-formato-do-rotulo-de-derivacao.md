# 0045 — Formato do rótulo de derivação

> Decisão vigente. 2026-10-31.

## Contexto

A [0013](0013-chave-de-cifra-por-dominio.md) manda derivar a chave de cada domínio da chave mestra,
com o domínio e a finalidade no rótulo, e a [0003](0003-cifra-em-repouso.md) fixa duas finalidades.
Nenhuma diz como o rótulo se escreve, e sem isso a rotação e o arquivo de rótulos da custódia
([operação 003](../operacao/003-rotacao-da-chave.md)) não têm o que comparar. A
[0030](0030-qual-texto-fica-em-claro.md) manda cifrar o texto sanitizado com a mesma autorização
do bruto.

## Decisão

**O rótulo é `<finalidade>/<id do domínio>/v<n>`**: o domínio 7, na primeira geração da finalidade
texto-bruto, tem o rótulo "texto-bruto/7/v1". As finalidades são duas e fechadas: texto-bruto e
mapa-reversao. O texto sanitizado da 0030 usa texto-bruto. O id é o inteiro positivo do domínio,
escrito sem zero à esquerda; n começa em 1 e a rotação o incrementa. Outra finalidade só entra
por decisão nova. **Um rótulo registrado não se remove nem se altera, para nenhum papel, o dono
do banco incluído.**

## Alternativas recusadas

- **Nome do domínio no rótulo**: o nome é texto livre do operador e pode mudar; o id não muda.
- **Finalidade nova para o texto sanitizado**: a 0030 pede a mesma autorização do bruto, e uma
  finalidade a mais seria uma chave a mais para guardar sem que outra autorização a justifique.
- **Remoção permitida ao dono, com registro**: um rótulo removido deixa ilegível o que foi cifrado
  sob ele, e registrar o apagamento não devolve o dado.

## Consequências

Quem lê o rótulo sabe a finalidade, o domínio e a geração da chave sem consultar mais nada, e a
leitura é estrita: só vale o texto que o código escreveria. O banco recusa DELETE, UPDATE e TRUNCATE
em `public.derivation_label` por gatilho, para todos os papéis; o gatilho dispara também numa
sessão em modo de réplica. Quem é dono da tabela ainda pode remover o gatilho, e isso só se vê na
revisão do esquema, não é impedido. Criar domínio passa a exigir os rótulos das duas
finalidades no arquivo de custódia antes do registro no banco.

## O que reabre esta decisão

Uma terceira finalidade de cifra, ou um domínio cujo id deixe de ser inteiro, obriga a nova decisão.
Uma exigência legal de apagar o dado cifrado por apagar a chave reabre a recusa de remoção.
