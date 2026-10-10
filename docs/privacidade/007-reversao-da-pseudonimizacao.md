# 007 — Reversão da pseudonimização

> Norma vigente. 2026-10-04.

Existe um caminho de volta à identidade, e é um só: decifrar o mapa de reversão, que a
[decisão 0003](../decisoes/0003-cifra-em-repouso.md) mantém cifrado com chave e finalidade próprias.
Não há atalho, porque o pseudônimo não deriva do nome — ver [dado pessoal](001-dado-pessoal.md).

É ato, não permissão. Nenhuma função do [acesso](006-acesso.md) inclui reversão.

## Quem pede, e o que precisa sustentar

Pede quem responde pelo acervo do domínio. Quem anota não pede, porque anotar não exige identidade:
se exigisse, a pseudonimização estaria errada antes da reversão.

O pedido é escrito antes do ato e diz quatro coisas: qual pseudônimo, em qual item; por que a
análise ou o processo que a motivou não avança sem a identidade; o que será feito com ela; e por
quanto tempo. Pedido que não distingue "preciso saber quem é" de "seria útil saber quem é" não
passa — essa é a distinção que o texto escrito força e a conversa dispensa.

## Pedir não é autorizar

Autoriza outra pessoa, que não a que pediu. A aplicação recusa pedido em que as duas coincidem.

Vale ser honesto sobre o alcance desse controle: quem tem a chave decifra o mapa sem passar pela
aplicação. A segunda pessoa é mecanismo enquanto a chave estiver fora do alcance de quem pede, e
vira formalidade no instante em que as duas coisas se juntarem na mesma pessoa.

Transformá-la em mecanismo de verdade exigiria dois segredos para reconstituir a chave, em mãos
separadas — e a decisão [0014](../decisoes/0014-custodia-da-chave-mestra.md) recusou isso: quórum
protege contra custódia desonesta e cria indisponibilidade a cada ausência, que não é o risco deste
acervo. Então o controle **é mesmo de processo, por escolha registrada**, e o que o sustenta é a
separação de pessoas da guarda da chave, descrita em operação. Juntar guarda e
pedido na mesma pessoa não quebra regra técnica alguma: por isso está escrito aqui que não se faz.

## Um por vez

Reversão é de pseudônimo nomeado, em escopo declarado. Pedido que alcançaria o domínio inteiro, ou
conjunto grande sem critério, é recusado: isso não é consulta, é reidentificação do acervo, e
nenhuma finalidade legítima precisa dela de uma vez.

A autorização vale para aquele pedido e expira com ele. O sistema não guarda a identidade revelada
em tabela nova: entrega a quem pediu e segue sem ela.

## O que fica, e o que nunca fica

Fica na cadeia de eventos, acrescentável como o resto: quem pediu, quem autorizou, a justificativa
escrita, quais pseudônimos foram alcançados, quando, e se o pedido foi recusado. Recusa também é
evento, e a série de recusas de alguém é informação.

**Não fica a identidade revelada.** Registro de auditoria que guarda o valor revelado é uma segunda
cópia do mapa, em claro, numa tabela que gente lê por função. O registro diz que `PESSOA_1` foi
revelada; não diz quem `PESSOA_1` é.

Se a cadeia desses eventos quebrar, trata-se como qualquer quebra de custódia, em
[verificação](005-verificacao-da-custodia.md) — com uma diferença: enquanto a classificação não
sair, nenhuma reversão nova é autorizada naquele domínio.
