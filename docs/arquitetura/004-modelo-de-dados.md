# 004 — O modelo de dados

> Descrição. 2026-10-04.

As entidades do sistema e o que as liga, em nível conceitual. Aqui não há tabela nem coluna: o que
importa é o que existe e o que depende de quê.

O **domínio** é o eixo de tudo. Material, anotação e métrica pertencem a um domínio, e é por ele que
o banco decide o que uma consulta pode ver ([0006](../decisoes/0006-dominios-coexistem.md)). Não é
atributo descritivo; é a dimensão que sustenta o isolamento.

O **lote de ingestão** é a origem. Registra de onde o material veio, com que resumo criptográfico do
conteúdo original, como foi lido, em que estado está e, se houve acomodação de forma, qual perda foi
aceita. Esse resumo é o que reconhece material submetido duas vezes como repetição em vez de item
novo, e o que sustenta a afirmação de que o item não mudou desde que entrou. Todo item vem de um
lote, e é pelo lote que se chega ao arquivo original.

O **item** é a unidade de análise: uma conversa, um comentário, um documento ou uma sequência
rotulada ([0005](../decisoes/0005-unidades-de-analise.md)).

A **parte** é onde a forma se expressa, e é ela que evita quatro modelos paralelos. Numa conversa,
cada parte é uma mensagem com autoria e posição na ordem. Num documento não há autoria por parte, e
a divisão é só de leitura. Numa sequência rotulada é o trecho que a anotação recebe, e seus limites
resolvem sempre para o mesmo texto ou falham à vista — ver [anotação](008-modulo-anotacao.md).

O **participante** é quem fala, já pseudonimizado: um rótulo estável dentro do domínio, que não o
atravessa — a mesma pessoa em dois domínios é dois participantes. A ligação com a pessoa real não
está aqui; está no **mapa de reversão**, cifrado e separado, a única entidade do modelo cujo acesso
revela identidade ([0003](../decisoes/0003-cifra-em-repouso.md)).

A **anotação** liga uma pessoa que anota, um item ou uma parte, uma categoria e um momento. Guarda
também a versão da taxonomia sob a qual foi feita — anotações de taxonomias diferentes não se somam
([0004](../decisoes/0004-taxonomia-configuravel.md)) — e a origem do rótulo, que distingue sugestão
aceita, sugestão corrigida e rótulo criado pela pessoa. Duas anotações do mesmo alvo que discordam
produzem uma divergência, que pode ter uma resolução: outra anotação, com autoria própria, não um
apagamento das anteriores.

A **medição** é um número com contexto: qual métrica, sobre qual recorte, em que momento, com quais
anotações dentro. Guardá-la, em vez de recalculá-la quando perguntarem, é o que permite dizer que a
qualidade mudou — e o contexto é o que permite a quem recebe o dataset chegar ao mesmo número.

A **exportação** é o recorte que saiu: quais domínios, sob quais taxonomias, com quais contagens e
com qual referência de integridade. Aponta para os itens que incluiu e para as medições que a
autorizaram.

A **pessoa que opera** e sua função vivem fora do conteúdo, e a concessão é por domínio — ver
[controle de acesso](011-modulo-controle-de-acesso.md).

O **evento de custódia** não se liga a uma entidade só: aponta para o que foi tocado e encadeia com
o anterior. Nunca é alterado nem removido — como o texto bruto, que também só é lido. A diferença é
que o [requisito 002](../requisitos/002-origem-preservada-e-rastreavel.md) admite o bruto deixar de
ser recuperável, e nada diz em que condição; o evento não tem essa saída.

## O que o modelo não guarda junto

Texto bruto e mapa de reversão existem no mesmo banco, cifrados, e cada um carrega em claro o rótulo
de derivação com que foi cifrado — finalidade e domínio
([0013](../decisoes/0013-chave-de-cifra-por-dominio.md)). Para o modelo são conteúdo opaco: nada se
filtra nem se ordena por eles, e ler exige decifrar na aplicação, o que é
[evento registrado](012-modulo-custodia.md).
