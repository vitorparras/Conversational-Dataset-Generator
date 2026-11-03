# 016 — A migração do esquema do banco

> Operacional. 2026-10-25.

A migração é um **modo do mesmo executável**, pedido com `migrate` como primeiro argumento: roda
uma vez, com a credencial de dono, e termina. A partida normal **só confere a versão**: nunca migra.

## Como rodar

O modo exige a seção `Database` inteira, com o papel dono no lugar do papel da aplicação. A senha
vai por variável de ambiente, nunca por argumento, que qualquer processo da máquina lê.

```
export CDG_Database__Host=<servidor>:5432 CDG_Database__Name=<banco> CDG_Database__Username=<papel dono>
read -rs CDG_Database__Password && export CDG_Database__Password
./Core.Host migrate
unset CDG_Database__Host CDG_Database__Name CDG_Database__Username CDG_Database__Password
```

Os passos são os arquivos de `core/src/Core.Persistence/Migrations/Sql`, embutidos no executável e
aplicados em ordem numérica, cada um em transação própria com a sua linha em `public.schema_version`
(versão, nome, SHA-256, quando e quem).

## Como termina

| Código | Resultado | O que fazer |
|---|---|---|
| 0 | Passos pendentes aplicados | Conferir e registrar, como em 010 |
| 10 | Esquema já na última versão | Nada |
| 11 | Banco à frente deste executável | Instalar a versão do sistema que conhece o passo |
| 12 | Passo aplicado com conteúdo diferente | Não migrar; descobrir quem alterou o passo |
| 13 | Um passo falhou | Ler o passo e o SQLSTATE na saída; corrigir e rodar de novo |
| 14 | Sem conexão com o servidor | Conferir endereço, banco, papel e senha |
| 78 | Configuração recusada | Declarar as quatro chaves de `Database` |

Passo que falha é desfeito por inteiro, linha do registro inclusive; os anteriores ficam. O modo
grava, fora da transação, uma linha em `public.schema_failure`: versão, nome, SHA-256, SQLSTATE
(se houver), quando e quem; sem mensagem, texto do passo nem dado de material. A tabela só recebe
linhas: falha repetida deixa outra, e o passo corrigido não apaga as anteriores. Não fica linha se
a tabela não existe (0001 ou 0002), se a inserção falha ou se a sessão se perdeu; o código segue 13.
Migrações simultâneas não aplicam o mesmo passo: a segunda espera e termina com 10. O modo escreve
só no console, sem `Host:LogFile`: guarde a saída para o registro da janela.

## A partida normal

A versão é a última das garantias do servidor; banco vazio conta como 0. Banco atrás, à frente
ou com passo divergente impede a partida (69); atrás manda rodar `migrate`, divergente, não.

## O cerco da credencial

A janela segue [010](010-credencial-de-dono-do-banco.md): o `migrate` com passo pendente a abre na
cadeia, que ele verifica antes do primeiro passo (da versão 16, que cria a coluna que o verificador
lê; abaixo, só guarda o topo), e a partida seguinte a fecha. Cadeia quebrada recusa os eventos da
janela: o `migrate` falha no passo 0018 (SQLSTATE CE002), sem passo aplicado, e nada é
reconstruído ([006](006-restauracao.md)). Registro e log não levam a credencial.

## Passo novo

Um arquivo `NNNN-nome.sql` em `core/src/Core.Persistence/Migrations/Sql`, com o número seguinte.
**Passo aplicado nunca se edita**: o resumo muda e a partida recusa o banco; corrija em passo novo.
