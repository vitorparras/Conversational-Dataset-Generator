# 007 — Verificação periódica da custódia

> Operacional. 2026-10-04.

Verificação que existe e nunca roda não é garantia. Quando a conferência acontece está na
[política de verificação](../privacidade/005-verificacao-da-custodia.md) — na subida do sistema,
periodicamente com piso diário, antes de toda exportação e sob demanda, e só a exportação bloqueia.
O que a conferência precisa detectar, e que o resultado da última fique registrado e consultável, é
critério do requisito de
[integridade verificável](../requisitos/020-integridade-verificavel.md). Esta operação responde pelo
que as duas delegam: quem roda, com que credencial, onde fica o resultado, e o que mais se confere
além da cadeia.

## O que se confere além do que a política recalcula

| O quê | Com que frequência |
|---|---|
| Garantias do servidor: política de linha, permissão de coluna, gatilho | A cada implantação, e mensalmente |
| Papel da aplicação não é dono de tabela nem superusuário | A cada implantação |
| Amostra de registros cifrados que ainda decifram sob a versão declarada | Mensalmente |
| Guarda da chave, e separação dela da credencial de dono | Trimestralmente, e a cada troca de custodiante |
| Restauração de uma cópia em ambiente separado | Trimestralmente |

As duas primeiras linhas cobrem o ponto cego da cadeia: ela detecta alteração nos eventos, não a
remoção da garantia que impedia a alteração. Garantia desativada não quebra elo nenhum.

A amostra de registros cifrados é amostra de propósito: decifrar tudo expõe dado pessoal sem
necessidade, e a leitura em si é acesso registrado.

## Por quem, e com qual credencial

A verificação diária roda sem pessoa, com a credencial da aplicação — que não é dona de tabela e,
por isso, não teria como consertar o que encontrasse. A leitura do resultado é da administração
técnica; a revisão trimestral, de quem responde pelo acervo; a parte de chave, dos custodiantes.

Quem verifica não deveria ser a única pessoa capaz de reescrever o que verifica. Onde a instalação
não comportar essa separação, ela fica declarada como limitação conhecida — o problema não é a
separação ausente, é a separação ausente e não sabida.

## O que se faz com o resultado

Que o resultado fique registrado é garantia do sistema, não disciplina de quem opera. O que esta
operação acrescenta é quem olha: a revisão procura **o resultado**, não a falha — ausência de
resultado recente é falha, e é o modo mais comum de uma verificação deixar de existir sem que
ninguém tenha decidido isso. Inclusive "passou" se lê, porque sem isso "nunca rodou" e "rodou e
estava íntegro" ficam iguais para quem revisa.

Falha de cadeia ou de topo vai para [009](009-incidente-cadeia-quebrada.md). Garantia do servidor
ausente, acesso ao cofre sem operação correspondente, ou cópia de chave fora do lugar vão para
[008](008-incidente-vazamento.md).

## Antes de concluir adulteração

Rode a verificação sobre uma sequência sintética de resposta conhecida. Erro no verificador tem a
mesma aparência de cadeia alterada, e a distância entre as duas conclusões é grande demais para se
apoiar numa execução só.

## O selo na instalação com selo manual

Na instalação que declarou o selo `Manual`, ninguém envia o selo por ela: quem opera o guarda fora
do alcance da base. O fluxo é este, com a sessão de quem opera:

1. `GET /custody/seal` emite o selo do topo atual, em texto, para imprimir ou copiar. Não grava
   nada. Guarde o texto onde quem controla a base não alcance.
2. `POST /custody/seal/confirmation`, com `{"seal": "<texto do selo>"}`, grava o evento do selo
   na cadeia, em nome da pessoa da sessão, só se a cadeia tiver aquele evento com o resumo que o
   selo traz. Selo alterado, ou de uma sequência que a cadeia não tem, recebe 409 com o motivo e
   nada é gravado; texto que não é selo recebe 400. Corpo acima de 1 KiB recebe 413.
3. A verificação (`GET` e `POST /custody/verification`) traz o aviso `warning` quando nenhum selo
   foi registrado ou quando o último tem mais de 7 dias. O `/status` traz só esse aviso, em
   `sealWarning`, atualizado a cada verificação periódica e logo após cada confirmação; a data do
   selo e o selo ficam atrás da sessão. Repita o fluxo a cada semana: o selo antigo
   cobre a cadeia só até a data dele, e o que entrou depois vale pela coerência dos elos, não pela
   integridade.

Nas instalações `External` e `Internal` essas duas rotas respondem 409: o núcleo envia o selo
sozinho, uma vez por dia, e um selo gravado à mão diria que o destino guarda o que não guarda.
