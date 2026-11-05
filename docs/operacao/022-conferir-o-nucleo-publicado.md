# 022 — Conferir o núcleo publicado

> Operacional. 2026-10-30.

Depois de [023](023-iniciar-o-nucleo-publicado.md): entrar com a pessoa de [028](028-criar-primeira-pessoa.md),
consultar as rotas, derrubar o serviço de linguagem e ler o resultado, com `openssl s_client` como
cliente TLS (o canal é cifrado também no loopback; **proposta ao dono**, ver
[001](001-ambiente.md), que lista o pacote `openssl`), sem `ps` nem cliente SQL na máquina. Cada bloco roda num shell novo, então as funções ficam em `/tmp/f.sh`.
Num contêiner operado de fora: [021](021-subir-o-nucleo-publicado.md).

## Entrar e consultar as rotas

Só `GET /status` responde sem sessão; o resto dá 401. `entrar NOME` abre a sessão (`POST /session`,
com a senha de `/tmp/senha-pessoa`) e guarda o cookie em `/tmp/cookie`, que `rota` envia:

```
cat > /tmp/f.sh <<'FIM'
rota() { printf '%s %s HTTP/1.0\r\nHost: x\r\nContent-Type: application/json\r\nCookie: %s\r\nContent-Length: %s\r\n\r\n%s' \
    "$1" "$2" "$(cat /tmp/cookie 2> /dev/null)" "${#3}" "$3" | openssl s_client -quiet -connect 127.0.0.1:5080 2> /dev/null; echo; }
entrar() { rm -f /tmp/cookie; ( umask 077; rota POST /session "{\"name\":\"$1\",\"password\":\"$(cat /tmp/senha-pessoa)\"}" \
    | sed -n 's/^[Ss]et-[Cc]ookie: \([^;]*\).*/\1/p' | tr -d '\r\n' > /tmp/cookie ); }
pid() { for p in /proc/[0-9]*; do tr '\0' ' ' < $p/cmdline 2>/dev/null \
  | grep -q "$1" && echo ${p#/proc/}; done; true; }
FIM
. /tmp/f.sh
entrar Ana
rota GET /session
rota GET /status
rota GET /versions
rota POST /analysis '{"text":"texto de teste","language":"pt"}'
```

`GET /session` responde 204 com sessão viva, e `/tmp/cookie` vazio é entrada que falhou. Sem
`Content-Type: application/json`, a análise responde 415 sem corpo. O `${#3}` conta caracteres, e
com acento o `Content-Length` pede bytes. Os campos do pedido e as recusas com 400: README.

## Derrubar o serviço e ler a recusa

`pid` imprime os PIDs cuja linha de comando casa com o padrão; o `[s]` e o `^` impedem que o
próprio shell case com o que procura. O núcleo é `pid '^./Core[.]Host'` (SIGTERM, em 023), e o
serviço, `pid 'language_[s]ervice'`. O `/status` demora a mudar, até 30 s:

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
503 e na linha `interrompida`, que chega um instante depois (daí a espera).

## O que parece erro e não é

O reconhecedor da Fase 1 é o vazio: `occurrences` é sempre 0 e `modelVersion` é `null`
(`ModelVersion=<absent>` no registro), até com nome de pessoa no texto. A pasta leva o modelo.
