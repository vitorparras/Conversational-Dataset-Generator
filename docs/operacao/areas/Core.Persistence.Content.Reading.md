# Leitura do conteúdo protegido

> Operacional. 2026-11-03.

- Projeto/pacote: `Core.Persistence.Content.Reading`
- Piso: 90 %
- Crítica: sim
- Motivo: Lê o conteúdo protegido só depois de a cadeia gravar quem leu qual item e em qual passo;
  leitura que a cadeia não grava não devolve texto (requisito 019); separada de
  `Core.Persistence.Content` para que a mutação de cada uma fique em até 12 min (#557), dentro do
  teto da 017. Sem projeto de teste próprio: roda os testes de `Core.Persistence.Content.Tests`
