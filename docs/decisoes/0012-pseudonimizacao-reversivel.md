# 0012 — Pseudonimização reversível, não anonimização

> Decisão vigente. 2026-10-04.

## Contexto

As decisões 0002 e 0003 tratam o mapa de reversão como coisa existente: uma escolhe a plataforma por
causa dele, a outra decide como cifrá-lo. Nenhuma decide que ele deva existir. A escolha entre
substituir identidade por marca reversível e apagar identidade sem volta é anterior às duas, e muda
o que o dataset é.

## Decisão

**A identidade é substituída por marca estável e reversível, não apagada.** Cada pessoa, lugar,
organização ou identificador reconhecido recebe uma marca que se repete sempre que a mesma entidade
reaparece, e o mapa entre marca e valor original é guardado cifrado, com chave e finalidade
próprias. Reverter é operação autorizada e auditada, item por item, nunca um modo do sistema.

A razão é que anonimização destrói o que faz o material analisável. Se duas menções à mesma pessoa
em mensagens distintas viram marcas diferentes, perde-se o fio que atravessa a conversa — e é
justamente esse fio que a anotação examina. Se viram a mesma marca sem mapa nenhum, perde-se a
capacidade de conferir que a substituição acertou o alvo: ninguém verifica uma anonimização que não
se pode desfazer.

Há o reverso. **Nem tudo é reversível**: trecho que não é identidade nem contexto — número de
documento, credencial, dado de pagamento — é removido sem mapa, porque guardar o original seria
guardar risco sem ganho de análise.

## Recusados

| Opção | Por quê |
|---|---|
| Anonimização sem mapa, marcas distintas | Quebra a correferência, que é o que torna a conversa analisável |
| Anonimização sem mapa, marcas estáveis | Impede conferir se a substituição acertou o alvo, e erro de alvo fica indetectável |
| Guardar o original em claro ao lado da marca | É manter o acervo identificado com um passo a mais; a cifra da decisão 0003 perde objeto |
| Reversão como modo de operação, e não por item | Transforma autorização em interruptor, e a auditoria deixa de dizer o que foi revelado |

## Consequências

O dataset é de dado pseudonimizado, **não anônimo**. Enquanto o mapa existir, o material continua
sendo dado pessoal, e a licença dos dados da decisão 0009 não pode prometer redistribuição
irrestrita de algo que ainda se liga a pessoas.

Existe um caminho de volta, e isso é um risco que anonimização não tem. O mapa é o ponto único de
falha do sistema: quem o obtiver decifrado reidentifica o acervo inteiro. É o que justifica a chave
e a finalidade separadas, e a auditoria de cada reversão.

Marca estável carrega informação. A frequência com que uma marca aparece, e ao lado de quais outras,
diz coisas sobre a pessoa mesmo sem o nome — o dataset exportado não deixa de ter esse resíduo por
ter substituído nomes.

## O que reabre

Se um recorte precisar ser publicado como anônimo de fato, ele deixa de poder sair deste pipeline:
sai de uma passagem adicional que descarta o mapa e aceita a perda de correferência, por decisão
própria.
