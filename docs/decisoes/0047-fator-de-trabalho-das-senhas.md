# 0047 — O fator de trabalho das senhas: piso e teto, com a exceção da demonstração

> Decisão vigente. 2026-11-01, decisão do dono na issue #494.

## Contexto

A [0029](0029-identidade-e-sessao.md) manda guardar a senha por função de derivação própria para
senha, e a issue #69 fixou a função: PBKDF2-HMAC-SHA256, com sal de 16 bytes por derivação e 32
bytes derivados. O fator de trabalho, o número de iterações, é declarado por cada instalação em
`Passwords:WorkFactor`, sem valor padrão. Aceitar qualquer número positivo deixava uma instalação
real subir com 1 iteração, o que torna a derivação inútil contra quem leva a tabela, e deixava
uma entrada ficar lenta a ponto de um pedido derrubar o servidor.

## Decisão

**O fator de trabalho tem piso e teto, e o piso depende do tipo de instalação.**

- **Instalação real** (`Installation:Kind=real`): de **600.000 a 10.000.000** iterações, os dois
  extremos incluídos. O piso é a recomendação da OWASP para PBKDF2-HMAC-SHA256. O teto evita uma
  entrada tão lenta que sirva de negação de serviço.
- **Instalação de demonstração** (`Installation:Kind=demonstracao`, issue #53),
  que é declarada e não recebe material real: de **1 a 10.000.000**. Só o teto vale como na real.
  A regra lê a distinção que a configuração já tem; não há desvio de teste no código.
- A partida recusa um valor fora do intervalo, nomeando a chave, o valor encontrado e o limite
  cruzado (e, no piso, que é o da instalação real). Vale também no modo `migrate`, quando a chave
  é dada; sem tipo declarado, o `migrate` segura o valor ao piso da real, o mais estrito.
- Os três números (`600000`, `1` e `10000000`) vivem num lugar só no código, em
  `PasswordConfiguration`. A derivação em si (`PasswordDerivation`) segue aceitando qualquer
  fator positivo: o limite é da configuração da instalação.
- Subir o piso no futuro é decisão nova. Uma senha já guardada com fator menor é recalculada no
  próximo acesso, como diz a 0029.

## Alternativas recusadas

- **Piso único para todas as instalações.** Obrigaria a demonstração e os testes que só exercitam a
  derivação a pagar 600.000 iterações a cada senha, sem proteger material algum.
- **Desvio de teste no código** (um ajuste que baixa o piso quando roda sob teste). Seria um
  caminho que a produção também carrega; a distinção real ou demonstração já existe e é declarada.
- **Sem teto.** Um valor enorme digitado por engano, ou de propósito, trava a entrada de todos.
- **Piso por consulta a um parâmetro do banco.** O fator é da instalação, não dos dados.

## Consequências

Quem instala uma instalação real precisa declarar pelo menos 600.000, e a entrada leva o tempo
que isso custa. Uma configuração de instalação real com valor baixo, que antes subia, passa a
ser recusada na partida. A demonstração segue com fator baixo. A [operação 023](../operacao/023-iniciar-o-nucleo-publicado.md)
diz o intervalo a quem opera.
