# Transação da cadeia de custódia

> Operacional. 2026-11-02.

- Projeto/pacote: `Core.Persistence.Custody.Chain`
- Piso: 90 %
- Crítica: sim
- Motivo: Abre a transação serializável em que o evento entra na cadeia, com o digest do anterior e
  o próprio calculados pelo servidor (requisito 019), e traduz as recusas do servidor; separada de
  `Core.Persistence.Custody` para que a mutação de cada uma rode só os testes dela dentro do teto
  da 017. A inclusão do evento na transação é a área `Core.Persistence.Custody.Chain.Append`, e os
  parâmetros dele, a área `Core.Persistence.Custody.Chain.Parameters`
