# 006 — A pseudonimização preserva o contexto

> Norma vigente. 2026-11-25.

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

**O escopo é a conversa** ([0057](../decisoes/0057-escopo-do-marcador-e-a-conversa.md)). As
mensagens da mesma conversa compartilham os rótulos, e um rótulo nunca se repete em outra
conversa; nas formas sem fio de conversa, cada item vale como uma. Ligar conversas exige saber
que dois nomes são a mesma pessoa, e o material só traz o nome salvo por quem exportou. A análise
entre conversas fica como melhoria futura.

## Critérios de aceitação

- Dado o mesmo participante em duas mensagens da mesma conversa, quando ela é pseudonimizada,
  então ele recebe o mesmo rótulo nas duas, e esse rótulo não aparece em nenhuma outra conversa.
- Dado dois participantes distintos, quando são pseudonimizados, então os rótulos são distintos,
  ainda que os nomes de origem coincidam, salvo os homônimos da mesma conversa (abaixo).
- Dado o mesmo participante em dois domínios, quando é pseudonimizado em cada um, então os rótulos
  não permitem ligar um ao outro sem passar pela reversão autorizada.
- Dado um rótulo exibido, quando alguém tenta deduzir dele a identidade, então ele não carrega nada
  do original — nem inicial, nem tamanho, nem posição alfabética.
- Dado um texto pseudonimizado, quando ele é lido, então menção a pessoa, a lugar e a organização
  continuam distinguíveis entre si.

A prova é uma propriedade: o mesmo referente sempre produz o mesmo rótulo na conversa, dois
referentes diferentes nunca produzem o mesmo, e nenhum rótulo aparece em duas conversas. Fica
prevista em testes/propriedades/pseudonimizacao, ainda inexistente.

## O que não exige

Não exige rótulo legível ou pronunciável por pessoa. Não exige estabilidade entre instalações
diferentes — a consistência é dentro do escopo, não universal.

Não exige separar homônimos na mesma conversa: pela regra por nome da
[0060](../decisoes/0060-correferencia-na-resposta-da-fronteira.md), dois participantes com o mesmo
nome de origem na mesma conversa dividem o rótulo. É limite declarado; em conversas distintas, os
rótulos seguem distintos.

Não exige o mesmo rótulo entre as mensagens de uma conversa gravada antes da F3
([0057](../decisoes/0057-escopo-do-marcador-e-a-conversa.md), emenda): cada mensagem antiga recebe
os seus. É limite declarado; a conversa gravada depois segue o primeiro critério.
