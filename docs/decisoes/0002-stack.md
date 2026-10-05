# 0002 — A stack

> Decisão vigente. 2026-10-04.

## Decisão

| Camada | Escolha |
|---|---|
| Núcleo: web, persistência, autenticação, pipeline | C# |
| Processamento de linguagem natural | Python, como serviço separado |
| Banco | PostgreSQL, com colunas estruturadas e campos JSON |
| Distribuição | Executável único, sem instalar nada no destino |

## Por quê

**C# no núcleo.** É a única candidata que reúne as três coisas que o projeto exige ao mesmo tempo:
expressões regulares com contexto à esquerda de largura variável, executável único e teste de
mutação maduro. A primeira é a que mais pesa — reconhecer documentos brasileiros depende do texto
que antecede o número ("CPF", "documento", "inscrição"), e esses prefixos variam em extensão.

**Python no processamento de linguagem.** Os modelos que reconhecem entidades junto com a análise
gramatical do mesmo trecho, para português, estão nesse ecossistema. Fora dele é preciso montar isso
com duas peças e conciliar as posições de caractere na mão — e um índice errado anonimiza o pedaço
errado do texto.

**PostgreSQL.** A custódia exige esquema e integridade; a reutilização em outros domínios exige
campos que variam. Ele faz as duas coisas num motor só.

## Recusados

| Opção | Por quê |
|---|---|
| Python no núcleo | Tipos verificados só em execução; exige interpretador no destino |
| Go | Sem contexto à esquerda, por garantir tempo linear; mutação imatura |
| Rust | Sem contexto à esquerda, pela mesma razão |
| Java | Exige máquina virtual no destino. É a alternativa mais próxima |
| Banco documental | Na edição aberta, sem cifra em repouso nem imutabilidade imposta pelo servidor |
| Dois bancos | Duas bases para cifrar e salvar em backup, sem transação entre elas |

## Consequências

A cifra em repouso precisa de decisão própria, e **bloqueia o código que persiste dado sensível**.

Texto sensível passa a atravessar uma fronteira de processo até o serviço de linguagem. A decisão
sobre esse formato precisa dizer o que trafega e o que nunca pode entrar em registro de log.

Duas linguagens custam duas suítes de teste e duas formas de empacotar.

## O que reabre

Se não houver, em PostgreSQL, forma de manter a cifra junto ao dado — de modo que copiar o banco
para fora não exponha nada.

Se o executável único deixar de ser requisito. Nesse caso Java volta, com vantagem em mutação.
