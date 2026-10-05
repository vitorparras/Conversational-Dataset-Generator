# 005 — Backup

> Operacional. 2026-10-04.

O backup deste sistema carrega mais do que parece. O texto bruto e o mapa de reversão saem cifrados,
porque a aplicação os cifra antes de gravar, mas o texto sanitizado, as anotações, as métricas e o
registro de auditoria estão em claro no banco — e saem em claro na cópia. O conjunto de backup é
material sensível do acervo, não arquivo de infraestrutura.

## O que é salvo

- O banco inteiro, incluindo o registro de auditoria e a cadeia de custódia
- O arquivo de taxonomia de cada domínio, na versão em uso — sem ele, as anotações restauradas
  apontam para rótulos que ninguém sabe mais o que significam
- A configuração da instalação, menos os segredos
- O topo da cadeia no momento da cópia, anotado **fora** do conjunto de backup, como o registro
  externo que a [cadeia de custódia](../privacidade/004-cadeia-de-custodia.md) exige

A chave não entra no backup, em nenhuma versão. Guardá-la junto do banco anula a cifra em repouso:
quem levar o conjunto leva dado e chave na mesma caixa.

## Onde

Em lugar distinto do que guarda a cópia da chave ([002](002-guarda-da-chave.md)), cifrado em
trânsito e em repouso por chave de finalidade própria, com acesso registrado. Retenção maior que o
tempo que se leva para perceber um problema: conjunto que só cobre a última semana não serve contra
corrupção descoberta no mês seguinte.

A separação não é zelo extra. É o que mantém de pé o requisito de que
[uma cópia do armazenamento não expõe nada](../requisitos/003-copia-do-armazenamento-nao-expoe.md),
que vale para o conjunto de backup como vale para o banco.

## Por que a cabeça da cadeia vai para fora

Um conjunto que contém a cadeia e também o seu topo esperado prova apenas consistência interna. Quem
puder reescrever o conjunto reescreve as duas coisas de forma coerente, e a verificação passa. O
valor anotado fora — com data, em registro que não se sobrescreve, sob guarda de quem responde pelo
acervo e não de quem administra o banco — é o que transforma a verificação em prova. Sem ele,
[007](007-verificacao-da-custodia.md) atesta coerência, não integridade.

Onde esse registro externo mora é pendência declarada na política de custódia. Esta operação
responde pela parte que lhe cabe: quem o guarda, e que não seja a mesma pessoa que administra o
banco de onde ele viria.

## Testar é parte de salvar

Backup não testado é hipótese. A cada trimestre, e a cada mudança no formato de persistência, uma
cópia é restaurada em ambiente separado e conferida pelo procedimento de
[006](006-restauracao.md). O resultado — passou ou falhou — entra no registro de custódia. Falhar
em restaurar é incidente, não pendência de melhoria.
