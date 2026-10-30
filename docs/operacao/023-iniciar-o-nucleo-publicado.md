# 023 — Iniciar o núcleo publicado

> Operacional. 2026-10-30.

Depois de [021](021-subir-o-nucleo-publicado.md) (senhas, `migrate` e como rodar os blocos num
contêiner operado de fora) e antes de [022](022-conferir-o-nucleo-publicado.md). Mesmas premissas.

## Iniciar

O registro vai ao console, então o redirecionamento o guarda (`Host:LogFile` o grava em arquivo). O
núcleo roda em segundo plano, e o subshell grava o código de saída dele em `/tmp/nucleo.saida`:

```
: > /tmp/nucleo.saida
cd /opt/cdg/nucleo
( CDG_Database__Password="$(cat /tmp/senha-app)" ./Core.Host --Host:Urls=http://127.0.0.1:5080 \
  --Installation:Kind=demonstracao --Database:Host=db:5432 --Database:Name=cdg \
  --Database:Username=app > /tmp/nucleo.log 2>&1; echo $? > /tmp/nucleo.saida ) &
```

## Esperar o núcleo ficar pronto

O núcleo só abre a porta depois que o serviço de linguagem responde e as conferências passam, e
escreve então a linha `Now listening on`. Espere por ela, ou pela saída do núcleo, sem adivinhar um
prazo (60 s é o limite). Se o núcleo sai antes, a recusa está no registro, e `/tmp/nucleo.saida`
traz o código: 69, conferência que falha (senha recusada); 78, configuração; 70, falha ao servir.

```
for i in $(seq 60); do
  grep -q 'Now listening on' /tmp/nucleo.log && break
  [ -s /tmp/nucleo.saida ] && break
  sleep 1
done
grep -q 'Now listening on' /tmp/nucleo.log && echo pronto || tail -5 /tmp/nucleo.log
```

## Encerrar

SIGTERM ao PID do núcleo (`pid`, de 022). O núcleo sumiu quando `/tmp/nucleo.saida` deixa de estar
vazio, e o código nela deve ser 0:

```
. /tmp/f.sh
kill -TERM $(pid '^./Core[.]Host')
for i in $(seq 30); do [ -s /tmp/nucleo.saida ] && break; sleep 1; done
cat /tmp/nucleo.saida
```

No fim da conferência, apague as senhas do contêiner:

```
rm -f /tmp/senha-owner /tmp/senha-app
```

## Os avisos da partida

Antes dos registros, na partida com banco e no `migrate`, saem duas linhas de um mesmo aviso:
`Cannot load library libgssapi_krb5.so.2` e `Error: libgssapi_krb5.so.2: cannot open shared object
file: No such file or directory`. A biblioteca de autenticação Kerberos não está na máquina; o
cliente do banco a tenta e segue sem ela, e a conexão usa a senha. Instalá-la é opcional (ver 001).
