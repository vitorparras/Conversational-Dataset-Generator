# 010 — Módulo de exportação

> Descrição. 2026-10-04.
> Estado no código: o módulo não existe em `core/src`. Ver a [exportação](../roadmap/009-exportacao.md).

A exportação é a única porta de saída, e o que ela produz não volta: ninguém corrige um dataset que
já está em outra máquina. É o que torna este o módulo mais conservador do pipeline.

Garante que o artefato declara o próprio recorte: quais domínios, sob quais taxonomias, com quais
contagens e com qual referência de integridade
([0006](../decisoes/0006-dominios-coexistem.md)). Sem essa declaração, um dataset de conjunto misto
é indistinguível de um de origem única, e quem o recebe não tem como saber o que tem na mão.

Garante também que nada sai sem passar por três recusas: rótulo fora da taxonomia carregada
([0004](../decisoes/0004-taxonomia-configuravel.md)), recorte sem parecer de validação
([validação](009-modulo-validacao.md)) e auditoria de resíduo que encontre o que a sanitização
deveria ter removido — ver [privacidade](../privacidade/). As três são recusas, não avisos. A
auditoria está aqui porque o reconhecimento de nome é imperfeito, e esta é a última barreira.

Consome material pseudonimizado, anotações e medições. Produz o artefato de dataset e o registro de
que ele existiu. Fala com a validação, com o controle de acesso — exportar é a operação de maior
alcance do sistema — e com a custódia.

## O recorte é declarado, não inferido

Quem exporta diz qual recorte quer, e o módulo confere que o que saiu é o que foi pedido. A
diferença entre declarar e inferir aparece no caso ruim: um filtro que silenciosamente deixou de
fora metade de um domínio produz dataset válido e errado, indistinguível de um correto pela
aparência do arquivo.

## Nenhum texto bruto sai

O artefato contém texto que já passou por sanitização e por pseudonimização. Texto bruto não sai,
nem cifrado, e o mapa de reversão não sai de jeito nenhum: se saísse, a pseudonimização teria sido
teatro ([0003](../decisoes/0003-cifra-em-repouso.md)).

O que sai é dado pseudonimizado, não anônimo
([0012](../decisoes/0012-pseudonimizacao-reversivel.md)). Marca estável carrega informação: a
frequência com que aparece, e ao lado de quais outras, diz coisas sobre a pessoa mesmo sem o nome.

## O artefato é autossuficiente

Quem recebe o dataset não tem este sistema nem o repositório, e ainda assim entende cada rótulo e
refaz as métricas publicadas ([0015](../decisoes/0015-versionamento-e-compatibilidade.md),
[requisito 016](../requisitos/016-metricas-recalculaveis.md)). Então o artefato carrega a taxonomia
inteira, com a definição de cada categoria em vez de um ponteiro para ela, as anotações individuais
distinguíveis por anotador pseudonimizado com a arbitragem marcada como tal, e as versões sob as
quais foi produzido. Métrica que o artefato não sustente não é publicada.

## O que permite reconstruir

Cada linha aponta para o item de onde veio, e o item para o lote de ingestão; com o registro de
custódia, isso basta para refazer o caminho até o arquivo que entrou
([fluxo do material](003-fluxo-do-material.md)). Mas identificador diz a origem, não o conteúdo: o
texto sanitizado de um item pode mudar num reprocessamento posterior, que o
[requisito 009](../requisitos/009-anotacao-de-trecho-estavel.md) prevê. Por isso a exportação guarda
o resumo criptográfico do que incluiu, e não só os identificadores — e é com ele que a exportação
registrada passa a ser o retrato do que foi entregue. Exportar duas vezes o mesmo recorte produz
dois registros, não um atualizado, porque a pergunta que alguém vai fazer é "o que exatamente a
pessoa recebeu naquele dia".
