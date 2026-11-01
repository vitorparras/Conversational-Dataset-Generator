# Material do canal

> Operacional. 2026-11-01.

- Projeto/pacote: `Core.Platform.Tls`
- Piso: 90 %
- Crítica: sim
- Motivo: Lê a chave privada do canal cifrado e recusa permissão aberta, chave fora do par e
  certificado fora da validade (decisões 0029 e 0046)
