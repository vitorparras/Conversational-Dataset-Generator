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
   **Reinicie o servidor** (ou mantenha-o parado até o passo 3): ele lê as épocas só na partida e,
   sem reinício, segue embrulhando o que grava sob a época antiga.
2. **Reembrulhar.** Rode `./Core.Host rotate-epoch`, com a configuração de `migrate`
   ([016](016-migracao-do-esquema.md)) e os três caminhos `MasterKey:`, o arquivo de duas épocas
   entre eles: em uma transação, cada chave de linha não esvaziada passa para a época nova, sem
   tocar no conteúdo, e grava na cadeia o evento do giro, sem o qual o banco recusa a troca
   ([0050](../decisoes/0050-banco-amarra-a-mudanca-do-conteudo-a-um-evento.md)). Sai com 0 e diz
   quantas; com 15 se o custodiante não foi lido, uma chave não abriu ou a cadeia recusou o evento
   (nada muda, e a recusa nomeia a causa); com 16 se o banco não respondeu; com 78 se faltou um
   dos caminhos.
3. **Conferir.** Rode `rotate-epoch` de novo, com o servidor já reiniciado: tem de sair com 0 e
   dizer 0 chaves. Se disser mais, algo gravou sob a época antiga no meio: reinicie e repita.
   Sem 0 chaves, não se segue.
4. **Novo backup do banco** ([005](005-backup.md)), já com todas as linhas na época nova.
5. **Nova cópia de custódia** do arquivo de épocas, com as duas linhas, no lugar da guarda.
6. **Destruir a antiga.** O custodiante destrói a cópia anterior da época e a dos backups do banco
   anteriores ao passo 4 deixa de abrir. Só então se tira a linha da época antiga do arquivo.

Trocar a ordem perde dado: destruir antes do passo 4 deixa o único backup sem a época que o abre, e
destruir com chaves ainda na época antiga (passo 3 sem 0 chaves) perde o conteúdo vivo delas.

## O que o prazo significa

Um item esvaziado fica irrecuperável quando a época que embrulhava a chave dele é destruída, e isso
acontece na rotação seguinte: **em até 30 dias**, não no ato do esvaziamento. A cópia do banco e o
WAL anteriores à rotação continuam com a chave embrulhada até a destruição.

## Backup anterior a uma rotação

Não restaura mais depois do passo 6: a partida recusa o banco com `época ... ausente` e nomeia a
época. Para restaurar um backup velho antes do passo 6, a cópia antiga da época tem de existir
ainda; depois, o backup só serve para estrutura, nunca para conteúdo ([006](006-restauracao.md)).

## Limites

O que ficou em SSD, em snapshot ou em cópia que o núcleo não enxerga não se apaga por este
procedimento: só a destruição da época inutiliza esses restos.
