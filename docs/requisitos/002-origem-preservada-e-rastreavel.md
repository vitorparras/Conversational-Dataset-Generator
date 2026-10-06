# 002 — A origem do material é preservada e rastreável

> Norma vigente. 2026-10-04.

**De qualquer item do acervo é possível chegar ao material como ele chegou, e dizer quando, por quem
e em que lote entrou.**

## Por que existe

A [decisão 0003](../decisoes/0003-cifra-em-repouso.md) guarda o texto anterior à sanitização em vez
de descartá-lo, e essa escolha só se justifica se o original for alcançável: sem ele não há como
refazer a sanitização quando ela erra, nem sustentar que o item publicado corresponde ao recebido.

Num acervo cujo material pode vir de investigação, a pergunta "de onde veio esta frase?" não admite
resposta aproximada. Procedência não é metadado acessório: é o que separa acervo de pasta de
arquivos.

## Critérios de aceitação

- Dado um item anotado, quando se pede sua procedência, então o sistema devolve o lote de entrada,
  o instante da submissão e a pessoa que submeteu.
- Dado o mesmo material submetido duas vezes, quando o segundo envio é processado, então o sistema
  reconhece a repetição em vez de criar um item novo sem relação com o primeiro.
- Dado um item cujo texto original não esteja mais recuperável, quando se pede o reprocessamento da
  sanitização, então o sistema recusa e diz por quê, em vez de reprocessar sobre texto já alterado.
- Dado material recusado na ingestão, quando o registro de entrada é consultado, então a recusa
  aparece com o motivo: nada entra nem deixa de entrar sem registro.

## O que não exige

Não exige preservar o arquivo de entrada em seu formato de origem — exige o conteúdo textual e a
estrutura que o tornou uma das formas reconhecidas. Não exige que o original seja legível por quem
anota: quem o alcança e sob que condição é assunto de [017](017-acesso-negado-por-padrao.md), e a
proteção do que está guardado é de [003](003-copia-do-armazenamento-nao-expoe.md).
