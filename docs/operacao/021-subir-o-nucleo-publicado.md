# 021 — Subir o núcleo publicado

> Operacional. 2026-10-30.

Para quem tem o executável, a pasta do serviço de [018](018-pasta-do-servico.md), um banco com o
esquema de [016](016-migracao-do-esquema.md) e o que [001](001-ambiente.md) lista. Usa só o que o
Linux traz (bash, `/proc`) e o `openssl`: `ps` e cliente SQL não são dependência do artefato.

Os blocos de 021 a 024 são comandos de um shell na máquina. Se a máquina é um contêiner
operado de fora, cada bloco vai inteiro em `docker exec -i <contêiner> bash -s <<'EOF'`, com um
`EOF` sozinho na última linha; no Git Bash do Windows, ponha `MSYS_NO_PATHCONV=1` antes do
`docker`, ou ele reescreve caminhos como `/tmp/...`.

**O roteiro é o de uma demonstração.** Os blocos usam os valores do ambiente do exemplo, a trocar
pelos do seu (001): banco `cdg` em `db:5432`, papéis `owner` e `app`, artefato em `/opt/cdg`, núcleo
em `127.0.0.1:5080`. `FICHA` e `CONTEINER` são marcadores: o arquivo da ficha (no Git Bash serve o
caminho do Windows com barras normais) e o nome do contêiner. Numa instalação real, `real` troca
`demonstracao` em `--Installation:Kind`, e valem [024](024-arquivos-de-custodia-da-chave.md),
[025](025-certificado-do-canal-cifrado.md) e o cerco de [010](010-credencial-de-dono-do-banco.md).

## A pasta do serviço fica dentro da pasta do executável

O núcleo procura `SHA256SUMS` em `language-service/`, **dentro** da pasta do executável. Pasta irmã
não vale: a partida recusa com `manifesto ausente`, dando só o caminho relativo, sem a pasta-base.

```
cp -a /opt/cdg/language-service /opt/cdg/nucleo/language-service
```

## Configurar

As chaves vão por argumento, `--Seção:Chave=valor` (porta e caixa dos nomes: README). A senha vai
por variável de ambiente, nunca por argumento, e os blocos a leem de um arquivo de modo 600 por
papel (`/tmp/senha-owner`, `/tmp/senha-app`). O `sed` é exemplo para a ficha do exemplo, com linhas
`Papel dono ...: Credencial: <senha>` (para a `senha-app`, o padrão é `/^Papel da /`); com outro
cofre, grave a senha no mesmo arquivo e modo.

```
( umask 077; sed -n '/^Papel dono/s/.*Credencial: //p' FICHA > /tmp/senha-owner )
```

Com a ficha fora do contêiner, a senha vai pela entrada do `docker exec -i` (a entrada é ela):

```
sed -n '/^Papel dono/s/.*Credencial: //p' FICHA \
  | docker exec -i CONTEINER bash -c 'umask 077; cat > /tmp/senha-owner'
```

## Migrar o esquema

A partida só confere o esquema; antes dela, o `migrate` com o papel dono, como em 016. A senha do
dono só existe durante a migração (010): o bloco a apaga e mostra a saída (0 aplicou, 10 em dia).
Na demonstração não há cadeia a verificar: registre quem migrou, quando e a saída.

```
( export CDG_Database__Password="$(cat /tmp/senha-owner)"; cd /opt/cdg/nucleo \
  && ./Core.Host migrate --Database:Host=db:5432 --Database:Name=cdg --Database:Username=owner; \
  echo "saída=$?" )
rm -f /tmp/senha-owner
```
