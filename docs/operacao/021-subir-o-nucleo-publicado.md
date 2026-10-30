# 021 — Subir o núcleo publicado

> Operacional. 2026-10-30.

Para quem tem o executável, a pasta do serviço de [018](018-pasta-do-servico.md), um banco com o
esquema de [016](016-migracao-do-esquema.md) e o que [001](001-ambiente.md) lista. Usa só o que o
Linux traz (bash, `/proc`): cliente HTTP, `ps` e cliente SQL não são dependência do artefato, e a
máquina de quem opera pode não os ter. Conferir o que subiu está em
[022](022-conferir-o-nucleo-publicado.md). Por que a 001 não lista essas ferramentas: o artefato não instala nada no destino, e
ferramenta de quem opera é do ambiente de quem opera.

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
grave-a por entrada padrão num arquivo de modo 600 e leia-o no shell que inicia o núcleo;
`docker exec -e` a poria na linha de comando do `docker`, visível na máquina que o roda.

## Iniciar e guardar o registro

```
umask 077; cat > /tmp/senha-app            # cole a senha, depois Ctrl-D
export CDG_Database__Password="$(cat /tmp/senha-app)"
cd /opt/cdg/nucleo && ./Core.Host --Host:Urls=http://127.0.0.1:5080   --Installation:Kind=demonstracao --Database:Host=db:5432 --Database:Name=cdg   --Database:Username=app > /tmp/nucleo.log 2>&1 &
```

O registro vai ao console, e o redirecionamento o guarda; `Host:LogFile` o grava em arquivo. A
partida só confere o esquema: antes dela, o modo `migrate` com o papel dono, como em 016. Para
encerrar, SIGTERM ao processo; a saída é 0.

## O cerco da credencial numa demonstração

O cerco de [010](010-credencial-de-dono-do-banco.md) e 016 (verificar a cadeia antes e depois,
registrar a janela) vale para instalação real. Na demonstração da Fase 1 não há cadeia a
verificar, então a verificação não se aplica; registrar quem migrou, quando e o código de saída
continua valendo, e a saída do modo `migrate` é o registro.

## Os avisos da partida

`Cannot load library libgssapi_krb5.so.2` aparece antes dos registros, na partida com banco e no
`migrate`, quando a biblioteca de autenticação Kerberos não está na máquina. O cliente do banco a
tenta e segue sem ela; a conexão usa a senha. Instalá-la é opcional (ver 001).
