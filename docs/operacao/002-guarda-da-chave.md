# 002 — Guarda da chave

> Operacional. 2026-10-04.

A cifra em repouso é feita pela aplicação, e a chave nunca chega ao banco. O efeito colateral é que
a chave passa a ser parte do acervo: quem a perde perde o texto bruto e a capacidade de reverter a
pseudonimização, em definitivo.

A [decisão 0014](../decisoes/0014-custodia-da-chave-mestra.md) fixa o essencial — a chave é
fornecida ao processo na inicialização, de fora do artefato, e antes do primeiro dado real existem
uma cópia de guarda e uma recuperação testada, sem as quais a ingestão não começa. Aqui está o lado
operacional: onde a cópia mora, quem responde por ela, e o que se confere depois.

## A cópia de guarda

Fora da máquina que roda o sistema, em meio cifrado, com a custódia declarada: quem detém, onde, e
quem pode pedir. Guarda, junto da chave, **os rótulos de derivação** — a
[decisão 0013](../decisoes/0013-chave-de-cifra-por-dominio.md) deriva as chaves de domínio da mestra
pelo rótulo, e rótulo perdido é dado ilegível com a chave em mãos.

O conjunto de rótulos só cresce: acrescentar domínio cria rótulo, e o rótulo entra na custódia no
mesmo ato. Cópia atualizada só quanto à chave fica desatualizada em silêncio. Na máquina que serve,
a chave e os rótulos moram em dois arquivos ([024](024-arquivos-de-custodia-da-chave.md)).

Duas exigências se somam às da decisão, e nenhuma a contraria:

- **A cópia não mora onde mora o backup.** Juntas, as duas devolvem o acervo em claro a quem levar
  a caixa — é o requisito de
  [cópia que não expõe](../requisitos/003-copia-do-armazenamento-nao-expoe.md) visto do lado da
  salvaguarda
- **Quem detém a cópia não guarda a credencial do banco.** É separação de pessoas, e a
  [política de acesso](../privacidade/006-acesso.md) depende dela para afirmar que quem administra o
  banco obtém bytes

## Partir a chave entre pessoas foi recusado

A decisão 0014 recusa quórum: protegeria contra custódia desonesta e criaria indisponibilidade a
cada ausência, que não é o risco deste acervo. Quem desenhar a guarda não reintroduza isso por
conta — o custo aparece no primeiro reinício fora de hora, e a chave acaba anotada em lugar pior.

A cópia é, ela mesma, um alvo, e a decisão aceita a troca de olhos abertos: perda permanente do
acervo é pior que o acréscimo de superfície. O que a torna aceitável é a cópia ser cifrada e a
custódia, declarada.

## Demonstração não é instalação real

Instalação de demonstração roda sem cópia de guarda e sem recuperação testada, e a diferença precisa
ser visível a quem opera — no nome da instalação, na tela e no registro. Sem isso alguém ingere
material de verdade onde nunca houve guarda, e a descoberta vem no dia em que a chave falta.

## O que é auditado, e o que não dá para auditar

Decifrar o mapa de reversão revela identidade e é o único caminho de volta: cada operação dessas
fica no registro, com a finalidade declarada. Acesso à cópia sem operação correspondente é achado
para [008](008-incidente-vazamento.md).

O sistema **não tem como verificar se a custódia está sendo cumprida**, porque a cópia está fora
dele. Daí a revisão trimestral, e a cada troca de quem detém: a cópia está onde a custódia diz, quem
detém sabe que detém, quem pode pedir ainda é quem deveria. A revisão entra em
[007](007-verificacao-da-custodia.md), e é o que separa custódia de suposição.
