# 0027 — Todas as categorias recebem o mesmo tratamento

> Decisão vigente. 2026-10-07.

## Contexto

Categorias de um domínio não têm a mesma gravidade. Num acervo criminal, classificar errado algo
próximo de homicídio custa mais que classificar errado uma importunação, e a tentação é dar à
categoria grave mais anotadores, mais revisão, piso de concordância mais alto.

A alternativa — tratar todas igual — parece descuido com o que é grave, e não é.

## Decisão

**Toda categoria passa pelo mesmo processo: mesmo número de anotações independentes, mesmo critério
de resolução de divergência, mesmo piso de concordância, mesma chance de entrar na verificação.**

A razão é a integridade da medida. Rigor diferenciado cria duas réguas, e com duas réguas a
concordância de uma categoria deixa de ser comparável com a de outra: uma foi vista por três pessoas
e a outra por duas, e o número não significa a mesma coisa nos dois casos.

Comparar categorias entre si é o que permite dizer que uma está mal definida, que outra se confunde
com a vizinha, ou que o domínio inteiro está bem delimitado. Essa comparação depende de todas terem
passado pelo mesmo processo.

**Gravidade não é atributo que o sistema conhece.** A taxonomia declara categorias; não as ordena
por severidade, e nenhuma etapa do pipeline consulta tal ordenação.

## Recusados

**Mais anotadores para categoria grave.** Melhoraria a confiança naquela categoria ao custo de
tornar a medida dela incomparável com as demais — e a comparação é o instrumento que revela
definição ambígua.

**Prioridade na verificação por gravidade.** Faria a verificação deixar de ser uma amostra do
acervo e passar a ser uma amostra do que alguém considera importante, o que muda o que ela mede.

**Piso de concordância mais alto para categoria grave.** Categoria grave pode ser intrinsecamente
mais difícil de delimitar; exigir mais acordo nela mediria dificuldade, não qualidade.

## Consequências

Categoria rara corre risco de não aparecer em amostra sorteada por acaso — e, como o tratamento é
uniforme, não há privilégio que a traga. Para a uniformidade ser real e não apenas nominal, **a
amostragem precisa garantir representação de todas as categorias**, o que é diferente de privilegiar
algumas: é fazer a mesma régua alcançar todas.

Erro em categoria grave tem a mesma probabilidade que em categoria trivial, e consequência maior.
Isso é aceito, e o contrapeso não está no processo de anotação: está em a exportação declarar a
concordância **por categoria**, para que quem use o dataset veja onde a medida é frágil.

## O que reabre

Se uma categoria se mostrar sistematicamente mais difícil, o caminho é rever a definição dela na
taxonomia — não dar a ela um processo próprio. Se a revisão não resolver, aí sim a uniformidade
precisa ser discutida, com a perda de comparabilidade declarada.
