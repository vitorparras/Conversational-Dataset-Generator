# Serviço de domínio

> Norma vigente. 2026-11-15.

Peça que decide uma regra do negócio. Não abre conexão, não lê relógio nem configuração por conta
própria e não conhece HTTP.

## Exemplar

`core/src/Core.Ingestion/Deciding/DestinationPolicy.cs`, e para o caso com dependência
`core/src/Core.Ingestion/Entrance/SubmissionGate.cs`.

- `DestinationPolicy` é estática e pura: uma entrada, um destino, um ramo por regra, e o que ela não
  reconhece cai no lado seguro (`Destination.Remove`, linhas 16 a 38).
- `SubmissionGate` recebe pelo construtor tudo de que depende (leitores, limite, gerador de
  identificador; linhas 22 a 39), valida os argumentos na entrada e devolve `SubmissionResult`, com
  a recusa e o motivo como valor (linha 60).
- O comentário de classe diz a regra e cita a decisão que a autoriza (`DestinationPolicy`, linhas 5
  a 9, decisão 0018). `SubmissionGate` não cita decisão e não é exemplar nesse ponto.

## Princípio

Inversão de dependência e responsabilidade única: a regra fica num lugar que se testa sem banco, sem
host e sem rede, e o armazenamento e a rota só a chamam. Propriedade e invariante se testam aqui
(`DestinationPolicyPropertiesTests`, `SubmissionGateInvariantsTests`).

## Proibido

- A mesma regra em dois lugares: decidida no serviço e de novo no armazenamento ou na rota.
- Recusa esperada como exceção; exceção é defeito
  ([regras de código](../../regras-de-codigo.md), 5).
- Ler o relógio, a configuração ou o ambiente dentro do serviço; o que muda entra como argumento ou
  pelo construtor.
- Construtor `internal` com gerador de valor fora o caso do exemplar (linha 31), em que o teste
  precisa saber qual identificador saiu; ele não é gancho para trocar comportamento.
