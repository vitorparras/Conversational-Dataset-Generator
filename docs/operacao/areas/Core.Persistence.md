# Persistência, demais pastas

> Operacional. 2026-11-01.

- Projeto/pacote: `Core.Persistence`
- Piso: 80 %
- Crítica: não
- Motivo: Absorve: `Connection`, `Recovery`; conexão com credencial da aplicação, sem consulta de
  domínio; o ensaio da recuperação e a porta da ingestão (decisão 0014) leem a cadeia e abrem
  amostras em memória, sem gravar conteúdo
