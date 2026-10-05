# 007 — Módulo de pseudonimização

> Descrição. 2026-10-04.

Pseudonimizar aqui não é apagar. É trocar o que identifica uma pessoa por um rótulo que se mantém o
mesmo em todo o escopo, de modo que a conversa continue fazendo sentido depois.

Apagar seria mais seguro e inútil: um diálogo em que todos os nomes viraram a mesma marca não diz
quem respondeu a quem, e deixa de ser material analisável. A garantia tem duas metades que se
tensionam — a identidade não sobrevive, e a relação sim
([requisito 006](../requisitos/006-pseudonimizacao-preserva-contexto.md)). Ganho de uma à custa da
outra é defeito, não ajuste.

Consome o texto sanitizado e os trechos cujo destino é pseudonimizar. Produz o texto
pseudonimizado, os participantes como rótulos estáveis, e o mapa de reversão, cifrado
([0003](../decisoes/0003-cifra-em-repouso.md)) com finalidade e domínio próprios no rótulo de
derivação da chave ([0013](../decisoes/0013-chave-de-cifra-por-dominio.md)).

Fala com a anotação, que recebe o resultado, e com a custódia. O reconhecimento das ocorrências
aconteceu antes, na ingestão, sobre o texto como ele chegou; o que este módulo faz é a troca e a
guarda da correspondência. O serviço de linguagem devolve posição e tipo — tratar uma ocorrência
como pessoa, como lugar ou como nada sempre foi decisão do núcleo
([fronteiras](002-fronteiras.md)).

## Estável no domínio, e só nele

O mesmo participante é o mesmo rótulo em todo o material; participantes distintos nunca
compartilham rótulo, ainda que os nomes de origem coincidam. O escopo dessa estabilidade é o
domínio, e ela não o atravessa: a mesma pessoa em dois domínios recebe dois rótulos, porque rótulo
comum seria uma ligação entre acervos que sobreviveria à exportação
([0006](../decisoes/0006-dominios-coexistem.md)).

O rótulo é atribuído, nunca calculado a partir do original. Não carrega inicial, tamanho nem posição
alfabética — se carregasse, quem tivesse o texto e uma lista de nomes prováveis reverteria por
tentativa, sem tocar no mapa. O tipo da menção, em troca, continua reconhecível: pessoa, lugar e
organização se distinguem entre si depois da troca.

## A volta

Reverter é autorizado e auditado item por item, nunca um modo do sistema
([0012](../decisoes/0012-pseudonimizacao-reversivel.md)) — autorização que liga um interruptor deixa
de dizer o que foi revelado. É o único caminho entre o dataset e a identidade real, e por isso o
acesso ao mapa é evento de custódia como qualquer outro, com a diferença de que este revela pessoa.

A existência do mapa é o que permite conferir que a substituição acertou o alvo: ninguém verifica
uma anonimização que não se pode desfazer. É também o ponto único de falha do sistema, porque quem o
obtiver decifrado reidentifica o acervo inteiro. Perder a chave, em troca, é perder a volta em
definitivo — e a custódia da chave tem decisão própria
([0014](../decisoes/0014-custodia-da-chave-mestra.md)).

## Como este módulo falha

O reconhecimento é imperfeito, e a falha é conhecida: o que o detector não marcou segue adiante com
aparência de conteúdo comum. Nada aqui garante o contrário, e por isso as defesas contra resíduo
são posteriores — a revisão humana do lote e a auditoria que bloqueia a exportação. Nenhuma das duas
desfaz o resíduo dentro do acervo; as duas impedem que ele saia.
