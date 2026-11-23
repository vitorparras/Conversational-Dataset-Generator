# Contratos entre módulos da fase 5

> Norma pendente. 2026-11-22. Os nomes novos valem como proposta até o plano da fatia.

A rota chama o serviço, nunca o armazenamento ([rota](../../padroes/003-rota.md)). A troca é
domínio puro, numa pasta nova em `core/src/Core.Ingestion`, ao lado da sanitização. B muda
`/lotes/{id}`; C cria `/reversao/pedir` (GET e POST), `/reversao/autorizar` e
`/reversao/autorizar/{id}` (GET e POST); D cria `/reversao/meus-pedidos`,
`/reversao/meus-pedidos/{id}` e o POST `/reversao/meus-pedidos/{id}/revelar`. Só a
`PersonFunctions.Curation` as alcança; para as outras funções respondem como endereço inexistente.

## Fatia A

**Deslocamento.** `OffsetConverter` segue o único conversor, e `ResponseValidator` (linhas 54 e 99
a 107) já recusa fim antes do início e trecho fora do texto. A fatia faz a troca aceitar só o
trecho em ponto de código que sai da fronteira, um tipo que nada fora de `core/src/Core.Boundary`
constrói; a unidade é declarada uma vez, nesse tipo, e a gravação a usa.

**Marcador.** Recebe o referente, o tipo e os marcadores já usados no domínio; devolve o marcador
do referente se ele já tem um, ou sorteia outro com gerador criptográfico: o prefixo do tipo e um
número de seis dígitos, fora dos já usados (PESSOA_482913, ORGANIZACAO_071554). O referente é o
autor da mensagem, pelo identificador que o material traz (`Message`), ou a menção, por tipo e
forma normalizada; duas menções de mesmo nome são um referente, porque o serviço não diz quem é
quem. O autor não é gravado hoje em `core/src/Core.Persistence`: a fatia B grava o marcador dele.

**Datas.** Um deslocamento por domínio, sorteado uma vez em dias inteiros, nunca zero, selado; quem
o lê recebe o valor ou "perdido", nunca um valor adivinhado.

## Fatia B

**Submissão → pseudonimização.** Síncrona, logo depois da sanitização e antes de gravar o item:
recebe o sanitizado, as ocorrências a pseudonimizar já levadas ao texto novo (`Sanitizer`, linhas
63 e 107) e o mapa do domínio; devolve o pseudonimizado e as entradas do mapa do item. Ocorrência
que não é de destino pseudonimizar nunca entra no mapa: a tentativa lança, o lote é rejeitado e o
evento fica na cadeia.

**Mapa do domínio.** Achar o marcador de um referente que já apareceu exige abrir os mapas do
domínio na aplicação, porque o banco não filtra conteúdo selado; a leitura grava um evento por
lote, com quantos mapas abriu, antes de abrir (`ProtectedContentReader`).

**Gravador.** `ContentWriter.AddItemAsync` sela o pseudonimizado com a chave do item e grava o mapa
(`ContentWriter.AddReversalMapAsync`) e os marcadores do item no mesmo comando. A reexecução da fase
4 (`ItemResanitizer`) refaz o pseudonimizado e o mapa na mesma transação do sanitizado.

## Fatias C e D

**Pedido → autorização → revelação.** Cada ato é uma linha nova e um evento na mesma transação;
nada se atualiza. O estado do pedido é projeção: pendente, recusado, autorizado até um instante,
vencido ou revelado. Quem autoriza é da Curadoria do domínio e não é quem pediu; com a cadeia
quebrada (`CustodyRefusals`, linha 27), nenhuma autorização é gravada.

**Revelar.** O POST grava a revelação e o evento e confirma a transação; só depois abre a entrada
do mapa do item e devolve a página com a identidade, sem cache (`ScreenResult`, linha 19). Falha
ao gravar não abre nada; a identidade não é gravada nem registrada em lugar algum.

## Fatia E

**Varredura e registro.** Um inventário das leituras de conteúdo, ao lado do teste, conferido contra
as rotas que `ScreenAccessDeclarationTests` já lê: rota nova fora dele reprova. A redação do núcleo
(`DataClassifications`) e os campos fixos do serviço já existem; a fatia leva a prova à troca.
