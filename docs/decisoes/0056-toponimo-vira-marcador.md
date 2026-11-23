# 0056 — O nome de lugar vira marcador

> Decisão vigente. 2026-11-23. Substitui a decisão 0018 quanto ao destino do topônimo.

## Contexto

A [0018](0018-contrato-da-fronteira.md), no contexto (linhas 7 a 10), diz que o nome próprio de
lugar "permanece, porque apagar onde algo aconteceu destrói informação", e a política de destino da
entrada o preserva. A [0012](0012-pseudonimizacao-reversivel.md) (linhas 14 e 15) diz o contrário:
cada pessoa, lugar, organização ou identificador reconhecido recebe uma marca estável e reversível.
A [política 002](../privacidade/002-o-que-se-preserva.md) seguia a 0018 e mantinha bairro e cidade.

A divergência importa na fase 5: o [requisito 006](../requisitos/006-pseudonimizacao-preserva-contexto.md)
pede que menção a pessoa, a lugar e a organização continue distinguível pelo próprio marcador, o
que não existe se o lugar fica pelo nome.

## Decisão

**O nome próprio de lugar recebe marcador estável com o prefixo LUGAR, como manda a 0012.** O resto
da 0018 permanece: a classe gramatical e a função sintática atravessam a fronteira, e é a gramática
que separa nome de pessoa de nome de lugar e nome próprio de substantivo comum.

## Recusados

**Preservar o topônimo.** Era o que a 0018 dizia. Mantém o nome legível para quem lê o
pseudonimizado, e local exato junto de data e papel singulariza o evento (política 002, "Sai,
mesmo parecendo contexto").

**Remover o topônimo.** Apaga também o fio: duas menções ao mesmo lugar deixam de se reconhecer
como o mesmo lugar, a mesma perda que a 0012 recusa para pessoas.

**Generalizar o lugar** (a cidade vira a região). Exige saber a que região cada nome pertence, o
que nem o serviço de linguagem nem o núcleo sabem.

## Consequências

O marcador diz que dois trechos falam do mesmo lugar sem dizer qual. O nome entra no mapa de
reversão, sob a mesma chave, finalidade e auditoria do nome de pessoa.

Piora a análise que depende do lugar real (região, cidade grande ou pequena): ela passa a exigir
reversão autorizada, item por item.

O destino decidido na entrada fica guardado com cada item, e os gravados antes da mudança guardam
o lugar como preservado. No primeiro pseudonimizado deles, esse destino vira pseudonimizar sem
chamar o serviço de linguagem: é exceção declarada à regra de que a reexecução não decide destino
de novo (`core/src/Core.Ingestion/Sanitizing/SanitizationRerun.cs`, linhas 5 a 8), e vale só para o
lugar, o único destino preservado.

## O que reabre

Se a anotação mostrar que uma categoria depende do lugar real, e não só de ser o mesmo lugar,
generalizar volta à mesa, com a fonte que diga a região de cada nome.
