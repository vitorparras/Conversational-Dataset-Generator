# 023 — Iniciar o núcleo publicado

> Operacional. 2026-10-30.

Depois de [021](021-subir-o-nucleo-publicado.md) (pasta do serviço, senhas e `migrate`) e antes de
[022](022-conferir-o-nucleo-publicado.md) (conferir o que subiu). Mesmas premissas: só bash e
`/proc`, sem cliente HTTP, `ps` nem cliente SQL.

## Iniciar

O núcleo precisa de um processo que sobreviva a quem o iniciou, e o registro vai ao console, então
o redirecionamento o guarda (`Host:LogFile` o grava em arquivo). Em shell que dura (terminal,
`ssh`), com `&`:

```
cd /opt/cdg/nucleo && CDG_Database__Password="$(cat /tmp/senha-app)" ./Core.Host \
  --Host:Urls=http://127.0.0.1:5080 --Installation:Kind=demonstracao --Database:Host=db:5432 \
  --Database:Name=cdg --Database:Username=app > /tmp/nucleo.log 2>&1 &
```

Chegando por `docker exec`, o shell acaba com a chamada, e o `&` só sobrevive se ela não tiver
terminal; para não depender disso, use `-d`, que dispensa o `&`. O shell que fica de pai grava o
código de saída do núcleo em `/tmp/nucleo.saida`:

```
docker exec -d CONTEINER bash -c ': > /tmp/nucleo.saida; cd /opt/cdg/nucleo \
  && export CDG_Database__Password="$(cat /tmp/senha-app)" \
  && ./Core.Host --Host:Urls=http://127.0.0.1:5080 --Installation:Kind=demonstracao \
  --Database:Host=db:5432 --Database:Name=cdg --Database:Username=app > /tmp/nucleo.log 2>&1; \
  echo $? > /tmp/nucleo.saida'
```

## Esperar o núcleo ficar pronto

O núcleo só abre a porta depois que o serviço de linguagem responde e as conferências passam, e
escreve então a linha `Now listening on`. Espere por ela, sem adivinhar um prazo. Se o núcleo sai
antes, a recusa está no mesmo registro, e `/tmp/nucleo.saida` (modo `-d`) traz o código: 69 numa
conferência que falha, como a senha recusada; 78, configuração recusada; 70, falha ao servir.

```
timeout 60 bash -c 'until grep -q "Now listening on" /tmp/nucleo.log 2>/dev/null \
  || [ -s /tmp/nucleo.saida ]; do sleep 1; done'
grep -q "Now listening on" /tmp/nucleo.log && echo pronto || tail -5 /tmp/nucleo.log
```

## Encerrar

SIGTERM ao PID dado por `pid '^./Core[.]Host'` (022), por exemplo `kill -TERM $(pid ...)`. No
modo `-d`, depois que o processo sumir, `cat /tmp/nucleo.saida` deve dar 0.

## Os avisos da partida

`Cannot load library libgssapi_krb5.so.2` aparece antes dos registros, na partida com banco e no
`migrate`, quando a biblioteca de autenticação Kerberos não está na máquina. O cliente do banco a
tenta e segue sem ela; a conexão usa a senha. Instalá-la é opcional (ver 001).
