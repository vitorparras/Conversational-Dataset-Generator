# 0003 — Cifra em repouso

> Decisão vigente. 2026-10-04.

## Decisão

A cifra é feita **pela aplicação, antes de gravar**. A chave nunca chega ao banco.

| Dado | Tratamento |
|---|---|
| Mapa de reversão da pseudonimização | Cifrado, com chave e finalidade próprias |
| Texto bruto, antes da sanitização | Cifrado |
| Texto sanitizado | Em claro no banco |
| Anotações, métricas, registro de auditoria | Em claro no banco |

O volume do banco também é cifrado no sistema de arquivos, como segunda camada — nunca como a
única.

## Por quê

O critério é um só: **copiar o arquivo do banco para fora não pode expor nada**. É o cenário real
de vazamento — cópia de backup, disco descartado, acesso indevido ao servidor.

Com cifra feita pela aplicação, a chave vive no cofre de chaves do processo, derivada por finalidade
declarada. Quem obtiver o banco inteiro obtém bytes. Quem obtiver o servidor do banco também.

O texto sanitizado fica em claro porque é o material que as pessoas leem e filtram durante a
anotação, e cifrá-lo inviabilizaria busca e ordenação sem ganho correspondente: ele já passou pela
pseudonimização, e o que o torna sensível — a ligação com a identidade real — está no mapa de
reversão, que é cifrado.

Essa escolha concentra o risco num único lugar, e é deliberado: **é mais fácil proteger bem um
segredo do que razoavelmente dez.**

## Recusados

| Opção | Por quê |
|---|---|
| Cifra de coluna no banco | A chave fica no servidor e trafega na própria consulta, podendo aparecer em log. Quem comprometer o servidor leva dado e chave juntos |
| Só cifra de volume | Um despejo do banco sai em claro. Protege contra disco roubado, não contra cópia de backup |
| Cifrar tudo, inclusive texto sanitizado | Inviabiliza busca e ordenação na tela de anotação, que é onde o trabalho humano acontece |
| Cifra no banco gerenciada por serviço externo | Acrescenta dependência de infraestrutura a um sistema que precisa rodar em instalação local |

## Consequências

O conteúdo cifrado não pode ser buscado nem indexado pelo banco. Qualquer filtro sobre texto bruto
precisa acontecer depois de decifrar, na aplicação.

Perder a chave significa perder o texto bruto e a capacidade de reverter a pseudonimização, em
definitivo. A custódia da chave passa a ser parte da custódia do acervo, e o procedimento de guarda
e recuperação precisa estar escrito antes de haver dado real.

Decifrar o mapa de reversão é operação auditada: revela identidade, e é o único caminho de volta.

## O que reabre

Se a busca sobre texto bruto passar a ser necessária em operação, e não apenas na aplicação — aí
entra cifra que preserva ordenação ou busca, com a perda de segurança que ela implica, decidida por
escrito.

Se o sistema passar a rodar em infraestrutura gerenciada com serviço de chaves próprio, delegar a
guarda da chave a esse serviço passa a valer a dependência.
