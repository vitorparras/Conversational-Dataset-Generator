# 0005 — As unidades de análise

> Decisão vigente. 2026-10-04.

## Decisão

O sistema reconhece quatro formas de material, e cada uma é uma unidade de análise:

| Forma | O que é |
|---|---|
| Conversa | Sequência de mensagens com autoria e ordem temporal |
| Comentário | Manifestação isolada, com autoria, sem fio de conversa |
| Documento | Texto corrido, sem autoria por trecho |
| Sequência rotulada | Texto onde a anotação recai sobre trechos, não sobre o todo |

Acrescentar uma quinta forma exige decisão que substitua esta. **Não é configuração.**

## Por quê

A forma do material determina como ele é lido na ingestão, o que é validado, como aparece na tela de
anotação e o que significa anotá-lo. Uma conversa tem autoria por mensagem e ordem que importa; um
documento não tem nem uma coisa nem outra.

Tratar isso como parâmetro daria flexibilidade aparente e nenhuma garantia: cada forma nova
exigiria código em quatro lugares de qualquer jeito, e a configuração só esconderia que a mudança é
estrutural.

As quatro cobrem o material conversacional que o projeto se propõe a tratar. A quarta existe porque
anotar trechos é diferente de anotar o conjunto, e misturar as duas coisas na mesma forma tornaria
ambas imprecisas.

## Recusados

| Opção | Por quê |
|---|---|
| Forma livre, definida por configuração | Sem forma conhecida não há o que validar, e validar é o que distingue acervo de pasta de arquivos |
| Uma forma só, genérica | Perde a autoria por mensagem, que é o que torna uma conversa analisável |
| Formas definidas por domínio | Cada domínio reinventaria a mesma coisa, e datasets deixariam de ser comparáveis |

## Consequências

A ingestão de material que não se encaixa nas quatro formas precisa adaptá-lo a uma delas, com perda
declarada, ou motivar uma decisão nova.

Cada forma precisa de validação própria: conversa sem autoria ou sem ordem não é conversa, e aceitar
isso em silêncio produz dataset que parece válido e não é.

## O que reabre

Se aparecer material cuja estrutura não couber em nenhuma das quatro sem perda relevante — fio de
discussão em árvore e transcrição com falas sobrepostas são os candidatos mais prováveis.
