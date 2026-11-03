# Esvaziamento do conteúdo

> Operacional. 2026-11-03.

- Projeto/pacote: `Core.Persistence.Content.Emptying`
- Piso: 90 %
- Crítica: sim
- Motivo: Esvazia o item e os mapas dele numa transação serializável em que a cadeia grava o evento
  do esvaziamento ou a recusa (requisito 019), e só limpa o conteúdo se o evento entrou; separada de
  `Core.Persistence.Content` para que a mutação de cada uma fique abaixo do teto da 017. Sem projeto
  de teste próprio: roda os testes de `Core.Persistence.Content.Tests`
