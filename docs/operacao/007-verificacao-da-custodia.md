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

## Onde fica o resultado

Na subida e a cada `Custody:VerificationInterval`, cada execução vira um evento da cadeia: passo
`verify_guarantees`, veredito e, no item, a máscara do que faltou. A rota `/custody/verification`,
com sessão aberta, mostra em `guarantees` quando rodou, o que conferiu e o que faltou; `overdue`
verdadeiro, sem resultado ou com o último mais velho que dois intervalos, é falha.

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
