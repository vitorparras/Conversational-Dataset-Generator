# Contratos entre módulos da fase 4

> Norma pendente. 2026-11-15. Os nomes novos valem como proposta até o plano da fatia.

A rota de tela chama o serviço, nunca o armazenamento direto ([rota](../../padroes/003-rota.md)).
Quem alcança cada rota é `PersonFunctions.Curation`. A sanitização é domínio puro, sem banco nem
rede, em `core/src/Core.Ingestion`, ao lado de `core/src/Core.Ingestion/Deciding`.

## Fatia A

**Submissão → análise.** `SubmissionJudgement.Of` (linhas 14 e 15) confere a forma e descarta o
`ConferenceResult.Accepted` (linha 13); ele passa a seguir em `SubmissionStoring.Kept` (linha
150). Antes do primeiro item, `SubmissionStoring.StoreAsync` (linha 57) pede ao `IRecognizer`
(`RecognizeAsync`, linha 7) uma análise por texto de `MaterialTexts.Of` e entrega todas a
`DecidedMaterial.Decide` (linha 38). Com um texto sem análise, o lote é rejeitado com
analysis_unavailable e nenhum item é gravado.

**Submissão → gravador.** O item sai de `DecidedItemTexts.Of` (linha 17), e de mais nenhum lugar.
O construtor com ocorrências (`core/src/Core.Persistence/Content/Writing/NewItemText.cs`,
linha 15) não leva a acomodação (linha 42); a fatia junta os dois. O plano da fatia confere, por
forma, que os textos de `MaterialTexts.Of` e as unidades de `ReadMaterial.Units` têm a mesma
contagem e a mesma ordem. As contagens por destino da 0056 da fase 3, zeradas até aqui, passam a
sair das mesmas ocorrências, no mesmo comando que grava o item.

## Fatia B

**Passos.** Cada passo declara nome, versão, escopo e o que muda, e devolve as edições, nunca o
texto inteiro. A lista de passos (SanitizationSteps) tem a ordem declarada e a versão derivada dela.
Na subida, um `IStartupCheck` (`core/src/Core.Host/CompositionRoot.cs`, linha 104) recusa passo
sem escopo e diz qual.

**Submissão → sanitização.** Depois da decisão, a sanitização recebe o texto e as ocorrências
decididas e devolve o sanitizado, o rastro e as contagens. O passo dos destinos troca o removido
por um marcador uniforme, deixa o pseudonimizado como está (a pseudonimização é da fase 5) e
traduz cada posição pelo rastro, em ponto de código (#92).

**Gravador.** `ContentWriter.AddItemAsync` (linha 43) sela o sanitizado e o rastro com a
finalidade do bruto, no mesmo `Insertion` (linhas 49 a 56), e grava a versão da 0060 quando ela
ainda não existe. A submissão grava batch_sanitized na cadeia ao fechar a gravação dos itens.

**Tela do lote e amostra.** `BatchRegister` ganha a leitura das contagens pela 0063, só números.
A amostra lê pela 0059 da fase 3, que já devolve a versão mais tratada.

## Fatia E

**Tela → reexecução.** Um serviço novo, SanitizationRerun, compara a versão vigente com a dos
itens de lotes aceitos e reexecuta sobre o bruto: uma transação serializável por item, com o
UPDATE e o evento item_resanitized juntos (0064). Item esvaziado, ou de chave perdida, é recusado
com o motivo, e o resto segue.

## Rotas

| Rota | Verbo | Fatia |
|---|---|---|
| `/lotes/{id}` (a seção da sanitização) | GET | A, B |
| `/lotes/{id}/revisao` (o texto sanitizado) | GET | B |
| `/sanitizacao` | GET | E |
| `/sanitizacao/reexecutar` | GET, POST | E |
