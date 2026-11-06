# Capturas da #605

A tela da custódia (`/custodia`), com a cadeia íntegra e com a cadeia quebrada por fixture (o segundo
evento alterado fora do sistema), e a confirmação da verificação (`/custodia/verificar`), em claro,
escuro, transparência reduzida, contraste alto e na largura de celular (390 px). Geradas com a
variável SCREEN_CAPTURES nos testes de navegador `ChainScreenTests` e `ChainCheckTests`
(Core.Host.Tests.Browser), na ponta de feat/605-telas. Nomes e senha são sintéticos (`zqx-`).
Branch fora do main.

| Arquivo | Estado |
|---|---|
| custodia-integra-{claro,escuro,transparencia-reduzida,contraste-alto,celular}.png | cadeia íntegra, verificação do sistema, selo sem registro |
| custodia-quebrada-{claro,escuro,celular}.png | incidente aberto desde o evento nº 2 e o aviso de só leitura |
| custodia-confirmar-{claro,escuro,celular}.png | o que a verificação lê, grava e não muda, e o botão de pedir |
