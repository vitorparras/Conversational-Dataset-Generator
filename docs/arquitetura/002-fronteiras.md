# 002 — Fronteiras e contratos

> Descrição. 2026-10-04.

Fronteira explícita é o que permite trocar uma parte sem reabrir as outras. Aqui ela tem quatro
naturezas, e confundi-las é o jeito mais comum de perder a independência que o desenho pretende.

## Entre o núcleo e o serviço de linguagem

É a única fronteira de processo, e a única em que dado pessoal sai do processo que detém a chave. O
contrato está na [decisão 0011](../decisoes/0011-protocolo-com-o-servico-de-linguagem.md); o que
importa aqui é a forma dele.

O serviço devolve apontamentos — onde começa e termina cada trecho reconhecido, de que tipo, e a
classe gramatical e a função sintática do núcleo da ocorrência
([0018](../decisoes/0018-contrato-da-fronteira.md)) —, nunca texto transformado. Quem substitui, pseudonimiza e guarda o mapa de reversão
é o núcleo, porque é o lado que tem a chave. Um índice deslocado anonimiza o pedaço errado do texto,
e por isso o núcleo confere que as posições recebidas caem dentro do trecho enviado.

Atravessa o trecho, e nada que o situe: nem identificador de material, de domínio, de pessoa ou de
anotador. O serviço não sabe de quem é o texto nem de onde veio, não grava e não guarda nada do
acervo depois de responder — comprometê-lo rende texto sem procedência, ver
[texto fora do banco](../privacidade/003-texto-fora-do-banco.md).

Texto ainda não sanitizado atravessa aqui, e é o ponto mais frágil do desenho. Nem o texto nem os
deslocamentos entram em registro: deslocamento com tipo, somado ao texto que o núcleo tem,
reconstrói o dado pessoal ([requisito 021](../requisitos/021-registro-sem-dado-pessoal.md)). E o
transporte é local por condição, não por conveniência — a cifra protege o que está em repouso, não o
que trafega entre processos.

A unidade do deslocamento é ponto de código, e converte o lado que indexa diferente: o núcleo
([0011](../decisoes/0011-protocolo-com-o-servico-de-linguagem.md)). Sem unidade única, a divergência
cairia em emoji e caractere composto — sem erro, só no lugar errado.

Resposta ausente ou malformada não é o mesmo que nenhuma ocorrência: na primeira a ingestão para,
em vez de deixar passar texto como se estivesse limpo
([0011](../decisoes/0011-protocolo-com-o-servico-de-linguagem.md)).

## Entre módulos, dentro do núcleo

Nenhum módulo lê a tabela interna de outro. O que atravessa é o material no estado em que o anterior
o deixou, mais o registro do que foi feito com ele — e cada módulo declara três coisas, por onde se
verifica se pode ser substituído: a condição de entrada que exige, a garantia que dá na saída, e o
que registra. A exportação não sabe como a concordância foi calculada; sabe se o recorte atende ao
critério.

## Com o banco

O banco não é área de trabalho compartilhada: todo acesso a conteúdo passa pela política de linha do
domínio, que vale mesmo contra o dono da tabela
([0006](../decisoes/0006-dominios-coexistem.md)) — consulta que esqueça o domínio não devolve nada,
em vez de devolver material de outro. E a cifra fica do lado de fora: o que é sensível chega e sai
cifrado, a chave nunca cruza ([0003](../decisoes/0003-cifra-em-repouso.md)), e filtrar texto bruto
deixa de ser consulta — é decifrar na aplicação e filtrar lá.

## Com o mundo, na saída

O dataset exportado deixa o sistema e não volta. Dali em diante nada no sistema o corrige, e por
isso ele carrega a própria procedência: o recorte de domínios, as taxonomias vigentes, as contagens
e a referência de integridade. Ver [exportação](010-modulo-exportacao.md).
