# 023 — Iniciar o núcleo publicado

> Operacional. 2026-10-30.

Depois de [021](021-subir-o-nucleo-publicado.md), [024](024-arquivos-de-custodia-da-chave.md) e
[025](025-certificado-do-canal-cifrado.md), e antes de [022](022-conferir-o-nucleo-publicado.md).

## Iniciar

Os valores são os do exemplo de 021 (banco, papel `app`, `demonstracao`), a trocar pelos do seu
ambiente: `ITERACOES` é o fator das senhas, de 600000 a 10000000 com `Installation:Kind=real` e
de 1 a 10000000 com `demonstracao` ([0047](../decisoes/0047-fator-de-trabalho-das-senhas.md));
prazos: `Session:*`. Roda em segundo plano; a saída vai a `/tmp/nucleo.saida`:

```
: > /tmp/nucleo.saida
cd /opt/cdg/nucleo
( CDG_Database__Password="$(cat /tmp/senha-app)" CDG_Session__IdleTimeout=00:30:00 CDG_Session__AbsoluteTimeout=08:00:00 ./Core.Host --Host:Urls=https://127.0.0.1:5080 \
  --Installation:Kind=demonstracao --Passwords:WorkFactor=ITERACOES --MasterKey:File=/etc/cdg/custodia/chave \
  --MasterKey:LabelsFile=/etc/cdg/custodia/rotulos --Tls:CertificateFile=/etc/cdg/tls/certificado.pem \
  --Tls:KeyFile=/etc/cdg/tls/chave.pem --Database:Host=db:5432 --Database:Name=cdg \
  --Database:Username=app > /tmp/nucleo.log 2>&1; echo $? > /tmp/nucleo.saida ) &
```

## Esperar o núcleo ficar pronto

O núcleo só abre a porta depois que o serviço de linguagem responde e as conferências passam, e
escreve então `Now listening on`. Espere por essa linha, ou pela saída do núcleo, sem adivinhar um
prazo (limite de 60 s). Se ele sai antes, a recusa está no registro e `/tmp/nucleo.saida` traz o
código: 69, conferência que falha (senha recusada, chave); 78, configuração; 70, falha ao servir.

```
for i in $(seq 60); do
  grep -q 'Now listening on' /tmp/nucleo.log && break
  [ -s /tmp/nucleo.saida ] && break
  sleep 1
done
grep -q 'Now listening on' /tmp/nucleo.log && echo pronto || tail -5 /tmp/nucleo.log
```

## Encerrar

SIGTERM ao PID do núcleo. O `pid` vem de `/tmp/f.sh`, que o primeiro bloco de 022 grava (sem ele,
o `. /tmp/f.sh` falha). O núcleo sumiu quando `/tmp/nucleo.saida` deixa de estar vazio, com código
0. Aqui sai a senha da aplicação (a do dono saiu em 021):

```
. /tmp/f.sh
kill -TERM $(pid '^./Core[.]Host')
for i in $(seq 30); do [ -s /tmp/nucleo.saida ] && break; sleep 1; done
cat /tmp/nucleo.saida
rm -f /tmp/senha-app
```

## Os avisos da partida

Antes dos registros, na partida com banco e no `migrate`, saem duas linhas de um mesmo aviso:
`Cannot load library libgssapi_krb5.so.2` e `Error: libgssapi_krb5.so.2: cannot open shared object
file: No such file or directory`. A biblioteca de autenticação Kerberos não está na máquina; o
cliente do banco a tenta e segue sem ela, e a conexão usa a senha. Instalá-la é opcional (ver 001).
