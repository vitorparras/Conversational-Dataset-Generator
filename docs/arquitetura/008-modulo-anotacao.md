# 008 — Módulo de anotação

> Descrição. 2026-10-04.
> Estado no código: o módulo não existe em `core/src`. Ver a [anotação](../roadmap/007-anotacao.md).

A anotação é onde o trabalho humano acontece. O que o software faz aqui é tirar do caminho tudo o
que não é decidir, e registrar com exatidão o que foi decidido — inclusive como foi decidido.

Garante que toda anotação tem autoria, momento, a versão da taxonomia sob a qual foi feita e a
origem do rótulo, e que nenhum rótulo fora da taxonomia carregada é aceito
([0004](../decisoes/0004-taxonomia-configuravel.md)). A validação na escrita existe porque erro de
digitação que vira categoria nova em silêncio faz a medida de concordância mentir.

Consome o material pseudonimizado e a taxonomia do domínio. Produz anotações: pessoa, alvo,
categoria, versão da taxonomia, origem, momento. O texto não muda nesta etapa, e anotação gravada
não é reescrita ([0016](../decisoes/0016-mudanca-de-taxonomia-com-anotacao-existente.md)).

Fala com a validação, que mede o que foi produzido aqui, com o controle de acesso, que decide quem
alcança qual domínio, e com a custódia.

## Assistida, e quem determina a origem do rótulo

A anotação é assistida, e assistência acelera e enviesa pelo mesmo mecanismo: diante de uma sugestão
plausível, concordar custa menos que examinar. Por isso o rótulo distingue sugestão aceita sem
alteração, sugestão corrigida e rótulo criado pela pessoa
([requisito 010](../requisitos/010-sugestao-nao-e-anotacao.md)). Sugestão ignorada não grava nada.

Essa origem é derivada no núcleo, confrontando o que foi submetido com a sugestão que o núcleo
serviu — "aceita sem alteração" e "corrigida" não se distinguem olhando o rótulo final. Quem anota
não a declara, e nem poderia: seria a superfície cujo viés a classificação existe para medir
classificando a si mesma.

## Independentes até fecharem

Uma fração declarada do material de cada domínio é anotada por duas pessoas; o resto, uma vez
([0017](../decisoes/0017-dupla-anotacao-e-arbitragem.md)). Enquanto as duas anotam, nenhuma alcança
a anotação da outra, por tela ou por qualquer outra via
([requisito 011](../requisitos/011-anotacoes-independentes.md)) — e nenhuma sabe que aquele item
está na fração, porque saber muda o cuidado, e o que se mediria então seria o cuidado.

Não é regra de conduta: se o sistema permite a consulta, ela acontece e não deixa rastro. O item
servido é indistinguível esteja na fração ou fora dela. Fechadas as anotações, as duas passam a ser
mutuamente visíveis a quem arbitra.

## O alvo depende da forma

Numa conversa, num comentário ou num documento, o alvo é o item inteiro ou uma de suas partes. Numa
sequência rotulada o alvo é o trecho, e os limites precisam resolver depois para o mesmo texto que
foi anotado, ou falhar de forma visível — nunca para o trecho vizinho
([requisito 009](../requisitos/009-anotacao-de-trecho-estavel.md)). Deslocamento é pior que perda:
os rótulos continuam todos lá, bem formados, apontando para outra coisa.

## Texto em claro, e o que isso custa

O material desta tela está em claro no banco porque é o que permite buscar, filtrar e ordenar
enquanto se anota ([0003](../decisoes/0003-cifra-em-repouso.md)). Ter passado pela pseudonimização
não garante que esteja limpo: o que o reconhecimento não marcou segue no texto com aparência de
conteúdo comum, e esse resíduo está em claro — ver [privacidade](../privacidade/). É por isso que
esta é a maior superfície de exposição do sistema em operação normal, e que a leitura aqui é acesso
registrado como qualquer outro.
