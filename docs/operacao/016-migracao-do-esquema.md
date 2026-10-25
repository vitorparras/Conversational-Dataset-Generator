# 016 — A migração do esquema do banco

> Operacional. 2026-10-25.

A migração é um **modo do mesmo executável**, pedido pelo operador com `migrate` como primeiro
argumento. Roda uma vez, com a credencial de dono, e termina. A partida normal usa a credencial da
aplicação e **só confere a versão**: nunca migra.

## Como rodar

O modo exige a seção `Database` inteira, com o papel dono no lugar do papel da aplicação. A senha
vai por variável de ambiente, nunca por argumento, que qualquer processo da máquina lê.

```
export CDG_Database__Host=<servidor>:5432
export CDG_Database__Name=<banco>
export CDG_Database__Username=<papel dono>
export CDG_Database__Password=<lida da guarda do dono>
./Core.Host migrate
```

Os passos são os arquivos de `core/src/Core.Persistence/Migrations/Sql`, embutidos no executável e
aplicados em ordem numérica. Cada um roda numa transação própria, junto com a sua linha na tabela
`public.schema_version`: versão, nome, SHA-256 do conteúdo, quando e quem (o usuário da sessão).

## Como termina

| Código | Resultado | O que fazer |
|---|---|---|
| 0 | Passos pendentes aplicados | Conferir e registrar, como em 010 |
| 10 | Esquema já na última versão | Nada |
| 11 | Banco à frente deste executável | Instalar a versão do sistema que conhece o passo |
| 12 | Passo aplicado com conteúdo diferente | Não migrar; descobrir quem alterou o passo |
| 13 | Um passo falhou | Ler o passo e o SQLSTATE no log; corrigir e rodar de novo |
| 14 | Sem conexão com o servidor | Conferir endereço, banco, papel e senha |
| 78 | Configuração recusada | Declarar as quatro chaves de `Database` |

Passo que falha é desfeito por inteiro, linha do registro inclusive: não fica registro de falha, só
o log. Os passos anteriores a ele ficam aplicados. Duas migrações ao mesmo tempo não aplicam o mesmo
passo: a segunda espera a primeira e termina com 10.

## A partida normal

A conferência da versão do esquema é a última das garantias do servidor. Banco vazio conta como
versão 0. Banco atrás, à frente ou com passo divergente impede a partida (código 69), nomeando a
versão encontrada e a esperada, ou o passo e os dois resumos. Atrás, a mensagem manda rodar o modo
`migrate`; divergente, manda não rodar.

## O cerco da credencial

A janela segue [010](010-credencial-de-dono-do-banco.md): verificar a custódia antes
([007](007-verificacao-da-custodia.md)), rodar, verificar depois e registrar a janela. A tabela
`public.schema_version` responde ao "quem, quando, o quê" da janela. Nem o registro nem o log levam
a credencial.

## Passo novo

Um arquivo `NNNN-nome.sql` a mais em `core/src/Core.Persistence/Migrations/Sql`, com o número
seguinte. **Passo já aplicado nunca se edita**: o resumo muda e a partida passa a recusar o banco.
Correção vai em passo novo.
