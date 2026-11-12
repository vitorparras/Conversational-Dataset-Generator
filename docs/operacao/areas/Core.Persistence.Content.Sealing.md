# Cifra do conteúdo protegido

> Operacional. 2026-11-03.

- Projeto/pacote: `Core.Persistence.Content.Sealing`
- Piso: 90 %
- Crítica: sim
- Partes da mutação: 3
- Motivo: É o caminho único de cifra do conteúdo protegido: sela o texto sob o rótulo da finalidade
  e do domínio da linha e só o abre se o envelope carrega esse mesmo rótulo; o banco nunca guarda
  nem devolve texto protegido por outro caminho; separada de `Core.Persistence.Content` para que a
  mutação de cada uma fique em até 12 min (#557), dentro do teto da 017 (#801: inteira, com 28
  mutantes de 70 a 95 s, a suíte inteira, passou dos 20 min; por isso 3 partes). Sem projeto de
  teste
  próprio: roda os testes de `Core.Persistence.Content.Tests`
