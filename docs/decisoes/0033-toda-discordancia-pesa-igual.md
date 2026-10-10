# 0033 — Toda discordância pesa igual

> Decisão vigente. 2026-10-07.

## Contexto

Existe coeficiente de concordância que admite **distância entre categorias**: discordar entre duas
categorias vizinhas conta menos que discordar entre duas distantes. Para usá-lo, é preciso declarar
quão próximas as categorias são.

Nenhuma fonte vigente declara isso. A taxonomia
([0004](0004-taxonomia-configuravel.md)) traz identificador, rótulo, definição e exemplos
— não proximidade. A campanha declara protocolo, não vocabulário.

## Decisão

**A proximidade entre categorias não é declarada, e toda discordância pesa igual.** O coeficiente
roda com distância uniforme, e **o resultado declara que foi assim** — número de concordância sem a
régua que o produziu é número sem sentido.

## Recusados

**Declarar a proximidade na taxonomia.** Ficaria junto do que define as categorias, versionado com
elas, e embutiria na métrica um julgamento que varia por quem julga: qualquer tabela de proximidade
é opinião sobre o domínio, apresentada como propriedade dele. Duas pessoas competentes discordariam
sobre quais categorias são vizinhas, e essa discordância passaria a afetar silenciosamente todo
número de qualidade do dataset.

**Declarar na configuração da campanha.** Permitiria medir o mesmo acervo com réguas diferentes, e
duas campanhas do mesmo domínio produziriam números incomparáveis — o oposto do que a medida existe
para permitir.

## Consequências

A medida é **mais severa do que o material talvez mereça**: confundir categorias vizinhas conta como
erro pleno. Isso é conservador na direção certa — a concordância relatada nunca é melhor que a real.

O diagnóstico por categoria continua existindo e é onde a proximidade aparece sem precisar ser
declarada: se duas categorias se confundem sistematicamente, a matriz de divergência mostra isso.
Essa é informação sobre a taxonomia — possivelmente de que as duas deveriam ser uma — e não um peso
a aplicar na medida.

## O que reabre

Se a matriz de divergência mostrar, em campanhas sucessivas, confusão concentrada entre um par
específico de categorias, a resposta é examinar a definição das duas — não acrescentar distância à
medida. A distância volta à mesa apenas se a revisão da taxonomia concluir que a sobreposição é
legítima e irredutível.
