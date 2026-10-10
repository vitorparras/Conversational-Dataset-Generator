# 033 — O trabalho em andamento não se perde

> Norma vigente. 2026-10-07.

**Anotação em andamento sobrevive a sair da tela, perder a conexão e fechar o navegador.**

## Por que existe

Anotar um item longo leva tempo, e quem anota consulta o manual de critérios no meio do caminho,
atende alguém, volta.

Se o trabalho se perder nesse intervalo, a pessoa aprende a decidir rápido para não arriscar. E
pressa é exatamente o viés que o registro da origem do rótulo existe para medir — a tela passaria a
produzir o comportamento que o sistema tenta observar.

Há um segundo efeito, pior porque não aparece: perder trabalho repetidamente faz a pessoa evitar
itens longos, e os itens longos deixam de ser anotados com o mesmo cuidado.

## Critérios de aceitação

**CA-1** — *Dado* uma anotação em andamento, *quando* a pessoa consultar o manual ou qualquer outra
parte da tela, *então* o que ela já marcou permanece ao voltar.

**CA-2** — *Dado* uma anotação em andamento, *quando* a conexão cair, *então* o que já foi marcado
é recuperado quando ela voltar, identificado como rascunho e não como anotação registrada.

**CA-3** — *Dado* um rascunho recuperado, *quando* a pessoa retomar, *então* fica claro que aquilo
não foi submetido — rascunho nunca conta como anotação em contagem alguma.

**CA-4** — *Dado* um rascunho não retomado, *quando* o prazo configurado passar, *então* ele é
descartado, e o descarte não deixa conteúdo para trás.

## O que não exige

Não exige que o rascunho atravesse computadores: ele pertence a quem o começou, naquela sessão.

Não exige preservar rascunho de item que saiu da fila daquela pessoa.
