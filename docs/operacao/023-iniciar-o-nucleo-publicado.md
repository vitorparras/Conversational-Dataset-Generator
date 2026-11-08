# 023 — Iniciar o núcleo publicado

> Operacional. 2026-10-30.

Depois de [021](021-subir-o-nucleo-publicado.md), [024](024-arquivos-de-custodia-da-chave.md), [025](025-certificado-do-canal-cifrado.md)
e [028](028-criar-primeira-pessoa.md) (a primeira pessoa), e antes de [022](022-conferir-o-nucleo-publicado.md).

## Iniciar

Valores de 021, a trocar: `Passwords:WorkFactor` ([0047](../decisoes/0047-fator-de-trabalho-das-senhas.md)),
de 600000 a 10000000 com `real` e de 1 a 10000000 com `demonstracao` (o bloco usa 100000);
`Session:*`, prazos; `Ingestion:Conditions`, as condições de origem, da mais restrita à mais permissiva; `Custody:VerificationInterval`, a cadeia; `Audit:*`, pasta, intervalo. Roda
como a conta de [032](032-conta-do-nucleo.md), em segundo plano, com a saída em `/tmp/nucleo.saida`:

```
: > /tmp/nucleo.saida
cd /opt/cdg/nucleo
( CDG_Database__Password="$(cat /tmp/senha-app)" CDG_Session__IdleTimeout=00:30:00 CDG_Session__AbsoluteTimeout=08:00:00 CDG_Ingestion__Conditions=restrita,intermediaria,permissiva CDG_Custody__VerificationInterval=00:05:00 CDG_Custody__SealSituation=Manual ./Core.Host --Host:Urls=https://127.0.0.1:5080 \
  --Installation:Kind=demonstracao --Passwords:WorkFactor=100000 --MasterKey:File=/etc/cdg/custodia/chave \
  --MasterKey:LabelsFile=/etc/cdg/custodia/rotulos --MasterKey:EpochsFile=/etc/cdg/custodia/epocas --Tls:CertificateFile=/etc/cdg/tls/certificado.pem \
  --Tls:KeyFile=/etc/cdg/tls/chave.pem --Database:Host=db:5432 --Database:Name=cdg \
  --Database:Username=app --Audit:ServerLogDirectory=/var/log/postgresql --Audit:CompareInterval=01:00:00 > /tmp/nucleo.log 2>&1; echo $? > /tmp/nucleo.saida ) &
```

## Esperar o núcleo ficar pronto

O núcleo só abre a porta depois que o serviço de linguagem responde e as conferências passam, e
escreve então `Now listening on`. Espere por ela ou pela saída do núcleo, até 60 s. Se ele sai
antes, o registro traz a recusa e `/tmp/nucleo.saida`, o código: 69, conferência que falha (senha,
chave, pasta do registro); 78, configuração (como falta `--MasterKey:EpochsFile`); 70, ao servir.

```
for i in $(seq 60); do
  grep -q 'Now listening on' /tmp/nucleo.log && break
  [ -s /tmp/nucleo.saida ] && break
  sleep 1
done
grep -q 'Now listening on' /tmp/nucleo.log && echo pronto || tail -5 /tmp/nucleo.log
```

## Encerrar

SIGTERM ao PID do núcleo; o `pid` vem de `/tmp/f.sh`, que o primeiro bloco de 022 grava. O núcleo
sumiu quando `/tmp/nucleo.saida` deixa de estar vazio, com código 0. Saem aqui a senha da
aplicação, a da pessoa e o cookie (a do dono saiu em 021):

```
. /tmp/f.sh
kill -TERM $(pid '^./Core[.]Host')
for i in $(seq 30); do [ -s /tmp/nucleo.saida ] && break; sleep 1; done
cat /tmp/nucleo.saida
rm -f /tmp/senha-app /tmp/senha-pessoa /tmp/cookie
```

## Os avisos da partida

Na partida com banco e no `migrate` saem, antes dos registros, duas linhas de um aviso:
`Cannot load library libgssapi_krb5.so.2` e `Error: libgssapi_krb5.so.2: cannot open shared object
file: No such file or directory`. A biblioteca Kerberos não está na máquina; o cliente do banco
segue sem ela e a conexão usa a senha. Instalá-la é opcional (001).
