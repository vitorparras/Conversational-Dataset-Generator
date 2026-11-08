# Chave-mestra

> Operacional. 2026-11-01.

- Projeto/pacote: `Core.Platform.Keys`
- Piso: 90 %
- Crítica: sim
- Motivo: Guarda a chave em memória, recusa subir sem ela e carrega os rótulos de derivação
  (decisões 0003, 0013 e 0014)
  O resumo com chave do conteúdo replicado (`KeyDerivation.Digest`, #82) é feito só da chave-mestra
  e de uma finalidade por nome, que nunca é o texto de um rótulo: não depende da época da cifra nem
  da versão do rótulo do domínio, de modo que a rotação de uma ou da outra não quebra a comparação
  com as ocorrências já gravadas (decisão 0049; teste do resumo estável na rotação)
