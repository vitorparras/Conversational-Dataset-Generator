# Capturas da #594

O esqueleto das telas: a página inicial com menu e sem menu, e a página de um endereço que não leva a
lugar nenhum (a mesma para a rota negada e a inexistente), em claro, escuro e na largura de celular
(390 px). Geradas com a variável SCREEN_CAPTURES nos testes de navegador `NavigationScreenTests`
(Core.Host.Tests.Browser), na ponta de feat/594-navegacao (9e854b9d). Nomes e senhas são sintéticos
(`zqx-*`). Branch fora do main.

| Arquivo | Estado |
|---|---|
| inicio-com-menu-{claro,escuro,celular}.png | pessoa de Administração: menu em faixa própria e destinos |
| inicio-sem-menu-{claro,escuro,celular}.png | pessoa sem função alcançável: estado vazio, sem menu nem seletor |
| pagina-nao-encontrada-{claro,escuro,celular}.png | rota que a função não alcança, igual à inexistente |
