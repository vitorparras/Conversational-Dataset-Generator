# Contrato da ingestão

> Operacional. 2026-11-17.

- Projeto/pacote: `Core.Ingestion.Contracts`
- Piso: 90 %
- Crítica: sim
- Motivo: Decide pelo estado do lote o que a revisão por amostra recusa, uma só vez, para o
  banco e para o domínio, sem leitura nem escrita (decisão 0055); um erro aqui deixaria a recusa
  do banco e a do domínio diferirem
