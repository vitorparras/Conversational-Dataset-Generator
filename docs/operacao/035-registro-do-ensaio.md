# 035 — Registro do ensaio de recuperação: as recusas e a porta da ingestão

> Operacional. 2026-11-06.

O ensaio de recuperação ([004](004-perda-da-chave.md)) é registrado na produção com
`record-recovery-test --receipt "<recibo>"`. Este procedimento diz o que o registro recusa e
quando a porta da ingestão de material real se abre.

## O registro recusa, e não grava nada na cadeia

- **Recibo já registrado:** sai com o código **41** e a frase "o recibo já está registrado na
  cadeia". Cada recibo vale um registro só, identificado pelo conteúdo: o resumo SHA-256 do texto
  fica na referência do evento, e o servidor recusa um segundo evento com o mesmo resumo (índice
  único, migração 0033). Dois registros do mesmo recibo ao mesmo tempo não passam os dois.
- **Falta de conferência:** o topo da cópia fora da cadeia, a chave que não é a do arquivo de
  custódia daqui, amostras que não abriram ou um recibo alterado saem com o código **29**.

Para registrar de novo, repita o ensaio com `rehearse-recovery` e registre o recibo novo.

## A porta só aceita ensaio posterior à última perda da chave mestra

A ingestão de material real pergunta se há um ensaio registrado depois da última perda declarada
da chave mestra (`declare-key-loss` com escopo `master`). "Depois" é a ordem da cadeia, nunca o
relógio: uma data atrasada não reabre a porta.

- Com um ensaio só **anterior** à última perda, a porta recusa com "o ensaio de recuperação
  registrado é anterior à última perda declarada da chave mestra" e manda repetir o ensaio.
- Com um ensaio **posterior**, a porta libera.
- Sem nenhuma perda declarada, qualquer ensaio registrado libera.
- A perda de um rótulo não fecha a porta; só a da chave mestra fecha.
