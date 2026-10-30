# 021 — Subir o núcleo publicado

> Operacional. 2026-10-30.

Para quem tem o executável, a pasta do serviço de [018](018-pasta-do-servico.md), um banco com o
esquema de [016](016-migracao-do-esquema.md) e o que [001](001-ambiente.md) lista. Usa só o que o
Linux traz (bash, `/proc`): cliente HTTP, `ps` e cliente SQL não são dependência do artefato, e a
máquina de quem opera pode não os ter. Iniciar: [023](023-iniciar-o-nucleo-publicado.md);
conferir o que subiu: [022](022-conferir-o-nucleo-publicado.md).

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
`migrate`. Com terminal, `read -rs`, como em 016. Sem terminal (`ssh` sem tty, `docker exec`),
grave-a por entrada padrão num arquivo de modo 600, um por papel, e leia-o no shell que inicia o
processo; `docker exec -e` a poria na linha de comando do `docker`, visível na máquina que o roda.

Dentro do contêiner, com terminal (a senha do papel da aplicação vai em `senha-app`, do mesmo modo):

```
umask 077; cat > /tmp/senha-owner          # senha do papel dono, depois Ctrl-D
```

De fora, sem terminal, a senha vai pela entrada do `docker exec -i`. Guarde-a antes numa variável
do seu shell (`read -rs SENHA_DONO`): o `printf` é interno ao shell e não a põe na lista de
processos. Sem quebra de linha no fim, que o `$(cat ...)` do uso descartaria de qualquer forma.

```
printf '%s' "$SENHA_DONO" | docker exec -i CONTEINER bash -c 'umask 077; cat > /tmp/senha-owner'
```

## Migrar o esquema

A partida só confere o esquema; antes dela, o modo `migrate` com o papel dono, como em 016. Iniciar
o núcleo está em [023](023-iniciar-o-nucleo-publicado.md).

```
( export CDG_Database__Password="$(cat /tmp/senha-owner)"; cd /opt/cdg/nucleo \
  && ./Core.Host migrate --Database:Host=db:5432 --Database:Name=cdg --Database:Username=owner )
```

## O cerco da credencial numa demonstração

O cerco de [010](010-credencial-de-dono-do-banco.md) e 016 (verificar a cadeia antes e depois,
registrar a janela) vale para instalação real. Na demonstração da Fase 1 não há cadeia a
verificar, então a verificação não se aplica; registrar quem migrou, quando e o código de saída
continua valendo, e a saída do modo `migrate` é o registro.
