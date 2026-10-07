# 0030 — Qual texto fica em claro

> Decisão vigente. 2026-10-07. Substitui a decisão 0003 quanto à tabela de tratamento.

## Contexto

O fluxo produz três versões do mesmo material, e as três coexistem porque é isso que torna cada
etapa auditável sem reprocessar: o **bruto**, como chegou; o **sanitizado**, de onde saiu o artefato
de formato e o que a política manda remover; e o **pseudonimizado**, onde o que apontava para
pessoas virou marcador estável.

A decisão 0003 tratou de dois textos, não três. Sua tabela manda deixar em claro "o texto
sanitizado", e justifica dizendo que ele "já passou pela pseudonimização" — o que não é verdade do
sanitizado, e sim do pseudonimizado.

Lida ao pé da letra, ela deixa em claro justamente a versão que ainda carrega os nomes.

## Decisão

| Versão | Tratamento |
|---|---|
| Bruto, como chegou | Cifrado |
| Sanitizado, antes da pseudonimização | **Cifrado** |
| Pseudonimizado | Em claro no banco |
| Mapa de reversão | Cifrado, com chave e finalidade próprias |
| Anotações, métricas, registro de auditoria | Em claro no banco |

**O pseudonimizado é o texto em claro** — é o que as pessoas leem durante a anotação, e cifrá-lo
inviabilizaria busca e ordenação, que é onde o trabalho humano acontece.

**O sanitizado fica cifrado, junto com o bruto, e alcançá-lo exige a mesma autorização.** Ele ainda
contém o que aponta para pessoas; tratá-lo como material pseudonimizado seria tratar como protegido
algo que não está.

O resto da decisão 0003 permanece: o critério de que copiar o arquivo do banco para fora não pode
expor nada, a cifra feita pela aplicação antes de gravar, e a recusa da cifra de coluna no banco.

## Recusados

**Não persistir o sanitizado.** Guardaria menos dado sensível, e tornaria impossível auditar o que
cada etapa fez sem reprocessar o bruto — e para item esvaziado por pedido de remoção, impossível
para sempre, porque não sobra de onde partir.

**Deixar o sanitizado em claro.** É o que o texto anterior dizia ao pé da letra, e significa nome de
pessoa legível para quem obtiver o arquivo do banco. Contraria o próprio critério da decisão 0003.

## Consequências

Há mais um conteúdo sob a chave, e mais uma coisa que se perde se a chave se perder. A custódia da
chave não muda, mas o que ela protege cresce.

Auditar o que a sanitização removeu e o que a pseudonimização trocou exige autorização de acesso a
material cifrado — não é operação de rotina, e nem deveria ser.

O espaço ocupado cresce: três versões do mesmo material, duas delas cifradas.

## O que reabre

Se a auditoria entre etapas se mostrar desnecessária na prática, não persistir o sanitizado volta à
mesa — com a perda declarada de não poder reconstituir o intermediário de item esvaziado.
