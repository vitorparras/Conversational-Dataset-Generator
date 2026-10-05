# 015 — A exportação não leva identidade

> Norma vigente. 2026-10-04.

**Nenhum artefato exportado contém identificador direto de pessoa, texto anterior à sanitização ou
mapa de reversão, e a exportação é bloqueada quando resíduo é detectado.**

## Por que existe

A exportação é o ponto em que o erro deixa de ser corrigível. Dentro do sistema, resíduo pode ser
achado e reprocessado; distribuído, não há como recolher. Por isso esta é a camada que falha
fechada: ela existe pressupondo que [004](004-sanitizacao-sem-residuo.md) e
[006](006-pseudonimizacao-preserva-contexto.md) podem ter errado.

A auditoria distingue o que afirma do que suspeita. Forma de documento, contato e identificador
direto são detectáveis com certeza suficiente para bloquear; nome próprio não reconhecido não é, e
tratar suspeita como bloqueio treina quem opera a ignorar o aviso.

## Critérios de aceitação

- Dado um artefato pronto para exportar, quando a auditoria encontra forma de documento, contato ou
  identificador direto, então a exportação é bloqueada e o item é nomeado.
- Dado um resíduo partido por separador incomum ou caractere invisível, quando a auditoria roda,
  então ele é detectado igualmente.
- Dado uma suspeita que a auditoria não consegue confirmar, quando o relatório é emitido, então ela
  aparece como advertência e não bloqueia por si.
- Dado uma exportação bloqueada, quando alguém a repete sem corrigir a causa, então ela é bloqueada
  de novo, e liberar exige decisão registrada por quem tem autorização para isso.

A prova é um teste que submete à exportação material semeado com resíduo em variações de grafia e
separador, e falha se algum artefato for produzido, previsto em
testes/privacidade/exportacao-sem-residuo e ainda inexistente.

## O que não exige

Não exige impossibilitar a reidentificação por quem já conhece o caso de origem: o que se exige é
pseudonimização e ausência de identificador direto. Não exige revisão humana item a item.
