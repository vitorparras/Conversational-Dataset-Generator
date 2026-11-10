# Chave-mestra

> Operacional. 2026-11-01.

- Projeto/pacote: `Core.Platform.Keys`
- Piso: 90 %
- Crítica: sim
- Motivo: Guarda a chave em memória, recusa subir sem ela e carrega os rótulos de derivação
  (decisões 0003, 0013 e 0014)
  O resumo com chave do conteúdo replicado (`KeyDerivation.Digest`, #82) usa só a chave-mestra
  e uma finalidade por nome, nunca o texto de um rótulo: não depende da época da cifra nem da
  versão do rótulo, de modo que a rotação de uma ou da outra não quebra a comparação com as
  ocorrências já gravadas (decisão 0049)
