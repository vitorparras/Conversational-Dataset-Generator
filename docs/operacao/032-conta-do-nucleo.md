# 032 — A conta do núcleo e o registro do servidor

> Operacional. 2026-11-05.

O núcleo roda com uma conta que **não é root** e pertence ao grupo que lê o registro do servidor de
banco ([001](001-ambiente.md)). O ambiente diz qual é a conta, o grupo e a pasta; os exemplos de
[021](021-subir-o-nucleo-publicado.md) a [025](025-certificado-do-canal-cifrado.md) e de
[028](028-criar-primeira-pessoa.md) usam `nucleo`, `postgres` e `/var/log/postgresql`. Todo bloco
desses documentos roda como ela, e `/opt/cdg` e `/etc/cdg` são dela. Num contêiner operado de fora,
isso é o `-u` do `docker exec`.

## O que a partida recusa

A pasta do registro (`Audit:ServerLogDirectory`) é conferida na partida, e a recusa sai com código
69 na conferência `ServerLog`, dizendo o que viu:

- **Sem arquivo `.csv`:** o servidor não escreve o registro nela.
- **Ilegível** (acesso negado): a conta não está no grupo da pasta.
- **Gravável:** a conta pode gravar onde só devia ler. Root grava numa pasta comum, e por isso o
  núcleo não roda como root; numa montagem só de leitura, nem root grava.

## Conferir antes de iniciar

Com a conta do núcleo, antes de [023](023-iniciar-o-nucleo-publicado.md): o grupo da pasta tem de
estar na primeira linha, o `.csv` tem de aparecer, e a última linha tem de dizer "não gravável".

```
id -nG
ls -ld /var/log/postgresql
ls /var/log/postgresql/*.csv
test -w /var/log/postgresql && echo "gravável: a partida recusa" || echo "não gravável, como deve ser"
```

O servidor escreve a pasta com modo `0750` e os arquivos com `0640`, e a conta só lê pelo grupo.
