# Giro da época das chaves do conteúdo

> Operacional. 2026-11-03.

- Projeto/pacote: `Core.Persistence.Content.Rotation`
- Piso: 90 %
- Crítica: sim
- Partes da mutação: 3
- Motivo: Reembrulha, numa só transação e por domínio, as chaves de item que uma época antiga
  embrulha (decisão 0049), e só entra se a chave sai de uma época carregada; separada de
  `Core.Persistence.Content` para que a mutação de cada uma fique abaixo do teto de 20 min da 017
  (#915: inteira, levou 1.183 s dos 1.200 s, e a suíte de `Core.Persistence.Content.Tests` que
  cada mutante roda ainda cresce; por isso 3 partes). Sem projeto de teste próprio: roda os testes
  de `Core.Persistence.Content.Tests`
