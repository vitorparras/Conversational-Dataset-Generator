# 022 — Conferir o núcleo publicado

> Operacional. 2026-10-30.

Depois de [023](023-iniciar-o-nucleo-publicado.md): consultar as rotas, derrubar o serviço de
linguagem e ler o resultado, sem cliente HTTP, `ps` nem cliente SQL na máquina. Cada `docker exec`
abre um shell novo: grave as duas funções abaixo em `/tmp/f.sh`, de fora, pela entrada do
`docker exec -i`, e leia-o com `. /tmp/f.sh` em cada chamada.

## Consultar as rotas

```
docker exec -i CONTEINER bash -c 'cat > /tmp/f.sh' <<'EOF'
rota() { exec 3<>/dev/tcp/127.0.0.1/5080
  printf '%s %s HTTP/1.0\r\nHost: x\r\nContent-Type: application/json\r\nContent-Length: %s\r\n\r\n%s' \
    "$1" "$2" "${#3}" "$3" >&3; cat <&3; exec 3<&-; }
pid() { for p in /proc/[0-9]*; do tr '\0' ' ' < $p/cmdline 2>/dev/null \
  | grep -q "$1" && echo ${p#/proc/}; done; true; }
EOF
docker exec -i CONTEINER bash <<'EOF'
. /tmp/f.sh
rota GET /status
rota GET /versions
rota POST /analysis '{"text":"texto de teste","language":"pt"}'
EOF
```

Dentro do contêiner basta o conteúdo entre os `EOF`, num shell que leia o `/tmp/f.sh`.

Sem o `Content-Type: application/json`, a rota de análise responde 415 sem corpo. `${#3}` conta
caracteres, e com acento a conta de bytes é outra. `language` aceita qualquer texto não nulo: o
núcleo o repassa, e o reconhecedor da Fase 1 o ignora. `resumes`, se presente, é a `reference` de
uma recusa, em formato de GUID; outro valor, `"x"` por exemplo, recusa o pedido com 400 e **sem
corpo**. Também recusa com 400 o campo desconhecido, ou `text` e `language` ausentes.

## Achar o processo e derrubar o serviço

`pid` imprime os PIDs cuja linha de comando casa com o padrão. O `[s]` e o `^` impedem que o
próprio shell que procura case com o que procura:

```
pid '^./Core[.]Host'        # o núcleo, para o SIGTERM de 023
pid 'language_[s]ervice'    # o serviço de linguagem; depois: kill -9 N
```

O registro de partida também traz `Serviço de linguagem iniciado (processo N, ...)`. Derrubado o
serviço, `/status` mostra `languageService` `Unavailable` e `ready` falso, e `/analysis` responde
503 com `reason` e `cause`. Na Fase 1 nenhuma tabela recebe análise: "nada gravado" se lê no corpo
do 503 e no registro (`interrompida`). Conferir o banco pede cliente SQL, na máquina de quem opera
o banco.

## O que parece erro e não é

O reconhecedor da Fase 1 é o vazio, de propósito: `occurrences` é sempre 0, `kinds` vem vazio e
`modelVersion` vem `null` (`ModelVersion=<absent>` no registro), mesmo com nome de pessoa no texto.
A pasta já leva o modelo, mas nada o carrega até um reconhecedor com modelo substituir o vazio.
