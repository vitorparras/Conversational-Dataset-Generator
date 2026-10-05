# 007 — A reversão é restrita e auditada

> Norma vigente. 2026-10-04.

**Voltar de um rótulo à identidade só acontece sob autorização para uma finalidade declarada, deixa
registro que não se apaga, e é o único caminho de volta que o sistema oferece.**

## Por que existe

A [decisão 0003](../decisoes/0003-cifra-em-repouso.md) guarda o mapa de reversão com chave e
finalidade próprias, e diz que decifrá-lo é operação auditada porque revela identidade. Um sistema
que pode reidentificar sem deixar rastro não tem como demonstrar que não reidentificou — e essa
demonstração é a única coisa que sustenta a promessa feita a quem cedeu o material.

Registrar antes de revelar, e não depois, é o que impede que a operação aconteça e o registro falhe
em seguida.

## Critérios de aceitação

- Dado um pedido de reversão sem autorização para a finalidade declarada, quando ele é submetido,
  então é negado, e a negativa fica registrada com quem pediu, quando e sobre qual escopo.
- Dado um pedido autorizado, quando ele é atendido, então o registro de quem, quando, qual escopo e
  com que finalidade está gravado antes de a identidade ser revelada.
- Dado o registro de uma reversão, quando alguém tenta removê-lo ou alterá-lo, então a operação
  falha e a tentativa é detectável, conforme [019](019-custodia-encadeada.md).
- Dado o acervo e nenhum mapa de reversão, quando se tenta reidentificar por outra via, então
  nenhuma outra via existe no sistema.

A prova é um teste que percorre as operações de leitura disponíveis e falha se alguma devolver
identidade sem passar pela reversão autorizada, previsto em testes/privacidade/reversao-auditada e
ainda inexistente.

## O que não exige

Não exige que a reversão seja sempre possível: perder o segredo a torna definitivamente impossível,
e a decisão 0003 aceita esse preço. Não exige, por si, autorização por mais de uma pessoa — mas
a [política de reversão](../privacidade/007-reversao-da-pseudonimizacao.md) exige, e essa escolha
é dela: esta garantia continua valendo se a política mudar.
