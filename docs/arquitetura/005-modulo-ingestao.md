# 005 — Módulo de ingestão

> Descrição. 2026-10-04.
> Estado no código: o módulo não existe em `core/src`. Ver a [ingestão](../roadmap/003-ingestao.md).

A ingestão transforma arquivo em material do acervo, ou recusa o arquivo. Não há terceiro resultado,
e é essa recusa que faz dela um módulo em vez de um importador.

Garante que nada entra no acervo sem forma conhecida e sem origem registrada
([requisito 001](../requisitos/001-forma-validada-na-ingestao.md)). A forma é declarada e conferida
contra o conteúdo, não adivinhada; quando o material não se encaixa em nenhuma das quatro
([0005](../decisoes/0005-unidades-de-analise.md)), ele só entra acomodado em uma delas, e o item
declara que forma recebeu e o que se perdeu na acomodação. Material sem domínio não existe: o
domínio é declarado na abertura do lote, antes de qualquer leitura.

Garante também que de qualquer item se chega ao material como ele chegou, e se diz quando, por quem
e em que lote entrou ([requisito 002](../requisitos/002-origem-preservada-e-rastreavel.md)). O mesmo
material submetido duas vezes é reconhecido como repetição, não vira um item novo sem relação com o
primeiro.

Consome arquivos, um leitor por formato de origem. Cada leitor conhece um formato e produz sempre a
mesma coisa: uma forma de material com suas partes. Acrescentar formato é acrescentar leitor, e isso
não toca em nada depois da ingestão — é o ponto de extensão mais provável deste módulo.

Produz o lote, os itens com suas partes, e o texto bruto gravado cifrado
([0003](../decisoes/0003-cifra-em-repouso.md)). O bruto é guardado porque é contra ele que se
confere, depois, o que as etapas seguintes fizeram; sem ele a sanitização seria inauditável.

Produz também o destino de cada trecho identificável detectado — remover, pseudonimizar ou
preservar. Nada é gravado sem isso, e é por isso que este módulo fala com o serviço de linguagem: o
reconhecimento acontece sobre o texto como ele chegou. Quem aplica os destinos são as duas etapas
seguintes; quem os escolhe, pela política, é aqui — ver [privacidade](../privacidade/).

Fala com a sanitização, que recebe o material lido, e com a custódia, onde registra a abertura do
lote, o resultado da validação e a decisão de aceitar ou rejeitar.

## O lote, e por que ele existe

Material rejeitado não pode deixar resíduo no acervo. Se a leitura falha na metade, o que já foi
lido não é material válido pela metade — é lixo com aparência de dado. O lote é o escopo dentro do
qual isso se resolve: é aceito inteiro ou rejeitado inteiro, e o acervo só vê o que foi aceito.

Rejeitar não apaga o registro de que houve tentativa. O lote rejeitado continua existindo, com o
motivo, porque "este arquivo nunca entrou no acervo" é uma afirmação que alguém vai precisar fazer.

A aprovação também não é automática: há revisão humana de uma amostra do lote antes dela. Ela
existe por um motivo específico — o reconhecimento automático não encontra tudo, e o lote é o último
ponto em que o material ainda pode ser recusado inteiro. Qualquer achado reprova o lote por
completo: é gate, não estimativa. Ver [privacidade](../privacidade/).

## O ponto frágil

A validação de forma é a única barreira entre um arquivo mal lido e um dataset com autoria trocada.
Conversa cuja ordem foi inferida do nome do arquivo, mensagem cuja autoria foi deduzida da posição
na página: os dois casos atravessam qualquer verificação que não seja específica da forma, e o
estrago só aparece na análise, quando o dataset já circulou.

É também o ponto em que a diferença entre "não deu para ler" e "deu para ler mais ou menos" precisa
ser decidida pelo módulo, não por quem opera. Leitura parcial aceita por conveniência é o modo mais
barato de corromper um acervo.
