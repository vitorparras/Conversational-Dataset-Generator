# 049 — O aplicador da configuração: a conta e o papel do banco

> Operacional. 2026-11-09. Decisão [0053](../decisoes/0053-configuracao-alterada-pela-tela.md).

A tela propõe e outra pessoa aprova; **só o aplicador escreve o appsettings.json**. É o mesmo
executável, com a palavra `apply-configuration`, rodado uma vez pelo gerenciador de serviço sob
uma conta própria. O arquivo e o serviço estão em [050](050-arquivo-e-servico-do-aplicador.md); o
que ele faz e diz, em [051](051-o-que-o-aplicador-faz-e-recusa.md). As contas ficam assim:

| Conta | Escreve no appsettings.json | Escreve no banco |
|---|---|---|
| Núcleo (`nucleo`, [032](032-conta-do-nucleo.md)) | não | sim, pelo papel `app` |
| Aplicador (`aplicador`) | sim | **não**, pelo papel `leitura` |
| Quem opera | não | não |

## O papel do banco que só lê

O dono do banco cria o papel uma vez e dá a ele só o que o aplicador lê: conectar, usar o esquema
e `SELECT`. Nada de `INSERT`, `UPDATE`, `DELETE`, `TRUNCATE` nem `CREATE`; o esquema `public` já
não tem `CREATE` para `PUBLIC` ([001](001-ambiente.md)).

```
CREATE ROLE leitura LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS PASSWORD :'senha_leitura';
GRANT CONNECT ON DATABASE cdg TO leitura;
GRANT USAGE ON SCHEMA public TO leitura;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO leitura;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO leitura;
```

O `ALTER DEFAULT PRIVILEGES` cobre as tabelas que uma migração futura criar. Rode-o com o papel
que roda as migrações, ou o padrão não vale para o que ele cria.

O aplicador **confere o papel antes de qualquer coisa**: se a credencial grava em alguma tabela
ou cria em algum esquema da aplicação (`public`, `content` ou outro que não seja do sistema), ele
recusa com o código 37 e não toca no arquivo.

## Conferir (CA-7)

Com a conta do núcleo, o arquivo não pode ser gravado; com o papel `leitura`, o banco não pode:

```
test -w /opt/cdg/appsettings.json && echo "gravável: corrija" || echo "não gravável, como deve ser"
psql "host=127.0.0.1 dbname=cdg user=leitura" -c "UPDATE public.configuration_change SET motive = motive"
```

O `UPDATE` tem de falhar com "permissão negada".
