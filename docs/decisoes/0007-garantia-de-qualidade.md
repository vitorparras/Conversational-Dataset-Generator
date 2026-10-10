# 0007 — A garantia de qualidade dos testes

> Decisão vigente. 2026-10-04.

## Decisão

A qualidade é sustentada por quatro mecanismos, nenhum deles suficiente sozinho:

| Mecanismo | Onde se aplica |
|---|---|
| Revisão por pull request | Toda mudança, sem exceção |
| Cobertura mínima por área | Mais alta onde a falha vaza dado ou corrompe custódia |
| Teste baseado em propriedades | Sobre os invariantes de pseudonimização e de custódia |
| Teste de mutação | Nos módulos críticos, bloqueando quando a ferramenta permitir |

Os pisos de cobertura e a lista de módulos críticos ficam em documento operacional, não aqui — eles
mudam com o código, e esta decisão não deve mudar junto.

## Por quê

Cobertura mede quais linhas foram executadas, não se o teste perceberia um erro nelas. Um teste que
executa a função inteira e não verifica o resultado conta como cobertura cheia. Em código que decide
o que é dado pessoal e o que entra numa cadeia de custódia, essa diferença é a que importa.

Teste de mutação mede o que falta: altera o código de propósito e verifica se algum teste reclama.
É a única medida que responde "estes testes detectariam um erro?".

Teste baseado em propriedades cobre o que exemplo não cobre. Os invariantes deste sistema são
afirmações sobre todo texto possível — nenhum documento sai da sanitização contendo o que deveria
ter sido removido —, e exemplo escolhido à mão testa só os casos em que quem escreveu pensou.

Revisão por pull request é o que nenhum dos três dá: julgamento sobre se a mudança deveria existir.

## Recusados

| Opção | Por quê |
|---|---|
| Só cobertura, com piso alto | Mede execução, não detecção. Dá confiança sem base |
| Só mutação | Caro demais para rodar sobre todo o código a cada mudança |
| Nenhum piso, confiando na revisão | Revisão cansa e deixa passar; piso automático, não |

## Consequências

Os invariantes precisam estar escritos antes dos testes que os verificam, porque teste baseado em
propriedades parte de uma afirmação, não de um exemplo.

Teste de mutação sobre todo o código é inviável no tempo de uma verificação de pull request.
Restringi-lo aos módulos críticos exige que a lista desses módulos exista e seja mantida.

## O que reabre

Se a ferramenta de mutação disponível se mostrar instável a ponto de reprovar mudança correta, ela
passa a rodar fora do bloqueio, em modo informativo — por decisão escrita, não por desativação
silenciosa.
