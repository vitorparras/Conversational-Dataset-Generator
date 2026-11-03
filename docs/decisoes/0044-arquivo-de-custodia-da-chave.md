# 0044 — A chave chega por arquivo de custódia

> Decisão vigente. 2026-10-30, decisões D1, D2 e D3 do dono na issue #53. Emendada em 2026-11-01
> pela [0049](0049-chave-por-item-e-epoca.md): o terceiro arquivo.

## Contexto

A [0014](0014-custodia-da-chave-mestra.md) manda fornecer a chave-mestra ao processo na partida,
de fora do artefato, e a [0013](0013-chave-de-cifra-por-dominio.md) deriva as chaves de domínio
pelo rótulo, que só cresce. Faltava o meio concreto de entrega e o que o sistema faz com a
custódia que não consegue verificar.

## Decisão

**A chave, os rótulos e as épocas vêm de três arquivos, e a configuração leva os caminhos, não a
chave.**

- `MasterKey:File` aponta o arquivo da chave: uma linha com 32 bytes em base64, com ou sem quebra
  no fim. `MasterKey:LabelsFile` aponta o dos rótulos: UTF-8 sem BOM, um rótulo por linha.
  `MasterKey:EpochsFile` aponta o das épocas: uma por linha, `<n>:<base64 de 32 bytes>`, com `n`
  inteiro positivo e único. Os três são obrigatórios ao servir, sem valor padrão, e o `migrate`
  não os exige. O terceiro segue as regras de lugar e de permissão dos outros dois.
- Os arquivos ficam fora da pasta do artefato, e o núcleo recusa o que estiver dentro dela, também
  por ligação de pasta. Ficar longe do diretório do banco é exigência de operação, porque o núcleo
  não enxerga o disco do servidor ([001](../operacao/001-ambiente.md)).
- O núcleo recusa permissão mais aberta que a do dono do processo. No Linux, qualquer bit de grupo
  ou de outros. No Windows, qualquer permissão concedida a quem não é o usuário do processo,
  SYSTEM ou Administradores. A recusa diz o que viu, nunca o conteúdo.
- Instalação real declara `Custody:LastRecoveryTest`, uma data ISO, e `Custody:Holder`, quem
  detém a cópia. Sem eles a partida recusa e nomeia o que falta. A demonstração não os exige. A
  declaração sai na linha de partida; o responsável é apagado do registro, como toda identidade.
  A trava da ingestão fica na issue de ingestão.

## Alternativas recusadas

- **A própria chave numa variável de ambiente.** O serviço de linguagem, filho do núcleo, herdaria
  o ambiente, e ela apareceria na leitura do ambiente do processo.
- **Cofre do sistema operacional.** Traz uma dependência diferente em cada plataforma nesta fase.
- **Um arquivo só, com chave e rótulos.** Foi a recomendação inicial; o dono escolheu dois
  arquivos, e a época, que se destrói todo mês, ganhou o terceiro.

## Consequências

O arquivo é um segredo no disco da máquina que serve. A permissão do dono é a única barreira local,
e o sistema não vê onde fica o disco do banco. A custódia segue sem verificação automática: a
declaração é auditável, não provada. Os caminhos só aparecem na recusa da partida, que não serve
rota alguma, e o conferido que passa nomeia as chaves de configuração, não os caminhos. Nem os
caminhos nem o responsável aparecem em `/configuration` ou em `/status`.

## O que reabre esta decisão

Se o ambiente passar a ter um meio que entregue a chave sem arquivo e sem herança pelo filho, ou
se a instalação sair de um único usuário de sistema, a entrega por arquivo deixa de ser a menor
superfície.
