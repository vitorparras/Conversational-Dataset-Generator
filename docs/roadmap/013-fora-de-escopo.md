# Fora de escopo

> Norma vigente. 2026-10-04.

O que está aqui é trabalho futuro deliberado. Está escrito para que, quando alguém propuser, a
conversa comece em "isto foi considerado e deixado de fora, por este motivo" — e não em "isto estava
previsto e atrasou". Ampliação de escopo e atraso têm o mesmo sintoma e causas opostas.

**Uma quinta forma de material.** A decisão [0005](../decisoes/0005-unidades-de-analise.md) fixa
quatro e nomeia os candidatos mais prováveis: fio de discussão em árvore e transcrição com falas
sobrepostas. Entrar exige decisão que substitua a 0005 — não é configuração.

**Taxonomia mais profunda que categoria e subcategoria.** A decisão
[0004](../decisoes/0004-taxonomia-configuravel.md) prevê que isso muda a estrutura do arquivo e,
portanto, a própria decisão. Enquanto nenhum domínio exigir, não se antecipa.

**Busca sobre texto bruto em operação.** O bruto é cifrado e não indexável, e filtrar sobre ele
acontece depois de decifrar, na aplicação. A decisão
[0003](../decisoes/0003-cifra-em-repouso.md) só reabre isso se a busca passar a ser necessária em
operação, e com a perda de segurança decidida por escrito.

**Anotação sem pessoa.** A assistência está no escopo, e o requisito
[010](../requisitos/010-sugestao-nao-e-anotacao.md) é quem define seus limites: o que a máquina
sugere não entra como decisão de pessoa. Um pipeline que anotasse sozinho não é trabalho futuro —
é outro produto, e contrariaria uma norma vigente.

**Treinar modelo sobre o dataset produzido.** O projeto entrega a base anotada e o método que a
constrói, que são as duas contribuições da decisão
[0001](../decisoes/0001-as-duas-contribuicoes.md). Usar a base é o passo seguinte de quem a recebe.

**Isolamento físico por domínio.** A decisão [0006](../decisoes/0006-dominios-coexistem.md) prevê
que exigência legal ou contratual sobre onde o dado reside tira o domínio da instalação
compartilhada. É condição externa, não etapa planejável.

**Guarda de chave delegada a serviço de nuvem.** Previsto como reabertura da decisão 0003, e
incompatível com [022](../requisitos/022-operacao-sem-servico-externo.md) enquanto ele valer.

**Um terceiro domínio.** A decisão 0001 justifica generalidade por um segundo caso concreto. O
terceiro não acrescenta argumento, só custo.

A publicação do dataset exportado também não é fase deste roadmap: depende das duas licenças, que a
decisão [0009](../decisoes/0009-licenca-do-software-e-dos-dados.md) mantém pendentes e sem as quais
nada é publicado — nem código, nem amostra.
