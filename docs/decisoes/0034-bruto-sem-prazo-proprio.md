# 0034 — O texto bruto não tem prazo próprio

> Decisão vigente. 2026-10-07. Completa a decisão 0028.

## Contexto

A arquitetura afirma, em vários pontos, que o texto bruto "continua existindo, cifrado", e nenhuma
norma dizia **até quando**. A [decisão 0028](0028-guarda-descarte-e-esvaziamento.md) fixa o prazo de
guarda do material e o esvaziamento por pedido de remoção, sem distinguir o bruto das demais
versões.

A omissão abria uma porta específica: descartar o bruto **antes** do resto, por um argumento que
soa razoável — ele já foi sanitizado e pseudonimizado, então guardá-lo é só risco acumulado sem
função.

O argumento é falso. O bruto é a única referência contra a qual se confere o que a sanitização
removeu e o que a pseudonimização trocou. Descartá-lo encerra essa conferência para sempre, e a
[decisão 0030](0030-qual-texto-fica-em-claro.md) guarda o texto sanitizado exatamente para
permiti-la.

## Decisão

**O texto bruto não tem prazo de descarte próprio.** Enquanto o sistema operar, ele existe, cifrado.

Duas coisas o alcançam, e são as mesmas que alcançam o acervo inteiro:

**O prazo de guarda do material** — pesquisa em curso, mais três anos após o encerramento
([0028](0028-guarda-descarte-e-esvaziamento.md)). Vencido, o bruto é descartado com o resto.

**O esvaziamento por pedido de remoção** — o conteúdo é substituído por vazio no bruto cifrado
também, pelo mesmo ato, porque apagamento que preserva o original não é apagamento.

Nenhuma rotina de manutenção, otimização de espaço ou redução de superfície descarta o bruto antes
disso. Instalação que o faça deixa de poder auditar as próprias etapas.

## Recusados

**Prazo próprio, mais curto que o do acervo.** Reduziria o conteúdo sensível guardado, e cada item
cujo bruto vencesse se tornaria inauditável — sem aviso, e de forma irreversível. O acervo passaria
a ter duas classes de item: os conferíveis e os que ninguém pode mais conferir.

**Descartar o bruto após a conferência da sanitização.** Pressupõe que a conferência acontece uma
vez. Ela acontece quando alguém tem motivo — numa auditoria, numa dúvida sobre um item, na validação
do método por outra pessoa.

## Consequências

O conteúdo mais sensível do sistema é o que fica guardado por mais tempo, e a guarda da chave passa
a ser a proteção que mais importa — não a vigência de um prazo.

O espaço ocupado cresce de forma monotônica enquanto o sistema opera: três versões por item, duas
cifradas, nenhuma descartada antes do prazo do acervo.
