# Transação de conteúdo

> Operacional. 2026-11-04.

- Projeto/pacote: `Core.Persistence.Content`
- Piso: 90 %
- Crítica: sim
- Partes da mutação: 46
- Motivo: Declara o domínio que o banco usa para decidir o que uma consulta de conteúdo vê (decisão
  0006); inteira, a área estoura o teto da 017 (#580) e por isso muta em 46 partes (#557, #716:
  com 5 partes a 3 passou dos 20 min; #726: com 8, a 2 levou 17 a 20 min por 17 mutantes de 41 a
  61 s cada, que rodam a suíte do banco; #791: com 12, a 6 testou 26 mutantes de 65 a 93 s cada,
  a suíte inteira, e passou dos 20 min com 0 mutantes; #801: com 16, a 3 testou 15 mutantes de 70
  a 95 s e estourou; com 22, a 18 tinha 14 mutantes de 75 a 90 s mais 5,7 min de preparo e passou
  dos 20 min: cabem 9 ou 10 por parte; #849: com 32, depois das colunas da sanitização (#842), a
  28 passou dos 20 min duas vezes na main; com 40 e o fator por token, a 28 levou 17,7 min e o
  método que abria toda transação, sozinho, 15,5 min; #846: as colunas da 0064 levaram a lista de
  `ContentColumns` a 84 literais, e com 40 doze partes ficaram com três mutantes de suíte; 46 é o
  menor número com dois no máximo). O tempo de uma parte segue os mutantes que
  rodam a suíte inteira: o peso conta um por literal de inicializador estático, e os dois métodos
  por onde passa todo teste de conteúdo, `OpenAsync` e `DeclareAsync` de `ContentTransaction`,
  ficam cada um sozinho numa parte ([039](../039-repartir-a-mutacao.md), seção "A unidade e o
  peso"); nenhuma outra parte tem mais de dois. A unidade da repartição é o trecho de arquivo
  cortado entre membros, pelo peso (#585, #683, #849), não o arquivo nem a contagem de trechos:
  assim nenhuma parte fica com a maioria dos mutantes. A leitura do conteúdo protegido é a área
  `Core.Persistence.Content.Reading`, a escrita é a `Core.Persistence.Content.Writing` e a cifra
  é a `Core.Persistence.Content.Sealing`.
  Guarda com cada item as ocorrências e o destino de cada uma, seladas com a chave do item, e a
  leitura delas é um evento da cadeia (#84).
  As tabelas `ContentColumns.ReplicationOccurrence` e `ContentColumns.ReplicationMark` (#82,
  decisão 0024) guardam o resumo com chave do texto e do remetente de cada mensagem e a marca
  que aponta para a primeira ocorrência; são só de inserção, valem pelo lote como o conteúdo, e
  a rejeição do lote as remove com ele.
  Na parte 3 da #82, `OccurrencesAsync` e `MarksAsync` leem só o que casa com os resumos do
  lote (`= ANY(@digests)`), nunca o domínio inteiro. Limite conhecido: dois lotes simultâneos
  do mesmo domínio não se veem, porque cada um só enxerga o que o outro já gravou; o mesmo
  texto neles não se marca entre si e a marca chega no próximo lote, sem falha nem duplicata
  (`ON CONFLICT DO NOTHING`). O tamanho gravado, `Length`, e o instante, `SaidAt`, valem em
  pontos de código e em microssegundos.
