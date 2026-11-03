# Cifra do conteúdo protegido

> Operacional. 2026-11-03.

- Projeto/pacote: `Core.Persistence.Content.Sealing`
- Piso: 90 %
- Crítica: sim
- Motivo: É o caminho único de cifra do conteúdo protegido: sela o texto sob o rótulo da finalidade
  e do domínio da linha e só o abre se o envelope carrega esse mesmo rótulo; o banco nunca guarda
  nem devolve texto protegido por outro caminho; separada de `Core.Persistence.Content` para que a
  mutação de cada uma fique em até 12 min (#557), dentro do teto da 017. Sem projeto de teste
  próprio: roda os testes de `Core.Persistence.Content.Tests`
