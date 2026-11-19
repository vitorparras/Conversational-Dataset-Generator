# Contratos entre módulos da fase 4

> Norma pendente. 2026-11-15. Os nomes novos valem como proposta até o plano da fatia.

A rota chama o serviço, nunca o armazenamento ([rota](../../padroes/003-rota.md)); só a
`PersonFunctions.Curation` a alcança. A sanitização é domínio puro, em `core/src/Core.Ingestion`.

## Fatia A

**Submissão → análise.** `SubmissionJudgement.Of` (linhas 14 e 15) confere a forma e descarta o
`ConferenceResult.Accepted` (linha 13); ele passa a seguir em `SubmissionStoring.Kept` (linha 150).
Antes do primeiro item, `SubmissionStoring.StoreAsync` (linha 57) pede ao `IRecognizer`
(`RecognizeAsync`, linha 7) uma análise por texto de `MaterialTexts.Of` e entrega todas a
`DecidedMaterial.Decide` (linha 38). Com um texto sem análise, o lote é rejeitado com
analysis_unavailable e nenhum item é gravado. Todo `AnalysisRequest` leva o idioma "pt", fixo: a I1
não tem campo de idioma e não há escolha por domínio.

**Submissão → gravador.** O item sai só de `DecidedItemTexts.Of` (linha 17). O construtor com
ocorrências (`core/src/Core.Persistence/Content/Writing/NewItemText.cs`, linha 15) não leva a
acomodação (linha 42); a fatia junta os dois. O plano da fatia confere, por forma, que os textos de
`MaterialTexts.Of` e as unidades de `ReadMaterial.Units` têm a mesma contagem e ordem. As contagens
por destino da 0056 da fase 3 saem das mesmas ocorrências, no comando que grava o item.

## Fatia B

**Passos.** Cada passo declara nome, versão, escopo e o que muda, e devolve as edições, nunca o
texto inteiro. A lista (SanitizationSteps) tem ordem declarada e versão derivada dela. Na subida, um
`IStartupCheck` (`CompositionRoot`, linha 104) recusa passo sem escopo e diz qual.

**Submissão → sanitização.** Síncrona, depois da decisão e antes de gravar o item, logo antes da
amostra: recebe o texto e as ocorrências decididas e devolve o sanitizado, o rastro e as contagens.
O removido vira o tipo entre colchetes, de uma lista curta e fixa (documento, contato, credencial e
dado, este para o resto), sem caractere do valor original. O pseudonimizado fica como está (fase 5);
cada posição se traduz pelo rastro, em ponto de código (#92).

**Gravador.** `ContentWriter.AddItemAsync` (linha 43) sela o sanitizado e o rastro com a finalidade
do bruto, no mesmo `Insertion` (linhas 49 a 56), e grava a versão da 0060 quando ela ainda não
existe. A submissão grava batch_sanitized na cadeia ao fechar os itens. `BatchRegister` lê pela 0063
só as contagens por passo; o rastro com as posições segue cifrado (#93). A amostra lê pela 0059 da
fase 3, que devolve a versão mais tratada.

## Fatia E

**Tela → fila → reexecução.** A confirmação grava o pedido, enfileira os itens de lotes aceitos de
versão diferente da vigente (0064) e leva ao acompanhamento, sem tratar item. Um serviço em segundo
plano toma a posse de um item por vez, com prazo, e SanitizationRerun decifra o bruto e reexecuta
numa transação serializável com o UPDATE, o evento item_resanitized e a conclusão do item na fila.
Esvaziado ou de chave perdida, o item é recusado com o motivo. Posse vencida volta à fila, a última
tentativa recusa o item e, na subida, o trabalhador retoma o que ficou.

## Rotas

| Rota | Verbo | Fatia |
|---|---|---|
| `/lotes/{id}` (a seção da sanitização) | GET | A, B |
| `/lotes/{id}/revisao` (o texto sanitizado) | GET | B |
| `/sanitizacao` | GET | E |
| `/sanitizacao/reexecutar` | GET, POST | E |
| `/sanitizacao/reexecucoes/{id}` (o acompanhamento) | GET | E |
