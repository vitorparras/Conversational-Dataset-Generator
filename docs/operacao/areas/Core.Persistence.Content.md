# Transação de conteúdo

> Operacional. 2026-11-04.

- Projeto/pacote: `Core.Persistence.Content`
- Piso: 90 %
- Crítica: sim
- Partes da mutação: 5
- Motivo: Declara o domínio que o banco usa para decidir o que uma consulta de conteúdo vê (decisão
  0006); inteira, a área estoura o teto da 017 (#580) e por isso muta em 5 partes, cada uma em até
  12 min (#557). A unidade da repartição é o trecho de arquivo cortado entre membros, pelo peso
  (#585, #683), não o arquivo nem a contagem de trechos: assim nenhuma parte fica com a maioria dos
  mutantes. A leitura do conteúdo protegido é a área `Core.Persistence.Content.Reading`, a escrita
  é a `Core.Persistence.Content.Writing` e a cifra é a `Core.Persistence.Content.Sealing`
  As tabelas `replication_occurrence` e `replication_mark` (#82, decisão 0024) guardam o resumo com
  chave do texto e do remetente de cada mensagem e a marca que aponta para a primeira ocorrência;
  são só de inserção, valem pelo lote como o conteúdo, e a rejeição do lote as remove com ele
