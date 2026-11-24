# 0060 — A resposta da fronteira carrega o grupo de correferência

> Decisão pendente. 2026-11-24. Substituirá a decisão 0018 quanto à forma da resposta.

**Falta a medição do Stanza udcoref e os números**: precisão, fusão, recorte misto, ligação ao
autor, tempo e memória por tamanho. Eles fecham o tamanho máximo, a ligação ao autor e o modelo.

## Contexto

A [0057](0057-escopo-do-marcador-e-a-conversa.md) põe o escopo do marcador na conversa e conta com
a correferência do serviço de linguagem para dizer quais menções são a mesma pessoa. A
[0018](0018-contrato-da-fronteira.md) fixa uma resposta por texto, sem grupo, e os modelos pt do
spaCy não trazem correferência ([medição](../historico/003-medicao-do-modelo.md), linha 18). A
[medição](../historico/005-medicao-da-correferencia.md) mede, pelo
[procedimento 053](../operacao/053-medicao-da-correferencia.md), o Stanza udcoref sobre conversas
sintéticas inteiras: precisão, fusão, precisão nas mensagens que misturam línguas, ligação ao nome
do autor, tempo e pico de memória por tamanho; os números ainda faltam.

## Decisão

**O pedido leva os textos de um item na ordem, cada mensagem com o nome do autor; a resposta traz,
por ocorrência, o índice do texto, as posições nele, o tipo, a análise gramatical e o grupo, e o
grupo de cada nome de autor, que nunca volta como ocorrência.** O resto da 0018 e da 0011 vale.

- **Limiar de precisão: 0,95**, por pares de menções, na conversa toda e, à parte, nas mensagens
  que misturam línguas. Modelo abaixo dele, em qualquer dos dois, não entra.
- **Tamanho máximo do pedido: a fixar pela medição**, em caracteres; acima dele o pedido é
  recusado, sem cortar a conversa.
- **Ligação ao nome do autor: confiável ou limite declarado, pela medição.** Se não for, a menção ao
  autor recebe marcador próprio, distinto do dele.
- **Modelo: o Stanza udcoref, se passar do limiar.** Vai na pasta do serviço da
  [0041](0041-distribuicao-do-servico-de-linguagem.md), sem busca fora ([requisito 022](../requisitos/022-operacao-sem-servico-externo.md)).

## Alternativas recusadas

- **Um pedido por mensagem.** A correferência não atravessa mensagens, e é nelas que a mesma pessoa
  reaparece.
- **Agrupar pelo texto igual no núcleo.** Funde pessoas distintas de mesmo nome, a falha que a 0057
  recusa no escopo do domínio.
- **Cortar a conversa longa em janelas.** O grupo de uma janela não casa com o da outra sem uma
  segunda correferência entre elas.

## Consequências

A medição dirá o quanto crescem a pasta do serviço, a memória dele e o tempo por conversa.

A medida tem dois limites. O corpus põe cada pessoa num identificador (`NOME_A`), e o modelo pode
tratar esse token de outro jeito que um nome próprio. O recorte misto tem 14 mensagens e separa
pouco: agrupar por texto igual acerta todos os pares dele e só reprova pela precisão geral.

## O que reabre

Modelo novo, versão nova do modelo ou da biblioteca, ou corpus novo: mede-se de novo, e precisão
abaixo do limiar tira o modelo. Conversa real acima do tamanho máximo com frequência reabre o
tamanho e as janelas.
