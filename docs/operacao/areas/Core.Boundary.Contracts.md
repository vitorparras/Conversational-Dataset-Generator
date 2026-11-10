# Contrato da fronteira

> Operacional. 2026-11-01.

- Projeto/pacote: `Core.Boundary.Contracts`
- Piso: 90 %
- Crítica: sim
- Motivo: A forma JSON recusa campo desconhecido ou nulo no texto que cruza o processo (decisão
  0018)
- Vocabulário do tipo da ocorrência: lista fechada em `OccurrenceKinds`, escrita exatamente assim:
  `PERSON`, `LOCATION`, `ORGANIZATION`, `CREDENTIAL`, `SECRET`, `PASSWORD`, `TOKEN`, `API_KEY` e
  `PRIVATE_KEY`. A resposta com tipo fora dela é recusada inteira, como resposta fora do contrato
