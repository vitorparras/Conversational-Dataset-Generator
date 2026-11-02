# 026 — Rotação mensal da época

> Operacional. 2026-11-01.

A [decisão 0049](../decisoes/0049-chave-por-item-e-epoca.md) faz cada linha de conteúdo guardar a
chave dela embrulhada por uma época, um segredo do terceiro arquivo de custódia
([024](024-arquivos-de-custodia-da-chave.md)). Destruir a época antiga é o que torna um item
esvaziado irrecuperável, por isso a rotação é mensal e a ordem abaixo não muda.

## Quem faz

O custodiante da cópia ([002](002-guarda-da-chave.md)) destrói a cópia antiga e registra o ato
(data, época destruída, quem destruiu). O operador faz os demais passos. Sem o registro, a rotação
não terminou.

## A ordem fixa

1. **Época nova.** Acrescente ao arquivo de épocas uma linha `n:base64` com `n` maior que todos os
   outros e 32 bytes aleatórios (`head -c 32 /dev/urandom | base64`). O arquivo passa a ter as duas.
2. **Reembrulhar.** Rode a rotação (`EpochRotation`) com o arquivo de duas épocas: em uma transação,
   cada chave de linha não esvaziada passa para a época nova, sem tocar no conteúdo. Se uma chave
   não abrir, nada muda, e a recusa nomeia a época que falta. O executável ainda não tem comando
   para isso, e até que tenha este passo não se faz.
3. **Novo backup do banco** ([005](005-backup.md)), já com todas as linhas na época nova.
4. **Nova cópia de custódia** do arquivo de épocas, com as duas linhas, no lugar da guarda.
5. **Destruir a antiga.** O custodiante destrói a cópia anterior da época e a dos backups do banco
   anteriores ao passo 3 deixa de abrir. Só então se tira a linha da época antiga do arquivo.

Trocar a ordem perde dado: destruir antes do passo 3 deixa o único backup sem a época que o abre, e
reembrulhar depois de destruir não tem como abrir a chave.

## O que o prazo significa

Um item esvaziado fica irrecuperável quando a época que embrulhava a chave dele é destruída, e isso
acontece na rotação seguinte: **em até 30 dias**, não no ato do esvaziamento. A cópia do banco e o
WAL anteriores à rotação continuam com a chave embrulhada até a destruição.

## Backup anterior a uma rotação

Não restaura mais depois do passo 5: a partida recusa o banco com `época ... ausente` e nomeia a
época. Para restaurar um backup velho antes do passo 5, a cópia antiga da época tem de existir
ainda; depois, o backup só serve para estrutura, nunca para conteúdo ([006](006-restauracao.md)).

## Limites

O que ficou em SSD, em snapshot ou em cópia que o núcleo não enxerga não se apaga por este
procedimento: só a destruição da época inutiliza esses restos.
