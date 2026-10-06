# 009 — Módulo de validação e métricas

> Descrição. 2026-10-04.

Este módulo responde a uma pergunta: este recorte de anotação está bom o suficiente para sair? Ele
não melhora nada — mede, aponta divergência, e deixa a decisão para quem tem autoridade sobre o
recorte.

Garante que nenhum número circula sem contexto. Uma medição diz qual métrica, sobre qual recorte, em
que momento e com quais anotações dentro; sem isso, dois números do mesmo nome não se comparam.
Garante também que a comparação entre domínios é possível, porque é ela que demonstra que o método
se transfere ([0001](../decisoes/0001-as-duas-contribuicoes.md)).

Consome anotações, com a versão de taxonomia de cada uma. Produz medições guardadas, a lista de
divergências e o parecer sobre o recorte. Fala com a anotação, de onde vêm os dados e para onde
voltam as divergências, e com a exportação, que não deixa sair recorte sem parecer.

## O que se mede, e quando

Concordância entre pessoas sobre o mesmo alvo é a medida central, e recai sobre a fração do material
que foi anotada duas vezes. Ao lado dela, a cobertura do material e a distribuição das categorias,
que revela tanto desequilíbrio real do domínio quanto categoria mal definida na taxonomia.

A concordância é medida **antes da arbitragem**, sobre as duas anotações originais
([0017](../decisoes/0017-dupla-anotacao-e-arbitragem.md)). Medida depois, mediria o acerto da
arbitragem, que concorda consigo mesma por construção, e não a clareza das definições — que é o que
interessa saber.

Também é calculável desconsiderando os rótulos que vieram de sugestão aceita sem alteração
([requisito 010](../requisitos/010-sugestao-nao-e-anotacao.md)): o mesmo cálculo sobre um
subconjunto, para separar o que as pessoas decidiram do que apenas não contestaram.

Métrica por domínio, e comparável entre domínios
([0006](../decisoes/0006-dominios-coexistem.md)). Anotações sob taxonomias diferentes não entram na
mesma medida ([0004](../decisoes/0004-taxonomia-configuravel.md)): o cálculo recusa, em vez de somar
o que não se soma. Quais métricas, calculadas como, é assunto de metodologia — ver
[pesquisa](../pesquisa/).

## Divergência não é defeito

Duas pessoas que discordam sobre um item disseram algo sobre a taxonomia, não sobre si mesmas.
Divergência concentrada numa categoria é sintoma de definição ambígua, e a resposta certa pode ser
redefinir a categoria — mudança cujo efeito sobre o que já foi anotado tem decisão própria
([0016](../decisoes/0016-mudanca-de-taxonomia-com-anotacao-existente.md)), porque anotação não se
reescreve nem se converte em lote.

A arbitragem é de uma terceira pessoa, que decide e registra o motivo, e não apaga as duas anotações
originais: as três ficam. São elas que produziram a medida, e é nelas que está a evidência de onde a
taxonomia é ambígua.

## Guardar a medição, e deixá-la refazível

Recalcular dá o número de hoje; guardar dá a série, e é a série que diz se a qualidade melhorou
depois de uma mudança na taxonomia ou no treinamento de quem anota.

Guardar não basta. Toda métrica que acompanha um dataset precisa ser refazível por quem o recebe, a
partir do que está no artefato ([requisito 016](../requisitos/016-metricas-recalculaveis.md)).
Métrica que o artefato não permita recalcular não é publicada — é afirmação, e quem recebe só
poderia acreditar.
