# Capturas da #603 e da #604

A configuração em vigor (`/configuracao`) e o estado do sistema (`/estado-do-sistema`), em claro,
escuro, transparência reduzida, contraste alto e na largura de celular (390 px). Geradas com a
variável SCREEN_CAPTURES nos testes de navegador `ConfigurationScreenTests` e
`SystemStateScreenTests` (Core.Host.Tests.Browser), na ponta de feat/603-604-configuracao-e-estado
(60901922). Nomes, senha e a chave de segredo são sintéticos (`zqx-*`); o valor do segredo não
aparece em nenhuma das imagens. Branch fora do main.

| Arquivo | Estado |
|---|---|
| configuracao-{claro,escuro,transparencia-reduzida,contraste-alto,celular}.png | as cinco chaves do host de ensaio, a do segredo só com nome, "Segredo, valor oculto" e a origem |
| estado-do-sistema-{claro,escuro,transparencia-reduzida,contraste-alto,celular}.png | uma conferência que passou e uma que falhou, apto "Não", versões não obtidas ditas como tal |
