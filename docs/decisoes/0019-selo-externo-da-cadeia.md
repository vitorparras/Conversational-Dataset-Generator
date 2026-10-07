# 0019 — Onde mora o selo externo da cadeia

> Decisão vigente. 2026-10-07.

## Contexto

A cadeia encadeia cada evento ao resumo do anterior, então alterar um evento no meio quebra os elos
seguintes e a verificação acusa. O que ela não detecta é a substituição da base inteira por outra,
coerente consigo mesma: a nova também tem uma corrente válida, só que é outra.

Detectar isso exige ter registrado, fora da base, o resumo do último evento — o selo. A cadeia de
custódia declara essa necessidade e deixa em aberto onde ele mora.

O selo é pequeno — data, contagem de eventos e o resumo — e não carrega conteúdo do acervo: se
carregasse, ele próprio seria vazamento.

## Decisão

**Nada que esteja sob o mesmo controle da base conta como selo.** Outro contêiner do mesmo arranjo,
outro volume do mesmo host, outra base no mesmo servidor: em todos, quem substitui a base substitui
o selo na mesma operação. Vale como regra, independentemente do ambiente — selo interno com
aparência de externo é pior que selo nenhum, porque produz garantia que não existe.

**Onde o selo mora depende do que o ambiente oferece**, e a instalação escolhe entre três situações,
registrando qual adotou:

Com saída para fora da instituição, o selo é enviado automaticamente a um destino que a instalação
não controla, diariamente.

Com rede interna apenas, vai para outra máquina da rede, sob controle de quem não administra o
banco. Protege contra restauração equivocada e contra quem alcança só o banco; não contra quem
controla a rede.

Sem rede, não existe selo automático externo: resta transporte manual, em papel assinado com data
ou em arquivo copiado para fora, e a frequência cai para o que uma pessoa sustenta.

**Em qualquer das três, o selo é registrado antes de cada exportação de dataset**, porque é o
momento em que a integridade do que sai passa a importar para quem recebe.

## Recusados

**Serviço de selo dentro do mesmo arranjo de contêineres.** É a saída que parece resolver e não
resolve, e é a mais provável de ser adotada por conveniência — por isso está recusada por escrito.

**Exigir um único arranjo para todas as instalações.** Fixar saída externa inviabilizaria ambiente
isolado, que é cenário real para material sensível; fixar papel desperdiçaria automação onde ela é
possível.

## Consequências

A instalação declara qual situação adotou, e a verificação da custódia informa contra o quê ela
protege naquele ambiente — em vez de afirmar integridade sem qualificar.

Em ambiente sem rede, há uma janela entre registros manuais em que a verificação atesta coerência,
não integridade. É consequência do ambiente, não do desenho, e precisa estar dita a quem opera.

## O que reabre

Se o ambiente de produção for definido e for um só, a decisão pode fixar o arranjo em vez de
descrever três.
