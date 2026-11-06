# 038 — Repartir a mutação de uma área em partes

> Operacional. 2026-11-06.

A [017](017-teste-de-mutacao.md) dá o teto de 20 minutos por módulo. A área que não cabe nele
declara `- Partes da mutação: N` no seu arquivo de áreas (a
[014](014-modulos-criticos-e-pisos.md) diz o campo), e o passo `mutation` a reparte em N jobs,
cada um com o mesmo teto, projeto de teste e limite de sobreviventes. Aqui está como a repartição
decide, e por quê.

## A unidade e o peso

O passo corta cada arquivo C# entre os membros dos seus tipos e passa os trechos ao Stryker como
`arquivo.cs{a..b}`. O Stryker 5.0.0 só conta o mutante cujo nó cabe inteiro na faixa, e o corte
nunca cai num membro: cada nó está em uma só faixa e a soma das partes é a da área. Os trechos se
repartem dos mais pesados aos mais leves, sempre para a parte de menos peso, de modo que a divisão
só depende dos arquivos (#585: repartir por contagem deu a uma parte 40 mutantes).

O peso mede o custo de testar o trecho, não só o tamanho. É o número de tokens, sem comentário, e
**cinco vezes isso num inicializador estático**: campo ou propriedade estática com valor, ou
construtor estático. O Stryker não sabe quais testes alcançam o que roda uma vez, antes de
qualquer teste, e roda a suíte inteira para cada mutante dele.

## A medição que fixou o fator (#683)

Na parte 1 de 5 de `Core.Persistence.Content`, 24 mutantes, todos de texto em inicializadores
estáticos, rodaram 304 testes cada um e levaram 22 min 38 s dos 26 min 38 s da parte. Cada um
levou de 43 s a 74 s, e o tempo cresce a cada mutante. O mesmo peso em tokens, em código comum,
levou de 4 a 11 min. Daí o fator, entre 4 e 5. Se a suíte mudar muito, a medição se repete: o
relatório do Stryker em `--verbosity debug` traz o tempo de cada mutante.

## O corte dentro da lista

Um membro só não se divide, e uma lista estática de 26 elementos concentrava 21 desses mutantes.
Por isso uma lista de colchetes que inicializa um membro estático (`= [ a, b, c ]`) também se
corta, depois de cada vírgula entre os elementos. Não se corta a lista que tem `<` ou `>`, onde
uma vírgula pode ser de argumento genérico ou de lambda, nem depois da última vírgula. Cada
elemento fica inteiro numa faixa. O Stryker 5.0.0 não gera mutante da lista em si, só de cada
elemento, e por isso a soma continua sendo a da área; se isso mudar na versão fixada em
`core/dotnet-tools.json`, o corte tem de ser revisto antes de subir a versão.

## Como conferir

Cada parte escreve no relatório todos os mutantes do arquivo, os de fora da faixa como ignorados.
A união dos não ignorados nas N partes tem de ser a do arquivo inteiro, sem repetir nenhum.
