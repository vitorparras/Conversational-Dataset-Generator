# 024 — Arquivos de custódia da chave

> Operacional. 2026-10-30.

Os dois arquivos que a [decisão 0044](../decisoes/0044-arquivo-de-custodia-da-chave.md) manda o
núcleo ler na partida: `MasterKey:File` (a chave, uma linha com 32 bytes em base64) e
`MasterKey:LabelsFile` (os rótulos de derivação, UTF-8 sem BOM, um por linha). A configuração leva
os caminhos, nunca a chave. Vem antes de [023](023-iniciar-o-nucleo-publicado.md).

## Onde moram

Fora da pasta do executável: o núcleo recusa o que estiver dentro dela. Fora também do diretório do
banco e do lugar do [backup](005-backup.md), que o núcleo não enxerga: quem leva o banco ou o
backup não pode levar a chave junto. Só o dono do processo tem acesso. No Linux, modo 600 no
arquivo e 700 na pasta. No Windows, só o usuário do processo, SYSTEM e Administradores. Permissão
mais aberta recusa a partida, e a recusa diz o que viu.

## Criar numa demonstração

A demonstração não tem cópia de guarda, então a chave pode ser descartável. Os valores são os do
exemplo de [021](021-subir-o-nucleo-publicado.md), a trocar pelos do seu ambiente. Sem rótulos
usados, o arquivo de rótulos nasce vazio:

```
( umask 077; mkdir -p /etc/cdg/custodia; cd /etc/cdg/custodia \
  && head -c 32 /dev/urandom | base64 > chave && : > rotulos )
```

No Windows, tire a herança do arquivo e deixe só o usuário (`icacls ARQUIVO /inheritance:r
/grant:r "%USERNAME%:F"`).

## Instalação real

A chave e os rótulos vêm da cópia de guarda ([002](002-guarda-da-chave.md)), nunca de um sorteio
novo: chave nova sem os rótulos antigos deixa o dado antigo ilegível, e o arquivo de rótulos leva o
conjunto completo. Além dos arquivos, a instalação real declara quando a recuperação foi testada
pela última vez e quem detém a cópia, e sem isso a partida recusa e nomeia o que falta:

```
--Custody:LastRecoveryTest=AAAA-MM-DD --Custody:Holder=QUEM_DETEM
```

O sistema não verifica a declaração (002). A data sai na linha de partida do registro, e o nome é
apagado dele. A demonstração não declara nada disso.

## Quando a partida recusa

- **`arquivo ... não encontrado`**: o caminho está errado ou o arquivo não foi criado
- **`permissão aberta` ou `permissão concedida`**: feche o acesso como acima
- **`dentro da pasta do artefato`**: mova o arquivo para fora da pasta do executável e de
  `/opt/cdg`, senão atualizar o artefato troca a pasta e leva a chave junto
- **`tamanho inválido`**: a chave decodificada não tem 32 bytes, e a recusa diz quantos tem
- **`não é base64` ou `linhas`**: o arquivo da chave tem de ter uma linha só
- **`linha N`**: o arquivo de rótulos tem um rótulo vazio ou com espaço em volta nessa linha
