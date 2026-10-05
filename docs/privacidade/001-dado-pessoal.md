# 001 — Tratamento de dado pessoal

> Norma vigente. 2026-10-04.

O material que entra no pipeline foi produzido por pessoas que não consentiram com a anotação e que,
na maioria dos casos, não podem ser consultadas. A política não se apoia em consentimento; apoia-se
em necessidade. Cada elemento identificável do texto fica, ou não, pelo que a análise precisa dele —
e isso se decide uma vez, na ingestão, não a cada consulta.

Há três destinos, e nenhum outro.

**Removido** é o que sai do texto e não volta para ninguém: nem para quem anota, nem para quem
administra, nem sob autorização. É o destino do que identifica sem carregar relação analítica —
número de documento, endereço, telefone, endereço eletrônico, conta bancária, placa, identificador
de aparelho. E é o destino de credencial e segredo, sempre, mesmo quando pareçam identificador:
pseudonimizar uma senha a tornaria recuperável pelo mapa de reversão, o que faria do acervo um
arquivo de senhas com etiqueta de privacidade.

**Pseudonimizado** é o que vira identificador opaco, com a correspondência guardada no mapa de
reversão cifrado da [decisão 0003](../decisoes/0003-cifra-em-repouso.md). É o destino do que a
análise precisa **relacionar sem conhecer**: autoria de mensagem, alcunha, nome próprio, nome de
organização, identificador de conta ou de perfil.

**Preservado** é o que fica como está, porque é o objeto da análise. É o destino mais fácil de
errar, e tem documento próprio: [o que se preserva](002-o-que-se-preserva.md).

## O critério entre remover e pseudonimizar

A pergunta é o que se perde ao apagar. Se apagar o valor só tira a identidade, remove-se. Se apagar
também desmancha uma relação — a mesma pessoa aparecendo em dois pontos distantes, o fio de quem
respondeu a quem, a diferença entre três interlocutores e trinta —, pseudonimiza-se, porque a
relação é dado de análise e a identidade não é.

Menção que revela saúde, religião, convicção política, origem ou vida sexual segue o mesmo critério
do conteúdo em que aparece: se é o que está sendo analisado, fica; se é o que aponta a pessoa, sai.
Não há categoria com destino automático próprio, e isso é deliberado — categoria fixa faz quem
anota confiar numa proteção que o detector talvez não tenha aplicado naquele trecho.

## O pseudônimo é atribuído, nunca calculado

`PESSOA_1` não é função do nome que substitui. Se fosse — um resumo criptográfico dele, por exemplo
—, quem tivesse o texto pseudonimizado e uma lista de nomes prováveis reverteria por tentativa, sem
tocar no mapa, e o mapa deixaria de ser o único caminho de volta que a decisão 0003 estabelece.

O pseudônimo é estável dentro de um domínio e **não atravessa domínios**: a mesma pessoa em dois
domínios recebe dois pseudônimos. Pseudônimo comum seria uma ligação entre acervos que a
[decisão 0006](../decisoes/0006-dominios-coexistem.md) isola no banco — e, ao contrário do
isolamento do banco, essa ligação sobreviveria à exportação.

## Quem impõe

A aplicação. A ingestão não grava material sem ter atribuído destino a cada trecho detectado, e o
texto bruto que resta fica cifrado por força da decisão 0003. O banco não ajuda aqui: ele não sabe
distinguir nome próprio de substantivo comum.

Por isso a camada falha de um modo conhecido: **o detector não encontra tudo.** O que ele não marcou
passa para o texto sanitizado como se fosse conteúdo comum. Contra isso há duas defesas, e as duas
são posteriores — a revisão humana do lote antes da aprovação, e a auditoria de resíduo que bloqueia
a saída, em [distribuição](008-distribuicao.md). Nenhuma das duas desfaz o vazamento dentro do
acervo; as duas impedem que ele saia.
