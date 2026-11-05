# 021 — Subir o núcleo publicado

> Operacional. 2026-10-30.

Para quem tem o executável, a pasta do serviço de [018](018-pasta-do-servico.md), um banco com o esquema de [016](016-migracao-do-esquema.md) e o que [001](001-ambiente.md) lista. Usa só o que o Linux traz
(bash, `/proc`) e o `openssl`: `ps` e cliente SQL não são dependência do artefato. A ordem:

1. 021 (este): pasta do serviço, senhas e `migrate`.
2. [024](024-arquivos-de-custodia-da-chave.md) e [025](025-certificado-do-canal-cifrado.md), em qualquer ordem: a custódia da chave e o certificado.
3. [028](028-criar-primeira-pessoa.md): a primeira pessoa (num contêiner operado de fora, a forma é a de [033](033-primeira-pessoa-numa-maquina-operada-de-fora.md)).
4. [023](023-iniciar-o-nucleo-publicado.md): iniciar o núcleo e esperar que fique pronto.
5. [022](022-conferir-o-nucleo-publicado.md): entrar, consultar e derrubar o serviço; o encerramento é o de 023.

Os blocos são comandos de um shell na máquina, como a conta de [032](032-conta-do-nucleo.md). De fora, cada
um vai inteiro em `docker exec -i -u <conta> <contêiner> bash -s <<'EOF'`, com `EOF` sozinho no fim
(no Git Bash, `MSYS_NO_PATHCONV=1` antes do `docker`, ou ele reescreve `/tmp/...`).

**O roteiro é o de uma demonstração.** Os blocos usam o ambiente do exemplo, a trocar pelo seu
(001): banco `cdg` em `db:5432`, papéis `owner` e `app`, artefato em `/opt/cdg`, núcleo em
`127.0.0.1:5080`. `FICHA`, `CONTEINER` e `CONTA` são marcadores (a ficha, no Git Bash, com o caminho do Windows). Numa instalação real, `real` troca `demonstracao` em `--Installation:Kind`, e valem [024](024-arquivos-de-custodia-da-chave.md), [025](025-certificado-do-canal-cifrado.md) e [010](010-credencial-de-dono-do-banco.md).

## A pasta do serviço fica dentro da pasta do executável

O núcleo procura `SHA256SUMS` em `language-service/`, **dentro** da pasta do executável. Pasta irmã
não vale: a partida recusa com `manifesto ausente`, dando só o caminho relativo, sem a pasta-base.

```
cp -a /opt/cdg/language-service /opt/cdg/nucleo/language-service
```

## Configurar

As chaves vão por argumento, `--Seção:Chave=valor` (porta e caixa: README). A senha vai por variável
de ambiente, nunca por argumento, lida de um arquivo de modo 600 por papel (`/tmp/senha-owner`,
`/tmp/senha-app`). O `sed` serve à ficha do exemplo (`Papel dono ...: Credencial: <senha>`); com
outro cofre, grave a senha no mesmo arquivo e modo:

```
# dentro da máquina, com a ficha nela:
( umask 077; sed -n '/^Papel dono/s/.*Credencial: //p' FICHA > /tmp/senha-owner )
( umask 077; sed -n '/^Papel da /s/.*Credencial: //p' FICHA > /tmp/senha-app )
# de fora, com a ficha fora do contêiner:
sed -n '/^Papel dono/s/.*Credencial: //p' FICHA \
  | docker exec -i -u CONTA CONTEINER bash -c 'umask 077; cat > /tmp/senha-owner'
sed -n '/^Papel da /s/.*Credencial: //p' FICHA \
  | docker exec -i -u CONTA CONTEINER bash -c 'umask 077; cat > /tmp/senha-app'
```

## Migrar o esquema

A partida só confere o esquema; antes dela, o `migrate` com o papel dono, como em 016. A senha do
dono só existe durante a migração (010): o bloco a apaga e mostra a saída (0 aplicou, 10 em dia).
Na demonstração, registre quem migrou, quando e a saída.

```
( export CDG_Database__Password="$(cat /tmp/senha-owner)"; cd /opt/cdg/nucleo \
  && ./Core.Host migrate --Database:Host=db:5432 --Database:Name=cdg --Database:Username=owner; \
  echo "saída=$?" )
rm -f /tmp/senha-owner
```
