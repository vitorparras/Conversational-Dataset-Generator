# 0006 — Domínios coexistem; a exportação é que recorta

> Decisão vigente. 2026-10-04.

## Decisão

Uma instalação coordena **vários domínios**. Material, anotação e métrica pertencem a um domínio,
cada um com sua taxonomia, e o recorte acontece na saída, não na instalação:

| O que | Escopo |
|---|---|
| Exportação de dataset | Um domínio, ou um conjunto declarado deles |
| Métricas de concordância e qualidade | Por domínio, e comparáveis entre domínios |
| Taxonomia | Por domínio |
| Acesso de pessoas | Por domínio |

O isolamento entre domínios é imposto pelo banco, por política de linha, não por filtro escrito em
cada consulta.

## Por quê

A decisão 0001 estabelece que a ferramenta precisa demonstrar que funciona em mais de um domínio.
Essa demonstração é uma comparação — concordância entre anotadores aqui e ali, comportamento da
sanitização em materiais diferentes — e comparar exige que os dois lados estejam ao alcance da mesma
análise.

Se cada domínio vivesse em sua instalação, comparar viraria exportar, transportar e reimportar
toda vez. O que deveria ser métrica de rotina viraria procedimento manual — e procedimento manual
não é feito.

Pela mesma razão, o recorte pertence à exportação: o dataset publicado é de um domínio, ou de um
conjunto declarado, e quem o recebe precisa saber exatamente de qual recorte ele saiu.

Domínios na mesma base só são aceitáveis se o isolamento não depender de cada consulta estar
correta. Por isso ele é do banco: política de linha com `FORCE`, que vale até contra o dono da
tabela. Consulta que esqueça o filtro não devolve material de outro domínio — não devolve nada.

## Recusados

| Opção | Por quê |
|---|---|
| Uma instalação por domínio | Comparar entre domínios vira exportar, transportar e reimportar — e deixa de ser feito |
| Domínios na mesma base, isolados por filtro na consulta | Basta um filtro esquecido para vazar material entre domínios |
| Sem eixo de domínio | Impossível dizer de onde veio um dataset, nem medir qualidade por origem |

## Consequências

Toda tabela de conteúdo carrega o domínio a que pertence, e toda consulta passa pela política do
banco.

A exportação precisa declarar o recorte no próprio artefato: quais domínios, sob quais taxonomias,
com quais contagens. Sem isso, um dataset de conjunto misto é indistinguível de um de origem única.

Se domínios tiverem sensibilidade diferente, chave por domínio passa a fazer sentido — fica para
decisão própria, antes de existir o segundo domínio real.

## O que reabre

Se um domínio exigir isolamento físico — por exigência legal ou contratual sobre onde o dado reside
—, ele deixa de caber na mesma instalação, e a separação volta a ser por ambiente.
