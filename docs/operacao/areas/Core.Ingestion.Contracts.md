# Contrato da ingestão

> Operacional. 2026-11-17.

- Projeto/pacote: `Core.Ingestion.Contracts`
- Piso: 90 %
- Crítica: sim
- Motivo: Absorve: `Dating`, `Reversal`; decide o deslocamento de datas de cada domínio,
  sorteado uma vez e nunca sorteado de novo sobre a linha perdida (decisão 0020). Julga o pedido
  de reversão e a autorização dele, sem leitura nem escrita, antes do banco (issues 103 e 104).
  Decide pelo estado do lote o que a revisão por amostra recusa, uma só vez, para o banco e para
  o domínio, sem leitura nem escrita (decisão 0055); um erro aqui deixaria a recusa do banco e a
  do domínio diferirem
