# 008 — Incidente: suspeita de vazamento

> Operacional. 2026-10-04.

Este procedimento vale à suspeita, não à confirmação. Esperar certeza para agir é o que transforma
exposição pequena em exposição longa. Dispara aqui: cópia de backup ou dispositivo perdido, acesso
de quem não tem autorização no domínio, cópia de guarda da chave fora da custódia declarada, acesso
a ela sem operação correspondente, exportação publicada com conteúdo que não deveria conter.

## Primeiro, não apague

Preserve registro de auditoria, logs e cadeia como estão. Contenha sem destruir: revogar acesso,
retirar a cópia de circulação e suspender a exportação são atos reversíveis; apagar registro não é,
e elimina justamente a evidência que delimita o escopo.

Anote a hora da descoberta e quem descobriu. É o marco a partir do qual o resto se mede.

## Depois, delimite

O registro de auditoria responde o que foi alcançado: quais domínios, quais unidades, quais
exportações, e — a pergunta que mais muda o resultado — se o mapa de reversão foi decifrado.

| O que saiu | O que isso expõe |
|---|---|
| Conjunto de backup, sem a chave | Texto sanitizado, anotações, métricas e auditoria |
| Cópia de guarda da chave, sem o banco | Nada por si; expõe tudo se o conjunto também sair |
| As duas coisas | O acervo em claro, texto bruto incluído |
| Mapa de reversão decifrado | Identidade real das pessoas no material alcançado |

A primeira linha é o motivo de [005](005-backup.md) separar backup de chave. A última é o motivo de
decifrar o mapa ser operação auditada.

## Então, responda

Se a cópia de guarda pode ter saído, rotacione ([003](003-rotacao-da-chave.md)) e considere exposto
todo material cifrado sob os rótulos que ela carregava: a rotação protege o que vem depois, não o
que já saiu.

Levante o que saiu pelo registro de exportação — o que foi gerado, quando e para quem —, porque é a
via de avisar quem recebeu material afetado. Comunique a quem forneceu o material, e a quem
responde pelo acervo.

A quem mais se comunica, e em que prazo, nenhuma norma vigente deste repositório diz. É pendência
aberta, não espaço de discricionariedade de quem conduz o incidente: a operação entrega escopo
apurado e registra a quem comunicou; quem fixa o dever é a política, quando existir.

## Fechar

O incidente fecha quando o escopo está delimitado, a contenção conferida, e o que mudou no
procedimento está escrito. O registro do incidente é permanente: um acervo que esquece seus
incidentes não consegue afirmar custódia sobre o período em que eles aconteceram.
