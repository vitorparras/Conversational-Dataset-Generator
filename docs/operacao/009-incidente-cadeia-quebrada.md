# 009 — Incidente: cadeia quebrada

> Operacional. 2026-10-04.

A cadeia está quebrada quando um elo não fecha, o topo difere do registro externo, a contagem de
eventos de um item não bate, ou uma garantia de imutabilidade foi encontrada desativada. O que
acontece com o acervo nesse estado é norma, e está na
[verificação da custódia](../privacidade/005-verificacao-da-custodia.md): o domínio afetado para de
ingerir, anotar e exportar, a leitura continua porque investigar exige ler, e **a cadeia não se
refaz para voltar a fechar.** O que esta operação faz é o trabalho dentro disso.

## Congele antes de investigar

Confirme que a escrita no domínio parou de fato, e que está recusando visivelmente em vez de
descartar em silêncio. Preserve a instalação como está e trabalhe sobre cópia — que serve para
investigar, nunca para substituir o original.

Abra o incidente na própria cadeia, que é como a política trata o estado de incidente, e anote a
hora da descoberta e quem descobriu.

## Separe as causas, nesta ordem

1. **Verificador errado.** Confirme contra sequência sintética de resposta conhecida, antes de tudo:
   erro no verificador tem a aparência exata de cadeia alterada
2. **Janela operacional conhecida.** Restauração, migração sob a credencial de
   [010](010-credencial-de-dono-do-banco.md), queda durante gravação. É aqui que o registro das
   janelas paga o que custou — ele explica o dano, e a sua ausência deixa o dano sem explicação
3. **Evento ausente.** A sequência aponta onde: o antecessor existe, e o sucessor aponta para outro
4. **Alteração.** O que resta quando as três primeiras foram descartadas — e só então

Concluir alteração sem descartar as três primeiras é acusar com base em defeito próprio. As
assinaturas que separam acidente de adulteração estão na política; o que a operação entrega a elas é
o registro das janelas, sem o qual não há como aplicá-las.

## Delimite o alcance

A verificação aponta o primeiro ponto em que a cadeia quebra, como o requisito de
[integridade verificável](../requisitos/020-integridade-verificavel.md) exige. Dali para frente o
material é suspeito, inclusive o que foi anotado desde então — não porque esteja errado, mas porque
não se pode mais afirmar que não foi tocado. O que é anterior ao ponto continua verificável.

Levante também o que já saiu: o registro de exportação diz o que foi gerado, quando e para quem, e é
a única via de avisar quem recebeu material cuja procedência deixou de se sustentar.

## Quem conclui, e o que sobra

A conclusão é escrita por quem responde pelo acervo, e entra na cadeia como evento. A operação
entrega observação, não veredito: o que o verificador disse, quais janelas existem, o que o registro
externo atesta.

Exportação que inclua material do trecho suspeito declara isso no artefato, ou não acontece. Quebra
não declarada contamina toda afirmação feita a partir do dataset, incluindo as que estavam certas:
depois, ninguém tem como separar uma coisa da outra.
