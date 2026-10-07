# 0020 — A exportação desloca as datas

> Decisão vigente. 2026-10-07.

## Contexto

O acervo guarda data e hora como vieram, porque a sequência dos acontecimentos é parte do que se
analisa. O que a exportação leva ficou em aberto.

Data exata é um dos caminhos mais curtos para reidentificação: basta cruzar com notícia, registro
público ou qualquer outro acervo datado para estreitar quem são as pessoas envolvidas. E o dataset
exportado sai do alcance de quem o produziu.

## Decisão

**Cada acervo exportado tem suas datas deslocadas por uma mesma quantidade, escolhida por acervo e
não divulgada.** Os intervalos entre mensagens permanecem exatos: quem analisa continua vendo que
algo aconteceu três dias depois de outra coisa, ou que uma conversa durou quarenta minutos.

O que se perde é a ancoragem com o mundo externo — justamente o vetor que a reidentificação usa.

O deslocamento é registrado no artefato como existente, sem o valor. Quem recebe precisa saber que
as datas não são absolutas, ou fará análise sazonal sobre eixo falso.

## Recusados

**Data real.** Preserva a sazonalidade e a relação com eventos externos, e é exatamente por isso
que expõe: essa relação é reversível por quem tenha qualquer outro acervo datado.

**Deslocamento por mensagem, ou aleatório.** Destruiria os intervalos, que são o que torna a
sequência analisável. Proteção maior ao custo de inutilizar o dado.

**Escolher caso a caso, por exportação.** Datasets deixariam de ser comparáveis quanto a tempo, e
quem recebe teria de conferir o regime antes de analisar — passo que será esquecido.

## Consequências

Análise que dependa de data absoluta não é possível sobre o dataset exportado: sazonalidade, efeito
de feriado, correlação com evento público. Quem precisar disso precisa de acesso ao acervo, que é
contratual.

O deslocamento de cada acervo é segredo operacional, e guardá-lo é parte da custódia: perdê-lo
impede reconciliar o exportado com o acervo depois.

Dois acervos exportados não são comparáveis entre si no eixo do tempo, já que cada um tem seu
deslocamento. Comparação temporal entre acervos exige outra abordagem.

## O que reabre

Se um acervo tiver autorização expressa para publicação com data real — material já público, por
exemplo —, a regra não se aplica a ele, e isso é registrado na exportação.
