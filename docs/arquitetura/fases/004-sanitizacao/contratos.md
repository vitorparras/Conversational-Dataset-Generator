# Contratos entre módulos da fase 4

> Norma pendente. 2026-11-15. Os nomes novos valem como proposta até o plano da fatia.

A rota chama o serviço, nunca o armazenamento ([rota](../../padroes/003-rota.md)); só a
`PersonFunctions.Curation` a alcança. A sanitização é domínio puro, em `core/src/Core.Ingestion`.
A e B mudam `/lotes/{id}`, B muda `/lotes/{id}/revisao`, e E cria `/sanitizacao`,
`/sanitizacao/reexecutar` (GET e POST) e o acompanhamento, `/sanitizacao/reexecucoes/{id}`.

## Fatia A

**Submissão → análise.** `SubmissionJudgement.Of` (linhas 14 e 15) confere a forma e descarta o
`ConferenceResult.Accepted` (linha 13); ele passa a seguir em `SubmissionStoring.Kept` (linha 150).
Antes do primeiro item, `SubmissionStoring.StoreAsync` (linha 57) pede ao `IRecognizer`
(`RecognizeAsync`, linha 7) uma análise por texto de `MaterialTexts.Of` e entrega todas a
`DecidedMaterial.Decide` (linha 38). Com um texto sem análise, o lote é rejeitado com
analysis_unavailable e nenhum item é gravado. Todo `AnalysisRequest` leva o idioma "pt", fixo,
sem campo na I1 nem escolha por domínio.

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
O pseudonimizado fica como está (fase 5); cada posição se traduz pelo rastro, em ponto de código
(#92). O removido vira um marcador, o tipo entre colchetes, sem caractere do original:

| Quem remove | O quê | Marcador |
|---|---|---|
| O passo da fatia D | número de documento, conta bancária, placa, identificador de aparelho | [documento] |
| O passo da fatia D | telefone, endereço eletrônico | [contato] |
| Os destinos, fatia B | ocorrência de segredo, pela lista de `OccurrenceKinds` | [credencial] |
| Os destinos, fatia B | qualquer outra ocorrência que `DestinationPolicy.Of` remove | [dado] |

O passo da D roda antes dos destinos. A ocorrência que toca, no todo ou em parte, um trecho já
removido sai inteira com ele, qualquer que seja o destino, num só marcador, o do primeiro passo
(#91 CA-5); a posição dela se traduz como "não existe mais" (#92 CA-2).

**Gravador.** `ContentWriter.AddItemAsync` (linha 43) sela o sanitizado e o rastro com a finalidade
do bruto, no mesmo `Insertion` (linhas 49 a 56), e grava a versão (0060). A submissão grava
batch_sanitized ao fechar os itens. `BatchRegister` lê pela 0063 só as contagens por passo; o rastro
segue cifrado (#93). A amostra lê pela 0059 da fase 3, que devolve a versão mais tratada.

## Fatia E

**Tela → fila → reexecução.** A confirmação grava o pedido, enfileira os itens de lotes aceitos de
versão diferente da vigente (0064) e leva ao acompanhamento, sem tratar item; se todos já estão num
pedido aberto da versão vigente, não grava pedido e leva ao acompanhamento dele. Um serviço em segundo plano toma a
posse de um item por vez, com prazo, e SanitizationRerun decifra o bruto e reexecuta numa transação
serializável com o UPDATE, o evento item_resanitized e a conclusão do item na fila. Posse vencida
volta à fila, a última tentativa recusa o item e, na subida, o trabalhador retoma o que ficou.
