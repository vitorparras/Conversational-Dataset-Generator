# 0059 — Mutação do diff no pull request e do módulo inteiro à noite

> Decisão vigente. 2026-11-23. Substitui a [0007](0007-garantia-de-qualidade.md) quanto a onde a
> mutação roda sobre o módulo inteiro.

## Contexto

A [0007](0007-garantia-de-qualidade.md) põe a mutação dos módulos críticos no bloqueio, e a
[017](../operacao/017-teste-de-mutacao.md) muta inteiro cada módulo que o pull request alcança. Um
módulo grande ocupa muitos jobs: `Core.Persistence.Content` roda em 96 partes, e a área da escrita
dele, em 24 ([039](../operacao/039-repartir-a-mutacao.md)). Um pull request que muda um arquivo
dele espera a mutação de todos os outros, que não mudaram.

## Decisão

**No pull request, cada módulo crítico alcançado muta só os arquivos dele que o diff mudou.** O
módulo alcançado sem arquivo no diff não abre job. As partes da 039 se repartem na proporção do peso
desses arquivos. Diff que não se lê muta o módulo inteiro.

**O piso de cada módulo ([014](../operacao/014-modulos-criticos-e-pisos.md)) e o limite de
sobreviventes da 017 valem sobre o conjunto de arquivos mutado no diff, sem baixar nenhum.** O teto
de 20 minutos continua por módulo. Arquivo do diff sem mutante passa, com nota. O relatório separa o
mutado do que não foi: a lista da matriz diz, por módulo, quantos arquivos muta, quais, e que os
outros ficam para a noturna; o passo repete os arquivos mutados. Verde no pull request não quer
dizer módulo inteiro coberto.

**Todo dia, o agendamento da verificação muta inteiro cada módulo crítico do main; o merge no main
não muta.** Um módulo que reprova ganha uma issue com o módulo, o run e os mutantes sobreviventes;
se já há uma aberta para ele, a reprovação vira comentário nela. Reprovação sem relatório ganha uma
issue com o run. Esta é a decisão escrita que a 017 exige para mutação fora do bloqueio: a noturna
não bloqueia merge, porque o código já entrou.

O dono do projeto aprovou a troca da mutação completa em todo pull request pela mutação do diff no
pull request mais a completa noturna no main.

O Build remoto, com a opção de mutação, roda a mesma lista e o mesmo passo contra o main.

## Alternativas recusadas

| Opção | Por quê |
|---|---|
| Mutação completa em todo pull request | A espera cresce com o módulo, não com a mudança |
| Escopo incremental de cada ferramenta | Seria uma regra por linguagem; o passo escolhe os arquivos das duas do mesmo jeito |
| Só a noturna, sem mutação no pull request | O defeito do arquivo mudado só apareceria depois do merge |

## Consequências

O teste enfraquecido sem mudança no código do módulo, e o módulo que só depende do que mudou, não
são medidos no pull request: aparecem na noturna, depois do merge. A issue da noturna é trabalho a
priorizar; enquanto aberta, o módulo inteiro está abaixo do piso. O agendamento passa de semanal a
diário, e tudo o que roda nele roda todo dia.

## O que reabre

Uma issue da noturna sobre arquivo que um pull request mutou verde na mesma semana, ou issues da
noturna abertas por mais de uma semana sem correção, levam de volta a mutação completa ao pull
request.
