# 0001 — As duas contribuições do projeto

> Decisão vigente. 2026-10-04.

Esta decisão diz o que o projeto entrega e como resolver conflito entre essas entregas. Serve a
quem precisa decidir se vale investir numa melhoria que beneficia uma delas e não a outra. Não diz
como a ferramenta é construída — isso fica para as decisões seguintes.

## Contexto

Este projeto produz duas coisas ao mesmo tempo: um dataset conversacional anotado em português
brasileiro, no domínio forense, e a ferramenta que o constrói. As duas vão disputar a mesma decisão
de design com frequência, e é melhor ter a resposta escrita antes da primeira disputa.

Um exemplo concreto, que já se desenha: a ferramenta fica melhor se a lista de categorias vier de um
arquivo de configuração em vez de estar escrita no código, porque assim ela serve a outro domínio
sem precisar ser alterada. Isso não melhora em nada o dataset forense — ele tem as categorias que
tem, e elas não mudam. Se o que importa é o dataset, esse trabalho é desperdício e deve ser cortado.
Se o que importa é a ferramenta, é requisito.

Sem uma resposta escrita, cada pessoa responde do seu jeito, na hora em que a dúvida aparece, e
ninguém registra o motivo. Meses depois ninguém sabe dizer por que metade do sistema é flexível e a
outra metade não.

## Decisão

**As duas são contribuições.** A ferramenta não é meio para chegar ao dataset, e o dataset não é
pretexto para justificar a ferramenta. Cada uma vale por si:

O dataset vale porque recursos anotados em português brasileiro são escassos, e no domínio forense
praticamente inexistentes. Quem quiser treinar ou avaliar modelos nesse tipo de material hoje não
tem onde buscar.

A ferramenta vale porque o procedimento de construir uma base assim — ingerir material heterogêneo,
sanitizar, pseudonimizar sem destruir o sentido, anotar com mais de uma pessoa, medir concordância,
exportar de forma rastreável — é o que se transfere para outros domínios. Um dataset serve ao
domínio que o originou; o método serve a qualquer um que precise das mesmas garantias. E dados
envelhecem: perdem atualidade, mudam de regime legal, deixam de poder ser distribuídos. O método
sobrevive a isso.

Quando as duas disputarem uma decisão, a pergunta é:

> Isso prejudica alguma das duas?

Se prejudica o dataset, não entra, por mais elegante que torne a ferramenta. Se prejudica a
ferramenta, precisa de justificativa escrita — não basta ser mais rápido agora. E quando beneficia
só uma delas sem atrapalhar a outra, entra normalmente: não é preciso que toda melhoria sirva às
duas.

## Alternativas recusadas

**Só o dataset é a contribuição; a ferramenta é meio.** Esta é a leitura mais segura do ponto de
vista de prazo, e foi recusada por um efeito que costuma passar despercebido: uma norma que
subordina tudo à qualidade do dataset faz com que qualquer pessoa — ou agente de IA — que a leia
conclua, corretamente, que investir em generalidade é desperdício. Manter uma regra e esperar que
todos ajam contra ela é pedir que ignorem o que está escrito.

Ela também deixa o projeto sem resposta para a pergunta mais previsível que se pode fazer: o
pipeline funciona fora da base para a qual foi escrito?

**Só a ferramenta é a contribuição; o dataset é um caso de teste.** Recusada porque descarta valor
real. O dataset não é um subproduto de demonstração — é um recurso escasso, cuja construção custa a
maior parte do esforço do projeto. Tratá-lo como teste seria desperdiçar a parte mais trabalhosa do
que será entregue.

**Duas linhas de trabalho paralelas, uma para cada contribuição.** Recusada porque duplica esforço e
cria duas descrições do mesmo pipeline. Duas descrições do mesmo fato divergem com o tempo, e a
divergência só aparece quando alguém age sobre a versão errada.

## Consequências

A lista de categorias do domínio não pode estar escrita no código. Se as categorias forem um tipo
usado nas assinaturas das funções, ou se alguma validação percorrer essa lista para decidir se a
exportação pode acontecer, uma instalação em outro domínio não passa na própria validação do
pipeline. Como isso é resolvido fica para uma decisão específica.

Relatórios e métricas passam a fazer parte do produto, não a ser subproduto da pesquisa. A
ferramenta precisa saber dizer a qualidade da anotação de uma base e comparar bases diferentes,
porque é assim que se demonstra que ela funciona em mais de um lugar.

Em compensação, há mais superfície para manter. Cada ponto onde a ferramenta aceita configuração é
código que precisa de teste, e o prazo não melhora com esta decisão — piora, e isso é consciente.

Há também o risco de generalidade especulativa. Uma ferramenta que tenta servir a tudo é difícil de
projetar e fácil de errar. O contrapeso que adotamos: generalidade se justifica por um segundo caso
concreto, nunca por hipótese. Enquanto não houver um segundo domínio real à vista, a flexibilidade
se limita ao que já é visivelmente necessário.

Por fim, o escopo precisa ser dito com clareza em três faixas — o que foi implementado e validado, o
que foi implementado sem validação formal, e o que ficou como trabalho futuro. Sem essa separação,
quem avaliar o projeto encontra ambição onde deveria encontrar entrega.

## O que reabre esta decisão

Se a revisão por pares não aceitar que as duas entregas sejam tratadas como contribuições, esta
decisão cai, e com ela a justificativa de boa parte do que vem depois.

Se nenhum segundo domínio for identificado até o início da anotação, o risco de generalidade
especulativa passa a pesar mais que esta decisão, e a flexibilidade já construída deve parar onde
está.

Se o prazo apertar ao ponto de a ferramenta geral inviabilizar a entrega do dataset, a prioridade se
inverte: entrega-se o dataset, e a generalidade passa a trabalho futuro declarado — por uma decisão
que substitua esta, nunca por omissão.
