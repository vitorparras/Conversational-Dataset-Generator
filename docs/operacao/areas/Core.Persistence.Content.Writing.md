# Escrita do conteúdo

> Operacional. 2026-11-03.

- Projeto/pacote: `Core.Persistence.Content.Writing`
- Piso: 90 %
- Crítica: sim
- Partes da mutação: 6
- Motivo: Acrescenta item e mapa de reversão ao banco, selando o que vai a coluna protegida para o
  domínio da linha, de modo que o banco nunca guarda o texto protegido em claro; separada de
  `Core.Persistence.Content` para que a mutação de cada uma fique em até 12 min (#557), dentro do
  teto da 017 (#801: a escrita com 3 partes e a cifra inteira passaram dos 20 min, com 24 e 28
  mutantes de 70 a 95 s, a suíte inteira). Sem projeto de teste próprio: roda os testes de
  `Core.Persistence.Content.Tests`
