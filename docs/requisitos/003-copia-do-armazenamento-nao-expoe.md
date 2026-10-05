# 003 — Uma cópia do armazenamento não expõe nada

> Norma vigente. 2026-10-04.

**Levar o armazenamento inteiro para fora do sistema não revela texto anterior à sanitização nem
identidade de pessoa.**

## Por que existe

É o critério único da [decisão 0003](../decisoes/0003-cifra-em-repouso.md), e ele descreve o
vazamento que de fato acontece: cópia de salvaguarda esquecida, disco descartado, acesso indevido à
máquina que hospeda o acervo. Em nenhum desses cenários alguém precisa derrotar o sistema — basta
copiar o que está parado.

A garantia é formulada como observação, não como mecanismo, porque é assim que ela se testa: pega-se
o conjunto, lê-se sem o segredo, e procura-se qualquer trecho do que foi submetido.

## Critérios de aceitação

- Dado um despejo completo do armazenamento, quando alguém o lê sem o segredo de cifra, então nem o
  texto anterior à sanitização nem o mapa que liga rótulo a identidade são legíveis.
- Dado o armazenamento e a máquina que o hospeda, quando ambos estão comprometidos mas o segredo
  não, então o resultado continua ilegível.
- Dado o segredo ausente, errado ou inacessível, quando o sistema inicia, então ele falha
  visivelmente, em vez de seguir operando e gravar em claro o que deveria cifrar.
- Dado um conteúdo protegido, quando um filtro ou ordenação é pedido sobre ele, então a operação é
  recusada ou acontece após decifrar, nunca por leitura direta do que está guardado.

A prova é um teste que lê o armazenamento sem o segredo e falha se encontrar qualquer trecho do
texto submetido, previsto em testes/privacidade/armazenamento-opaco e ainda inexistente.

## O que não exige

Não exige proteger o texto sanitizado nem as anotações, que a decisão 0003 mantém em claro de
propósito. Não exige que a proteção do sistema de arquivos seja a única camada — ela é a segunda, e
nunca basta sozinha.
