# 054 — Mutação sem a cobertura por teste

> Operacional. 2026-11-26.

A [039](039-repartir-a-mutacao.md) reparte uma área em partes pelo peso. Esta diz quando uma parte
dispensa a cobertura por teste e como a divisão junta os trechos para que isso aconteça (#935).

## O custo fixo de cada parte

Cada parte de `Core.Persistence.Content` paga uns 586 s antes do primeiro mutante. Na parte 16 de
30 da #935, foram 10 s de análise, 66 s de build, 141 s do run inicial dos 640 testes, 33 s para
gerar os mutantes e 335 s para captar a cobertura de cada teste, rodando-os um a um. Cada mutante
de suíte custa uns 157 s. Para ele, a cobertura não serve: o Stryker roda a suíte inteira de
qualquer jeito.

## A parte só de suíte

A parte em que todo trecho é de suíte, ou inerte, roda com `"coverage-analysis": "off"`. Inerte é
o trecho sem código que o Stryker possa mutar, como um campo sem valor ou uma propriedade
automática: sem parêntese, `=`, `=>`, comando nem acessor com corpo. Na dúvida, o trecho conta
como código. O julgamento não muda: cada mutante continua testado contra a suíte inteira, e a
régua e o teto da [017](017-teste-de-mutacao.md) são os mesmos.

## A divisão

Os trechos que precisam da cobertura vão para partes próprias, as primeiras, e os de suíte e os
inertes, para as outras, cada grupo repartido como diz a 039. A captura pesa `COVERAGE` (425, uns
dois mutantes de suíte) na parte que tem trecho comum. O passo escolhe quantas partes os trechos
comuns levam para que a parte mais pesada custe menos, e só separa quando isso poupa mais de um
mutante de suíte; senão, reparte como antes. O número de partes não muda. No diff do #932, a conta
estimada da parte mais pesada caiu de 586 + 4 × 157 = 1214 s para 251 + 4 × 157 = 879 s.

## A parte do diff sem código

No pull request, a parte do diff sem código a mutar sai da lista de jobs. Se for pedida, passa
com essa nota, sem restaurar ferramentas nem rodar o Stryker. A mutação noturna mantém todas as
partes e nenhum mutante deixa de ser julgado.
