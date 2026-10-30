# 022 — Conferir o núcleo publicado

> Operacional. 2026-10-30.

Depois de [023](023-iniciar-o-nucleo-publicado.md): consultar as rotas, derrubar o serviço de
linguagem e ler o resultado, sem cliente HTTP, `ps` nem cliente SQL na máquina. Cada bloco roda num
shell novo, então as duas funções ficam em `/tmp/f.sh`, e os blocos as leem. Como rodar os blocos
num contêiner operado de fora está em [021](021-subir-o-nucleo-publicado.md).

## Consultar as rotas

```
cat > /tmp/f.sh <<'FIM'
rota() { exec 3<>/dev/tcp/127.0.0.1/5080
  printf '%s %s HTTP/1.0\r\nHost: x\r\nContent-Type: application/json\r\nContent-Length: %s\r\n\r\n%s' \
    "$1" "$2" "${#3}" "$3" >&3; cat <&3; echo; exec 3<&-; }
pid() { for p in /proc/[0-9]*; do tr '\0' ' ' < $p/cmdline 2>/dev/null \
  | grep -q "$1" && echo ${p#/proc/}; done; true; }
FIM
```

```
. /tmp/f.sh
rota GET /status
rota GET /versions
rota POST /analysis '{"text":"texto de teste","language":"pt"}'
```

Sem `Content-Type: application/json`, a análise responde 415 sem corpo. `${#3}` conta caracteres, e
com acento a conta de bytes é outra. Os campos do pedido e as recusas com 400, sempre sem corpo,
estão no README.

## Derrubar o serviço e ler a recusa

`pid` imprime os PIDs cuja linha de comando casa com o padrão; o `[s]` e o `^` impedem que o
próprio shell que procura case com o que procura. O núcleo é `pid '^./Core[.]Host'` (SIGTERM, em
023), e o serviço, `pid 'language_[s]ervice'`. O `/status` muda quando o núcleo percebe a queda;
espere por isso, até 30 s:

```
. /tmp/f.sh
kill -9 $(pid 'language_[s]ervice')
for i in $(seq 30); do rota GET /status | grep -q Unavailable && break; sleep 1; done
rota GET /status
rota POST /analysis '{"text":"texto de teste","language":"pt"}'
for i in $(seq 10); do grep -q interrompida /tmp/nucleo.log && break; sleep 1; done
grep interrompida /tmp/nucleo.log | tail -3
```

Então `/status` mostra `languageService` `Unavailable` e `ready` falso, e `/analysis` responde 503
com `reason` e `cause`. Na Fase 1 nenhuma tabela recebe análise: "nada gravado" se lê no corpo do
503 e na linha `interrompida` do registro, que chega um instante depois da resposta (daí a espera).
Conferir o banco pede cliente SQL, na máquina de quem opera o banco.

## O que parece erro e não é

O reconhecedor da Fase 1 é o vazio: `occurrences` é sempre 0 e `modelVersion` é `null`
(`ModelVersion=<absent>` no registro), até com nome de pessoa no texto. A pasta leva o modelo.
