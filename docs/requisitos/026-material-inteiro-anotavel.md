# 026 — O material inteiro é anotável

> Norma vigente. 2026-10-04.

**É possível anotar no nível do material inteiro, e não apenas no do bloco, de modo que
classificação cujo apoio está distribuído entre blocos possa ser registrada.**

## Por que existe

É este ponto que resolve o risco do recorte descrito em
[025](025-bloco-e-recorte-de-leitura.md). Se só o bloco for anotável, o caso em que o sentido existe
apenas no conjunto não fica errado: fica impossível de registrar. E o que não se registra não entra
em medida nenhuma, não aparece em divergência e não chega ao dataset — a ausência é invisível por
construção.

Com os dois níveis, o recorte volta a ser o que deveria ser: conveniência de leitura. Sem eles, o
recorte é uma decisão de interface que amputa o que pode ser classificado, tomada por quem escolheu
o tamanho do bloco.

Os níveis também precisam continuar distinguíveis depois. Anotação de bloco e anotação do conjunto
respondem perguntas diferentes, e somá-las numa contagem só produz número que não descreve nenhuma
das duas.

## Critérios de aceitação

- Dado um material recortado em blocos, quando quem anota conclui que a classificação se apoia no
  conjunto, então pode registrá-la no nível do material, sem escolher um bloco para abrigá-la.
- Dado uma classificação no nível do material, quando ela é gravada, então sua marcação de apoio
  pode apontar trechos de mais de um bloco, conforme
  [024](024-marcacao-de-apoio-na-anotacao.md).
- Dado anotações nos dois níveis, quando são lidas, medidas ou exportadas, então o nível de cada uma
  é distinguível e as contagens por nível são separadas.
- Dado um material cujo conjunto ainda não foi decidido, quando o progresso é contado, então ele não
  aparece concluído por terem sido decididos todos os seus blocos, conforme
  [023](023-atribuicao-por-pessoa.md).
- Dado um recorte de exportação que inclua anotações dos dois níveis, quando o artefato é produzido,
  então ele declara isso, conforme [014](014-exportacao-declara-o-recorte.md).

## O que não exige

Não exige anotação nos dois níveis para todo material, nem decisão de conjunto quando o bloco basta.
Não exige mais de dois níveis: bloco dentro de bloco altera a estrutura da unidade de análise, e a
[decisão 0005](../decisoes/0005-unidades-de-analise.md) manda tratar isso por decisão própria.
