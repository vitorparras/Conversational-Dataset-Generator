# 008 — O rótulo gravado pertence à taxonomia vigente

> Norma vigente. 2026-10-04.

**Nenhuma anotação é gravada ou exportada com categoria fora da taxonomia carregada, e o sistema não
opera com taxonomia inválida.**

## Por que existe

A [decisão 0004](../decisoes/0004-taxonomia-configuravel.md) tira as categorias do código, e com
isso tira do compilador a verificação dos rótulos. A consequência é explícita: a validação passa a
ser de execução, no carregamento e em cada escrita.

Sem ela, um erro de digitação vira categoria nova em silêncio. O efeito não é um rótulo errado: é a
medida de concordância passar a mentir, porque duas pessoas que concordaram aparecem discordando, e
ninguém procura a causa num lugar que deveria ser impossível.

## Critérios de aceitação

- Dado um arquivo de taxonomia ausente, malformado ou com identificador repetido, quando o sistema
  inicia, então ele não sobe, e a mensagem nomeia o defeito.
- Dado um rótulo que não consta na taxonomia do domínio do item, quando a anotação é gravada, então
  a gravação é recusada.
- Dado um conjunto pronto para exportar, quando a exportação roda, então ela valida os rótulos outra
  vez e para ao encontrar categoria desconhecida.
- Dado uma anotação gravada, quando se pergunta sob qual taxonomia ela foi feita, então a resposta
  identifica a versão exata, e não apenas o domínio.

## O que não exige

Não exige que domínios compartilhem taxonomia — cada um tem a sua. Não exige que a taxonomia seja
imutável: mudá-la depois de haver anotação é mudança de dado, não de configuração, e a decisão 0004
manda decidir por escrito o que acontece com o que já foi anotado. Não exige validar a qualidade das
definições, que é assunto de método.
