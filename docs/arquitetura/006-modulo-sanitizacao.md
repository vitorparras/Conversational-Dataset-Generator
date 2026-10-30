# 006 — Módulo de sanitização

> Descrição. 2026-10-04.
> Estado no código: o módulo não existe em `core/src`. Ver a [sanitização](../roadmap/004-sanitizacao.md).

A sanitização faz duas coisas que parecem opostas: tira o que a política manda tirar, e não toca em
nada além disso. A dificuldade é que remover ruído e remover sinal se parecem no código e não se
parecem no resultado.

Garante que o texto que segue adiante não contém o que deveria ter saído, nem partido por caractere
invisível ou separador incomum — partir não é esconder
([requisito 004](../requisitos/004-sanitizacao-sem-residuo.md)). Sai o que identifica sem carregar
relação analítica, e sai credencial ou segredo sempre; o que fica e o que vai para pseudônimo é a
política que decide, não este módulo — ver [privacidade](../privacidade/).

Garante, no mesmo passo, que as marcas linguísticas do original ficam: gíria, erro de grafia, caixa
alta, pontuação expressiva, emoji, mistura de línguas
([requisito 005](../requisitos/005-sanitizacao-preserva-o-sinal.md)). Não é delicadeza com o texto;
é o objeto de estudo. Quando as duas garantias se encontram no mesmo trecho, a remoção ganha, e a
perda de sinal é consequência aceita.

Consome o material lido pela ingestão e os destinos que ela atribuiu a cada trecho detectado.
Produz o mesmo material com o texto sanitizado, e o registro do que cada passo alterou. Fala com a
pseudonimização, que recebe o resultado, e com a custódia — não com o serviço de linguagem: o
reconhecimento já aconteceu.

## Passos independentes

Cada normalização é um passo de escopo estreito, que declara o que altera e não altera mais nada. A
razão é prática: quando um passo corrige codificação e de passagem uniformiza pontuação, não há como
saber qual dos dois efeitos produziu uma diferença inesperada no texto.

A ordem entre passos importa e é declarada. Reordená-los é mudança de comportamento, não arrumação.

## Auditável, não confiável

O texto bruto, cifrado, continua disponível para conferência. Toda alteração feita aqui é
verificável contra o original, e é esse par — bruto e sanitizado — que torna a etapa auditável em
vez de confiável ([0003](../decisoes/0003-cifra-em-repouso.md)).

Sanitizar duas vezes dá o mesmo resultado que sanitizar uma vez, e é o que permite reprocessar sem
que o texto derive a cada passagem. Reprocessar, porém, exige que o original ainda esteja
recuperável: sem ele o sistema recusa, em vez de trabalhar sobre texto já alterado
([requisito 002](../requisitos/002-origem-preservada-e-rastreavel.md)).

## Os dois invariantes

Remover sem resíduo e preservar o sinal são afirmações sobre todo texto possível, não sobre os
textos que alguém pensou em testar, e por isso se verificam por propriedade
([0007](../decisoes/0007-garantia-de-qualidade.md)).

O que sai daqui ainda tem nome próprio, alcunha e nome de organização. Eles não são removidos: são
trocados por pseudônimo na etapa seguinte, porque é a relação entre as pessoas que a análise precisa
— ver [pseudonimização](007-modulo-pseudonimizacao.md).
