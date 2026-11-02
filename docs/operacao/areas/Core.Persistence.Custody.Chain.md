# Transação da cadeia de custódia

> Operacional. 2026-11-01.

- Projeto/pacote: `Core.Persistence.Custody.Chain`
- Piso: 90 %
- Crítica: sim
- Motivo: Acrescenta o evento à cadeia em transação serializável, com o digest do anterior e o
  próprio calculados pelo servidor (requisito 019); separada de `Core.Persistence.Custody` para
  que a mutação de cada uma rode só os testes dela dentro do teto da 017
