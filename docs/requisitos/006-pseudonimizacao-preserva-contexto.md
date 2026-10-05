# 006 — A pseudonimização preserva o contexto

> Norma vigente. 2026-10-04.

**Trocar identidade por rótulo mantém o material legível: a mesma pessoa é o mesmo rótulo em todo
o escopo, pessoas distintas nunca compartilham rótulo, e o tipo de cada menção continua
reconhecível.**

## Por que existe

Pseudonimizar por ocorrência — rótulo novo a cada aparição — protege e destrói ao mesmo tempo: sem
correferência não se sabe mais quem responde a quem, e uma conversa sem isso não é conversa, como a
[decisão 0005](../decisoes/0005-unidades-de-analise.md) observa a propósito da autoria.

O inverso também falha. Rótulo derivado do nome sem escopo faz o mesmo rótulo reaparecer em
domínios diferentes, e a [decisão 0006](../decisoes/0006-dominios-coexistem.md) mantém domínios na
mesma instalação precisamente para que se possa compará-los — não para que se ligue uma pessoa de um
ao outro por acidente.

## Critérios de aceitação

- Dado o mesmo participante em duas mensagens do mesmo material, quando ele é pseudonimizado, então
  recebe o mesmo rótulo nas duas.
- Dado dois participantes distintos, quando são pseudonimizados, então os rótulos são distintos,
  ainda que os nomes de origem coincidam.
- Dado o mesmo participante em dois domínios, quando é pseudonimizado em cada um, então os rótulos
  não permitem ligar um ao outro sem passar pela reversão autorizada.
- Dado um rótulo exibido, quando alguém tenta deduzir dele a identidade, então ele não carrega nada
  do original — nem inicial, nem tamanho, nem posição alfabética.
- Dado um texto pseudonimizado, quando ele é lido, então menção a pessoa, a lugar e a organização
  continuam distinguíveis entre si.

A prova é uma propriedade: o mesmo original sempre produz o mesmo rótulo no escopo, e dois originais
diferentes nunca produzem o mesmo. Fica prevista em testes/propriedades/pseudonimizacao, ainda
inexistente.

## O que não exige

Não exige rótulo legível ou pronunciável por pessoa. Não exige estabilidade entre instalações
diferentes — a consistência é dentro do escopo, não universal.
