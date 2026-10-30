# 021 — Subir o núcleo publicado

> Operacional. 2026-10-30.

Para quem tem o executável, a pasta do serviço de [018](018-pasta-do-servico.md), um banco com o
esquema de [016](016-migracao-do-esquema.md) e o que [001](001-ambiente.md) lista. Usa só o que o
Linux traz (bash, `/proc`): cliente HTTP, `ps` e cliente SQL não são dependência do artefato.
Iniciar: [023](023-iniciar-o-nucleo-publicado.md); conferir: [022](022-conferir-o-nucleo-publicado.md).

Os blocos de 021, 022 e 023 são comandos de um shell na máquina. Quando a máquina é um contêiner
operado de fora, cada bloco vai inteiro em `docker exec -i <contêiner> bash -s <<'EOF'`, o bloco e
um `EOF` sozinho na última linha, o que dispensa aspas aninhadas; no Git Bash do Windows, ponha
`MSYS_NO_PATHCONV=1` antes do `docker`, ou ele reescreve caminhos como `/tmp/...`.

## A pasta do serviço fica dentro da pasta do executável

O núcleo procura o manifesto `SHA256SUMS` em `language-service/`, **dentro** da pasta do próprio
executável. Pasta irmã não vale: a partida recusa com `manifesto ausente`, sem dizer onde procurou.

```
cp -a /opt/cdg/language-service /opt/cdg/nucleo/language-service
```

## Configurar

Chaves comuns vão por argumento, `--Seção:Chave=valor`. A porta do banco vai em `Database:Host`
(`servidor:5432`). Sem `Host:Urls`, o núcleo escuta em 5000. Nomes de chave e de variável não
distinguem maiúsculas: as grafias de 016 e do README são a mesma chave.

A senha vai por variável de ambiente do processo, nunca por argumento, na partida normal e no modo
`migrate`. Os blocos a leem de um arquivo de modo 600 por papel, `/tmp/senha-owner` e
`/tmp/senha-app`, gravado a partir da ficha (`FICHA`) sem digitar a senha; para a `senha-app`,
o padrão do `sed` é `/^Papel da /`. Com a ficha na máquina:

```
( umask 077; sed -n '/^Papel dono/s/.*Credencial: //p' FICHA > /tmp/senha-owner )
```

Com a ficha fora do contêiner, a senha vai pela entrada do `docker exec -i`, a única forma que não
é o bloco inteiro, porque a entrada é a senha:

```
sed -n '/^Papel dono/s/.*Credencial: //p' FICHA \
  | docker exec -i CONTEINER bash -c 'umask 077; cat > /tmp/senha-owner'
```

## Migrar o esquema

A partida só confere o esquema; antes dela, o modo `migrate` com o papel dono, como em 016.

```
( export CDG_Database__Password="$(cat /tmp/senha-owner)"; cd /opt/cdg/nucleo \
  && ./Core.Host migrate --Database:Host=db:5432 --Database:Name=cdg --Database:Username=owner )
```

## O cerco da credencial numa demonstração

O cerco de [010](010-credencial-de-dono-do-banco.md) e 016 (verificar a cadeia antes e depois,
registrar a janela) vale para instalação real. Na demonstração da Fase 1 não há cadeia a
verificar; vale registrar quem migrou, quando e o código de saída, e a saída do `migrate` o faz.
