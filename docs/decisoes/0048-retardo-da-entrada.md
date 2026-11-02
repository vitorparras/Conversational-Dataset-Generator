# 0048 — Retardo da entrada

> Decisão vigente. 2026-11-01.

## Contexto

A [0029](0029-identidade-e-sessao.md) manda retardar a tentativa repetida de entrada, mas só diz
que o retardo cresce com as tentativas. Faltava a curva, o que conta como "a mesma tentativa", de
onde a tentativa vem, o que acontece com um nome que não existe e onde os contadores moram. A
[0035](0035-reposicao-encerra-sessoes.md) já trata a sessão como estado de memória do processo.

## Decisão

**A curva é fixa no código, sem configuração.** A primeira recusa sai no tempo normal. Da segunda
em diante a resposta espera 1 s, e o retardo dobra a cada recusa (1, 2, 4, 8, 16 s) até o teto de
30 s por tentativa. O contador zera numa entrada aceita ou depois de 15 min sem recusa. Os números
vivem num lugar só do código.

**O retardo é o maior de dois contadores**: o do nome digitado e o da origem. O nome entra como
HMAC-SHA256 com chave aleatória gerada na partida do processo, só em memória, de modo que nada
derivado do que se digitou chega ao banco ou ao log. A origem é o prefixo da rede do endereço da
conexão, /24 no IPv4 e /48 no IPv6 (o IPv4 mapeado em IPv6 conta como IPv4), nunca o endereço
inteiro. Uma entrada aceita zera os dois contadores daquela tentativa.

**Nome que ninguém usa é retardado como senha errada**: o contador é pelo que foi digitado, não
pela conta, e o texto da recusa não muda. O tempo de verificação já é o mesmo nos dois casos.

**A série de recusas por origem é uma consulta**, não um evento novo; ela entra com o registro da
entrada, em outra mudança. **Os contadores ficam em memória** e zeram no reinício, que é ato de
quem opera o servidor, não de quem tenta entrar.

**O retardo atrasa a própria resposta**, com no máximo 4 respostas retardadas ao mesmo tempo por
prefixo de origem e 64 no servidor (este só protege o recurso). Em qualquer limite cheio a entrada é
recusada **antes de verificar a senha**, com o texto de toda recusa e o contador subindo, também
para a senha certa: aceitá-la daria a quem satura o limite um oráculo. Cada tabela guarda no máximo
10000 contadores e esquece o que passou de 15 min. Com a tabela cheia, um nome ou uma origem novos
não ganham contador: o nome fica sob o da origem, e uma origem nova, com a tabela de origens cheia,
fica sem retardo até que algo seja esquecido; encher a tabela custa 10000 redes em 15 min.

## Alternativas recusadas

- **Curva configurável**: um operador poderia zerá-la sem rastro; a política é do produto.
- **Endereço inteiro como origem**: identifica um dispositivo; o prefixo basta para ver uma série.
- **Contador só pela conta**: o nome inexistente escaparia do retardo e se revelaria como tal.
- **Contador no banco**: gravaria algo derivado do que foi digitado, e uma escrita por tentativa.

## Consequências

Atrás de um proxy todos os clientes partilham a origem do proxy, e o contador da origem passa a
somar todos; esta versão não lê cabeçalho de encaminhamento, que quem ataca poderia forjar. Entradas
em paralelo antes da primeira recusa terminar ainda não sentem o retardo; o custo da senha limita
quantas cabem. O reinício zera os contadores. **Resíduo**: quem ocupa o limite do servidor, a partir
de 16 prefixos ao menos, nega a entrada a todos enquanto mantém as tentativas, e quem está atrás de
uma mesma rede se atrapalha entre si. A negação dura só enquanto as esperas duram (30 s no máximo),
nunca é permanente e nunca diz nada sobre a senha ou sobre a conta.

## O que reabre esta decisão

Um proxy confiável na frente do núcleo obriga a decidir de onde vem a origem. Exigência de reter o
contador além do processo reabre os contadores em memória.
