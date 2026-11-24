# Escrita do conteúdo

> Operacional. 2026-11-03.

- Projeto/pacote: `Core.Persistence.Content.Writing`
- Piso: 90 %
- Crítica: sim
- Partes da mutação: 12
- Motivo: Acrescenta item e mapa de reversão ao banco, selando o que vai a coluna protegida para o
  domínio da linha, de modo que o banco nunca guarda o texto protegido em claro; separada de
  `Core.Persistence.Content` para que a mutação de cada uma fique em até 12 min (#557), dentro do
  teto da 017 (#801: a escrita com 3 partes e a cifra inteira passaram dos 20 min, com 24 e 28
  mutantes de 70 a 95 s, a suíte inteira; #846: com a reexecução, a parte 5 de 6 levou 20,6
  min; #101: com o pseudonimizado, duas partes passaram dos 20 min com 7 e com 12, porque cada
  mutante da escrita espera a classe do armazenamento opaco, que ficou de 52 para 68 s e batia no
  timeout por mutante; a adulteração ganhou classes próprias, que correm ao lado das outras, e os
  quatro textos opcionais do item passaram por uma só condição; com 12, o maior peso de uma parte
  cai de 344 para 251).
  Sem projeto de teste próprio: roda os testes de `Core.Persistence.Content.Tests`
