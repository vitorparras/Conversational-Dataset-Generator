# Modelo de dados da fase 4

> Norma pendente. 2026-11-15. Os números são provisórios: o número se fixa quando o PR abre.

A última migração do main é a 0054 (`core/src/Core.Persistence/Migrations/Sql`), e a fase 3
reserva da 0055 à 0059. As cinco abaixo seguem sem lacuna e andam juntas se outra entrar antes. A
fatia A não tem migração: analysis_unavailable cabe na regra de motivo da 0034 (linha 57), e as
ocorrências já têm coluna (0052, linha 7). C e D também não: um passo novo muda a versão, e a 0060
guarda a versão nova na primeira vez.

## Fatia B

**`0060-sanitization-version`.** A tabela `content.sanitization_version`: a versão, que é a
impressão digital da lista de passos, e os passos na ordem, cada um com nome e versão. A aplicação
a grava na primeira vez que a usa e nunca a muda. Sem ela, a versão de um item não diz quais
passos rodaram (#87 CA-1), e a tela do lote não tem o nome de cada contagem. Nenhum texto entra.

**`0061-item-sanitization`.** Três colunas em `content.item_text`:

- sanitization_version, em claro, que aponta a versão da 0060;
- sanitization_trail, selada com a finalidade do bruto (`protected:texto-bruto`, como a 0052,
  linha 9): as edições de cada passo, com tipo e posição em ponto de código (#92, #93 CA-1);
- sanitization_counts, em claro, um inteiro por passo, na ordem da versão: só quantas edições.

O sanitizado e as três são gravados no mesmo comando que grava o item, como as ocorrências
(0052, linha 1). Um CHECK exige as quatro juntas ou nenhuma, e um gatilho exige uma contagem por
passo da versão. O esvaziamento limpa o rastro e as contagens com o resto: item_text_emptied,
o GRANT de UPDATE e emptied_content_is_final, na versão da 0056 da fase 3, que já os refez com as
contagens por destino (0052, linhas 12 a 28), são refeitos com elas. As colunas entram em
`ContentColumns` (`Protected`, linha 63; `Clear`, linhas 66 a 77), e a contagem pede um
`ClearKind` novo, pois nenhum dos oito de hoje é número.

**`0062-batch-sanitized-event`.** O passo batch_sanitized na cadeia, com a versão como
referência, entra em custody_event_step_listed e em custody_event_batch_shape, como a 0054 fez
com batch_sampled (linhas 8 a 30). A sanitização é evento do registro
([cadeia de custódia](../../../privacidade/004-cadeia-de-custodia.md), linha 10).

**`0063-batch-sanitization-counts`.** A função `batch_sanitization_counts(of_batch, of_domain)`,
com `SECURITY DEFINER` como a 0057 da fase 3. Devolve a versão e, por passo, a soma das edições e
os itens alterados: só números e nomes de passo. A tela do lote é de um lote aberto, que a política
batch_accepted_only esconde.

## Fatia E

**`0064-sanitization-rerun`.** A fila: content.sanitization_rerun, o pedido (versão alvo, quem pediu
e quando), e content.sanitization_rerun_item, com chave (pedido, item), estado (na fila, com posse
ou concluído), prazo da posse, tentativas, quando foi concluído e resultado (idêntico, substituído,
recusado ou superado); um índice único parcial deixa o item em um só pedido aberto, e pedido
repetido não o enfileira de novo. A troca: o GRANT de UPDATE do sanitizado e das três colunas da
0061, e um gatilho que só a deixa passar quando a mesma transação gravou o evento item_resanitized e
concluiu o item da fila, e nunca num item esvaziado. A cadeia: os passos sanitization_rerun (um por
pedido) e item_resanitized (um por item), e os motivos sanitization_identical,
sanitization_replaced, sanitization_refused e sanitization_superseded, este sem UPDATE.

## O que não muda

A coluna em claro do pseudonimizado fica nula em toda a fase (critério 4 do roadmap); o bruto e as
ocorrências (0052) não mudam, e a reexecução só lê o bruto.

Ver [riscos](riscos.md) para o que cada migração precisa provar em teste de banco.
