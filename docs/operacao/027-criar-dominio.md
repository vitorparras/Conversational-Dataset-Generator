# 027 — Criar domínio

> Operacional. 2026-11-02.

O domínio nasce de um comando só, `./Core.Host create-domain --name <nome>`, rodado pelo operador
com a credencial de dono do banco ([010](010-credencial-de-dono-do-banco.md)), a configuração de
`migrate` ([016](016-migracao-do-esquema.md)) e os três caminhos `MasterKey:` de
[024](024-arquivos-de-custodia-da-chave.md). Não há endpoint HTTP para isso. A
[decisão 0045](../decisoes/0045-formato-do-rotulo-de-derivacao.md) exige os rótulos das duas
finalidades no arquivo de custódia antes do registro no banco, e a
[guarda da chave](002-guarda-da-chave.md) exige que a cópia de guarda os receba no mesmo ato.

## A ordem fixa

1. **Rode o comando.** O id do domínio é o maior id existente mais um. Faltando um rótulo, nada
   é gravado: o comando diz o id que o domínio teria, lista os rótulos que faltam, um por linha
   (`texto-bruto/<id>/v1` e `mapa-reversao/<id>/v1`), e sai com 17.
2. **Acrescente os rótulos** ao arquivo de rótulos e à cópia de guarda, no mesmo ato. Cópia
   atualizada só quanto à chave fica desatualizada em silêncio.
3. **Rode o comando de novo.** Numa transação serializável, ele grava o domínio com esse id, as
   duas linhas de `public.derivation_label` e o evento da criação na cadeia; se qualquer uma das
   três gravações falhar, nenhuma fica. Sai com 0 e diz o id e a sequência do evento.
4. **Reinicie o servidor**, que lê os rótulos só na partida.

## Códigos de saída

- **0**: domínio criado.
- **17**: falta um rótulo no arquivo de custódia.
- **18**: já existe domínio com o nome.
- **19**: arquivo de custódia não lido, nome vazio ou com espaço nas pontas, cadeia que recusou
  o evento, ou outra gravação que chegou antes. Neste último caso, repetir o comando resolve.
- **20**: o banco não respondeu.
- **78**: faltou o nome ou um dos caminhos.

Só o 0 grava algo. Dois comandos ao mesmo tempo: um cria e o outro é recusado, e a cadeia fica
íntegra.

## O que fica registrado

O evento da criação tem o sistema como autor e ato administrativo como motivo, porque a credencial
de dono não é uma pessoa. Quem criou o domínio fica em `domain.created_by` e no registro de
auditoria do servidor ([001](001-ambiente.md)), e o nome do domínio nunca entra na cadeia. A saída
do comando não leva o nome do domínio nem chave alguma.
