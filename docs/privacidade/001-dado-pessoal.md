# 001 — Tratamento de dado pessoal

> Norma vigente. 2026-10-04.

O material foi produzido por pessoas que não consentiram com a anotação e que, na maioria dos casos,
não podem ser consultadas. A política apoia-se em necessidade, não em consentimento: cada elemento
identificável fica, ou não, pelo que a análise precisa dele — e isso se decide uma vez, na entrada,
sobre o texto como chegou. As etapas seguintes **aplicam** o que foi decidido: a sanitização remove
o que foi marcado para sair, a pseudonimização substitui o que vira marcador. Nenhuma decide de
novo.

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

Menção que revela saúde, religião, convicção política, origem ou vida sexual segue o mesmo critério:
se é o que está sendo analisado, fica; se aponta a pessoa, sai. Não há categoria com destino
automático, e isso é deliberado — categoria fixa faz quem anota confiar numa proteção que o detector
talvez não tenha aplicado ali.

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
que resta fica cifrado. O banco não ajuda: ele não distingue nome próprio de substantivo comum.

Por isso a camada falha de um modo conhecido: **o detector não encontra tudo**, e o que ele não
marcou segue adiante como conteúdo comum. Há três defesas: a revisão humana do lote, a auditoria de
resíduo que bloqueia a saída, e o relato de quem anota — a única que alcança o resíduo **dentro** do
acervo ([0026](../decisoes/0026-relato-de-dado-pessoal.md)).
