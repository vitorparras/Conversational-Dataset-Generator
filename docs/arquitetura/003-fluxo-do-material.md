# 003 — O fluxo do material

> Descrição. 2026-10-04.
> Estado no código: nenhuma etapa existe; só a rota de análise (`core/src/Core.Host/Analysis`). Ver a [ingestão](../roadmap/003-ingestao.md) em diante.

Do arquivo bruto ao dataset exportado há oito etapas. Cada uma tem um antes e um depois
verificáveis, e nenhuma delas descarta o que a anterior produziu.

**Recebimento.** Um arquivo chega e abre um lote de ingestão. O arquivo é identificado pelo
conteúdo, não pelo nome, e o lote guarda de onde veio e em que estado está. Nada aqui é material
oficial ainda: o lote existe para que a rejeição seja possível sem deixar resíduo no acervo.

**Leitura.** O conteúdo é interpretado como uma das quatro formas
([0005](../decisoes/0005-unidades-de-analise.md)) e validado contra o que aquela forma exige —
conversa sem autoria por mensagem ou sem ordem não é conversa, e aceitá-la em silêncio produz
dataset que parece válido. O texto bruto é gravado cifrado
([0003](../decisoes/0003-cifra-em-repouso.md)). Se a forma não couber, a adaptação fica declarada no
lote, com a perda que ela implica.

**Atribuição de destino.** Cada trecho identificável detectado recebe um destino — remover,
pseudonimizar ou preservar —, e isso se decide uma vez, aqui, não a cada consulta. O
reconhecimento acontece sobre o texto como ele chegou, e é o que faz a travessia até o serviço de
linguagem ser de texto ainda não sanitizado ([fronteiras](002-fronteiras.md)). As duas etapas
seguintes aplicam esses destinos; não os escolhem.

**Sanitização.** Sai o artefato de formato, e sai o que a política manda remover: o que identifica
sem carregar relação analítica, e credencial ou segredo. Fica o registro linguístico, que é o objeto
de estudo. O texto bruto continua existindo, cifrado, e é contra ele que se confere depois o que a
sanitização fez.

**Pseudonimização.** O que resta apontando para pessoas é trocado por rótulos estáveis, e a
correspondência vai para o mapa de reversão, cifrado com chave e finalidade próprias. Estável é a
palavra que importa: a mesma pessoa é o mesmo rótulo em todo o domínio, porque é isso que mantém a
conversa legível depois de anonimizada.

**Anotação.** Pessoas atribuem categorias da taxonomia do domínio
([0004](../decisoes/0004-taxonomia-configuravel.md)). O texto não muda nesta etapa; o que se acumula
é o que foi dito sobre ele, por quem e quando. Uma fração declarada do material é anotada por duas
pessoas, independentemente ([0017](../decisoes/0017-dupla-anotacao-e-arbitragem.md)).

**Validação.** A concordância entre anotações é calculada, as divergências são identificadas e
resolvidas, e o resultado diz se um recorte está pronto para sair.

**Exportação.** Um recorte declarado de domínios sai como dataset, com as taxonomias sob as quais
foi anotado, as contagens e a referência de integridade
([0006](../decisoes/0006-dominios-coexistem.md)).

## O que fica registrado

Cada passagem deixa um evento na custódia: o que entrou, o que saiu, quem pediu e quando. Decifrar
o mapa de reversão é o evento mais sensível da lista, e é registrado como qualquer outro — ver
[custódia](012-modulo-custodia.md).

## O que o fluxo preserva

Em nenhum ponto o material anterior é substituído pelo posterior: o bruto cifrado, o sanitizado, o
pseudonimizado e as anotações coexistem, e é isso que torna cada etapa auditável em vez de
confiável. O resultado é poder responder "de onde veio esta linha do dataset" sem depender da
memória de quem operou — a linha aponta para o item, o item para o lote, e o lote para o arquivo que
entrou.
