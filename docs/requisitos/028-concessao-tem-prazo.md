# 028 — Concessão de acesso tem prazo

> Norma vigente. 2026-10-07.

**Toda concessão de acesso e toda autorização de reversão têm prazo de validade declarado, e
expiram sozinhas.**

## Por que existe

Acesso concedido sem prazo sobrevive à razão que o justificou. A pessoa conclui a participação, muda
de função, deixa a instituição — e continua alcançando material sensível até alguém lembrar de
revogar.

Lembrar é o problema. Revogação depende de alguém notar que a razão acabou, e ninguém acompanha isso
como tarefa. Prazo inverte o ônus: o acesso acaba por omissão, e continuar exige ato.

O mesmo vale para a autorização de reverter um pseudônimo. Autorização concedida para um caso não
pode servir a outro meses depois.

## Critérios de aceitação

**CA-1** — *Dado* uma concessão com prazo vencido, *quando* a pessoa tentar alcançar o material,
*então* o acesso é negado como se nunca tivesse existido, e a tentativa é registrada.

**CA-2** — *Dado* uma concessão próxima do vencimento, *quando* o prazo se aproximar, *então* quem
concedeu é avisado, com tempo de renovar antes da interrupção.

**CA-3** — *Dado* uma renovação, *quando* ela for feita, *então* registra quem renovou, quando, e
por qual novo prazo — renovação é concessão nova, não extensão silenciosa da anterior.

**CA-4** — *Dado* uma autorização de reversão, *quando* o prazo dela vencer, *então* ela não
autoriza mais nada, ainda que o caso que a motivou continue aberto.

## O que não exige

Não exige prazo uniforme: cada concessão declara o seu, e a política de acesso diz os limites.

Não exige revogação automática do registro — o histórico de que a pessoa teve acesso permanece, e é
isso que permite auditar quem alcançou o quê e quando.
