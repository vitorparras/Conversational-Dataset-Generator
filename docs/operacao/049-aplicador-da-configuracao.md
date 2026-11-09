# 049 — O aplicador da configuração: a conta, o papel do banco e o serviço

> Operacional. 2026-11-09. Decisão [0053](../decisoes/0053-configuracao-alterada-pela-tela.md).

A tela propõe e outra pessoa aprova; **só o aplicador escreve o
`appsettings.json`**. É o mesmo executável, com a palavra `apply-configuration`, rodado uma vez pelo gerenciador de serviço sob uma
conta própria. As três contas da instalação ficam assim:

| Conta | Escreve no `appsettings.json` | Escreve no banco |
|---|---|---|
| Núcleo (`nucleo`, [032](032-conta-do-nucleo.md)) | não | sim, pelo papel `app` |
| Aplicador (`aplicador`) | sim | **não**, pelo papel `leitura` |
| Quem opera | não | não |

## O papel do banco que só lê

O dono do banco cria o papel uma vez e dá a ele só o que o aplicador lê: conectar, usar o esquema e `SELECT`. Nada de `INSERT`, `UPDATE`,
`DELETE`, `TRUNCATE` nem `CREATE`; o esquema `public` já não tem `CREATE` para `PUBLIC` ([001](001-ambiente.md)).

```
CREATE ROLE leitura LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS PASSWORD :'senha_leitura';
GRANT CONNECT ON DATABASE cdg TO leitura;
GRANT USAGE ON SCHEMA public TO leitura;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO leitura;
```

O aplicador **confere o papel antes de qualquer coisa**: se a credencial grava em alguma tabela ou cria no esquema, ele recusa com o
código 37 e não toca no arquivo.

## O arquivo do aplicador

`applier.json`, ao lado do binário, de modo `0600` e dono `aplicador`: a conta do núcleo não o lê. Ele guarda a senha do papel `leitura`,
e as chaves são fechadas (chave que falta, caminho relativo ou que não existe recusa o início nomeando a chave):

```
{
  "Applier": {
    "SettingsFile": "/opt/cdg/appsettings.json",
    "RestartCommand": "/usr/bin/systemctl",
    "RestartArguments": "restart|cdg-nucleo.service"
  },
  "Database": { "Host": "127.0.0.1:5432", "Name": "cdg", "Username": "leitura", "Password": "…" }
}
```

O reinício roda **sem shell** e sem nada do banco nos argumentos; os argumentos se separam por `|`. O `appsettings.json` é de
`aplicador:nucleo` com modo `0640`: o núcleo lê e não grava (CA-7). A pasta tem de deixar o aplicador criar e trocar arquivos nela.

## O serviço

Um serviço de execução única e um temporizador, sob a conta `aplicador`:

```
# /etc/systemd/system/cdg-aplicador.service
[Service]
Type=oneshot
User=aplicador
ExecStart=/opt/cdg/Core.Host apply-configuration

# /etc/systemd/system/cdg-aplicador.timer
[Timer]
OnBootSec=2min
OnUnitActiveSec=5min
[Install]
WantedBy=timers.target
```

`systemctl restart cdg-nucleo.service` é permitido ao `aplicador` por uma regra do `polkit` ou do `sudoers` restrita a esse comando.

## O que ele faz e o que diz

Para cada alteração aprovada e ainda não em vigor, na ordem da aprovação, confere na cadeia que ela fecha, que quem submeteu e quem
aprovou são pessoas distintas, ativas e com a administração técnica, que a chave está na lista do código e que o carregador aceita o
valor. Aplicando, grava o arquivo de forma atômica, guarda `appsettings.json.<momento>.previous` e, ao fim, reinicia o núcleo uma vez. Na
partida, o núcleo registra na cadeia que a alteração está em vigor.

O que ele não aplica fica no log do servidor (a saída de erro, que o gerenciador guarda), com o motivo e as duas sequências, nunca nome
nem endereço de pessoa: `ChainBroken`, `SameAuthor`, `SubmitterNotAllowed`, `ApproverNotAllowed`, `KeyOnlyOnServer`, `ValueRefused` e
`FileUnreadable`. Códigos de saída: 0 (aplicou ou não havia o que aplicar), 37 (recusado, nada gravado), 38 (o banco não respondeu ou o
núcleo não reiniciou; a próxima execução tenta de novo).

### A recusa que fica registrada

O aplicador **só lê o banco e não grava a recusa**: quem a grava é o núcleo, na partida e a cada 5 minutos, com as mesmas regras (o
mesmo código de julgamento). Os motivos se separam assim:

- **Definitivos**, que não mudam sozinhos: `SameAuthor`, `SubmitterNotAllowed`, `ApproverNotAllowed`, `KeyOnlyOnServer` e `ValueRefused`.
  O núcleo grava na cadeia um evento `configuration_not_applied`, de autor sistema, uma vez por alteração, com o código do motivo e a
  sequência da aprovação, sem nome nem endereço de pessoa. A alteração sai da lista de pendentes, e o aplicador deixa de tentar e de
  dizê-la no log.
- **Passageiros**: `ChainBroken` e `FileUnreadable`, além de um banco que não responde. Nada é gravado, a alteração segue pendente e
  o aplicador tenta de novo na próxima execução.

Quem administra vê a alteração como não aplicada, com o motivo em palavras, na tela da alteração (a leitura é
`ApprovedChanges.RefusedAsync`, que devolve a chave, o código do motivo e a sequência da aprovação). Uma alteração que já está em vigor
não pode ser registrada como não aplicada.

## Conferir (CA-7)

Com a conta do núcleo, o arquivo não pode ser gravado; com o papel `leitura`, o banco não pode:

```
test -w /opt/cdg/appsettings.json && echo "gravável: corrija" || echo "não gravável, como deve ser"
psql "host=127.0.0.1 dbname=cdg user=leitura" -c "UPDATE public.configuration_change SET motive = motive"
```

O `UPDATE` tem de falhar com "permissão negada".
