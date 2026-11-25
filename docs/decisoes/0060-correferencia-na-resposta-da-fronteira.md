# 0060 — A resposta da fronteira carrega o grupo de correferência

> Decisão vigente. 2026-11-25. Substitui a decisão 0018 quanto à forma da resposta.

## Contexto

A [0057](0057-escopo-do-marcador-e-a-conversa.md) põe o escopo do marcador na conversa e conta com
o serviço de linguagem para dizer quais menções são a mesma pessoa. A
[0018](0018-contrato-da-fronteira.md) fixa uma resposta por texto, sem grupo, e os modelos pt do
spaCy não trazem correferência ([medição](../historico/003-medicao-do-modelo.md), linha 18). A
[medição](../historico/005-medicao-da-correferencia.md) do Stanza udcoref, pelo
[procedimento 053](../operacao/053-medicao-da-correferencia.md), reprova: precisão 0,823, 0,737 nas
mensagens que misturam línguas, e 0,243 na ligação ao nome do autor. Com 160 mensagens, o pico de
memória passa de 12 GiB.

## Decisão

**O pedido leva os textos de um item na ordem, cada mensagem com o nome do autor; a resposta traz,
por ocorrência, o índice do texto, as posições nele, o tipo, a análise gramatical e o grupo, e o
grupo de cada nome de autor, que nunca volta como ocorrência. O grupo sai de uma regra por nome,
no serviço, sobre a conversa inteira.** O resto da 0018 e da 0011 vale.

- **Mesmo nome, mesmo grupo.** Menções de pessoa com o mesmo texto, comparado sem caixa e sem
  acento, e o nome de autor de mesmo texto formam um grupo.
- **Parte do nome.** A menção de uma só palavra sem par de mesmo texto, de menção ou de autor,
  que seja palavra de um nome mais longo da conversa, também de menção ou de autor, comparada do
  mesmo jeito, entra no grupo dele se houver um só nome candidato. Com dois ou mais, fica em grupo
  próprio.
- **Pronome e apelido** ficam em grupo próprio, cada ocorrência no seu; o apelido de um autor
  recebe marcador distinto do dele.
- **Limiar de precisão: 0,95**, por pares de menções, na conversa toda e, à parte, nas mensagens
  que misturam línguas, para modelo. O Stanza udcoref fica abaixo e não entra; a regra não é
  modelo e entra sem a medição.
- **Tamanho máximo do pedido: nenhum novo.** A regra não carrega modelo, e nada entra na pasta do
  serviço da [0041](0041-distribuicao-do-servico-de-linguagem.md).

## Alternativas recusadas

- **O Stanza udcoref.** Reprova no limiar e funde pessoas distintas (0,207 delas).
- **Um pedido por mensagem.** O grupo não atravessaria mensagens, e é nelas que a mesma pessoa
  reaparece.
- **Agrupar no núcleo.** O serviço já recebe a conversa inteira e devolve o grupo; um modelo futuro
  entra no lugar da regra sem mudar o contrato.

## Consequências

Limites declarados da regra: homônimos na mesma conversa dividem o marcador, e quem anota lê uma
pessoa onde há duas; a menção de uma palavra com dois candidatos e o apelido separam a mesma pessoa
em dois marcadores; o pronome nunca liga. Em conversas distintas, os marcadores seguem distintos
(0057). O [requisito 006](../requisitos/006-pseudonimizacao-preserva-contexto.md) passa a declarar o
limite dos homônimos.

## O que reabre

Modelo novo, versão nova do modelo ou da biblioteca, ou corpus novo: mede-se pelo 053, e modelo com
precisão de 0,95 ou mais nos dois recortes, que caiba na 0041, pode substituir a regra, com o
tamanho máximo do pedido fixado pela medição dele.
