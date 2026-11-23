# Contratos entre módulos da fase 5

> Norma pendente. 2026-11-22. Os nomes novos valem como proposta até o plano da fatia.

A rota chama o serviço, nunca o armazenamento ([rota](../../padroes/003-rota.md)). A troca é
domínio puro, numa pasta nova em `core/src/Core.Ingestion`. B muda `/lotes/{id}`; C cria
`/reversao/pedir`, `/reversao/autorizar`, `/reversao/autorizar/{id}`, `/reversao/meus-pedidos` e
`/reversao/meus-pedidos/{id}`; D cria `/reversao/meus-pedidos/{id}/revelar` (GET confirma, POST
revela). Só a `PersonFunctions.Curation` as alcança; id inexistente ou de outro domínio responde
como endereço inexistente.

## Fatia A

**Deslocamento.** A conversão de unidade só acontece em `OffsetConverter`, e `ResponseValidator`
(linhas 54 e 99 a 107) já recusa fim antes do início e trecho fora do texto. A troca recorta só por
`ITextBoundaries`, como o `Sanitizer` (linha 40); a tradução pelo rastro muda a posição, não a
unidade. Sobreposição entre duas ocorrências não é recusa: é fundida, como na fase 4.

**Marcador.** Recebe o referente, o tipo e os marcadores já usados no domínio; devolve o marcador
do referente se ele já tem um, ou sorteia outro com gerador criptográfico: o prefixo do tipo e seis
dígitos, fora dos já usados (PESSOA_482913, ORGANIZACAO_071554). O referente proposto é o autor da
mensagem, pela forma que o material traz (`Message`, linha 9), ou a menção, por tipo e forma
normalizada no domínio; é pendente do dono, porque o serviço não diz quem é quem.

**Datas.** Um deslocamento por domínio, sorteado uma vez em dias inteiros, nunca zero, selado; quem
o lê recebe o valor ou "perdido", nunca um valor adivinhado.

## Fatia B

**Submissão → pseudonimização.** Síncrona, depois da sanitização e antes de gravar o item: recebe o
sanitizado, as ocorrências a pseudonimizar já levadas ao texto novo (`Sanitizer`, linhas 63 e 107)
e os mapas do domínio; devolve o pseudonimizado e as entradas do mapa do item. Ocorrência de outro
destino nunca entra no mapa: a troca lança, o lote é rejeitado e o evento fica na cadeia. O
registro de falha leva lote, posição do item, passo e motivo, porque o item ainda não existe.

**Mapas do domínio.** Achar o marcador de quem já apareceu exige abrir na aplicação os mapas do
domínio, porque o banco não filtra selado; cada mapa aberto grava antes o evento de leitura de
`ProtectedContentReader`. É pendente do dono: custo e eventos crescem com o acervo.

**Gravador.** `ContentWriter.AddItemTextAsync` sela o pseudonimizado com a chave do item; o mapa
(`ContentWriter.AddReversalMapAsync`) e os marcadores vão no mesmo comando. A reexecução da fase 4
(`ItemResanitizer`) grava mapa novo e esvazia o anterior na transação do sanitizado.

## Fatias C e D

**Pedido → autorização → revelação.** Cada ato é uma linha nova e um evento na mesma transação;
nada se atualiza. O evento aponta a linha do ato (o pedido, a autorização, a revelação), que guarda
quem, quando e o escopo; o texto livre do pedido vai selado na linha, não no evento. O estado do
pedido é projeção: pendente, recusado, autorizado até um instante, vencido ou revelado. Quem
autoriza é da Curadoria do domínio e não é quem pediu. Com a cadeia quebrada (`CustodyRefusals`,
linha 27), que é uma só na instalação, nenhum ato é gravado, nem a recusa.

**Revelar.** O GET confirma em página própria; o POST grava revelação e evento e confirma, só então
abre o mapa e responde a identidade sem cache (`ScreenResult`, linha 19). Falha ao gravar não abre.

## Fatia E

**Varredura e registro.** Um inventário das leituras de conteúdo, ao lado do teste, conferido contra
as rotas que `ScreenAccessDeclarationTests` já lê: rota nova fora dele reprova. A redação do núcleo
(`DataClassifications`) e os campos fixos do serviço já existem; a fatia leva a prova à troca.
