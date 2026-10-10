# 005 — Quando um dataset está pronto para exportar

> Operacional. 2026-10-04.

Pronto não é "a anotação terminou". É o conjunto de condições abaixo, todas conferidas e registradas
antes de o artefato existir. Falhar em qualquer uma delas não atrasa a exportação por formalidade:
produz um dataset sobre o qual não se pode afirmar o que se vai afirmar.

## Sobre a anotação

- Todo item tem o número mínimo de anotações independentes que a campanha declarou. O que está
  abaixo fica fora, ou sai como parte separada e declarada — nunca misturado em silêncio. O estado
  intermediário do requisito de [atribuição por pessoa](../requisitos/023-atribuicao-por-pessoa.md)
  é o que distingue item fechado de item com uma decisão de duas
- Nenhum item em divergência aberta: cada um está resolvido ou marcado como não decidível
- As métricas de concordância calculadas e publicadas com o artefato, nos escopos de
  [003](003-metricas-de-concordancia.md), e recalculáveis por quem o recebe, como exige o requisito
  de [métricas recalculáveis](../requisitos/016-metricas-recalculaveis.md)
- Os itens excluídos contados, com o motivo agrupado. Exportação que não diz o que descartou
  descreve uma seleção, não um corpus
- O recorte atende os limites de suficiência do domínio — total, por categoria e
  desigualdade entre categorias. Exportação abaixo deles é recusada, não avisada
  ([0023](../decisoes/0023-suficiencia-do-recorte.md))

## Sobre a taxonomia e a forma

- Todo rótulo gravado existe na taxonomia carregada, conferido na exportação e não apenas na
  escrita, como manda a [decisão 0004](../decisoes/0004-taxonomia-configuravel.md)
- Cada unidade satisfaz a validação da sua forma: conversa sem autoria ou sem ordem não sai como
  conversa, conforme a [decisão 0005](../decisoes/0005-unidades-de-analise.md)
- Anotação feita sob definição que a versão vigente redefiniu só entra depois de reanotada, e o que
  a versão nova removeu ou dividiu fica fora, pela
  [decisão 0016](../decisoes/0016-mudanca-de-taxonomia-com-anotacao-existente.md)
- O artefato declara o recorte — quais domínios, sob quais taxonomias, com quais contagens —, que é
  o que a [decisão 0006](../decisoes/0006-dominios-coexistem.md) exige para que um conjunto misto
  não se confunda com um de origem única

## Sobre o material

- Nenhum identificador direto de pessoa, nenhum texto anterior à sanitização e nenhum mapa de
  reversão no artefato, pelo requisito de
  [exportação sem identidade](../requisitos/015-exportacao-sem-identidade.md), que falha fechado
  quando detecta resíduo
- A verificação da custódia rodada antes de exportar, e fechando: a
  [política de verificação](../privacidade/005-verificacao-da-custodia.md) não deixa sair dataset de
  acervo não conferido, e [operação](../operacao/007-verificacao-da-custodia.md) diz quem roda
- O artefato carrega o próprio resumo criptográfico e o topo da cadeia no momento da exportação,
  pelo piso de [distribuição](../privacidade/008-distribuicao.md), para que quem recebe confira que
  tem o que foi gerado

## Sobre repetir

O conjunto descrito em [007](007-reprodutibilidade.md) acompanha o artefato. Dataset que não pode
ser repetido nem comparado é publicação de números, não de dados.
