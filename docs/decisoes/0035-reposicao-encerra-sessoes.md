# 0035 — A reposição de senha encerra as sessões

> Decisão vigente. 2026-10-07. Completa a decisão 0029.

## Contexto

A [decisão 0029](0029-identidade-e-sessao.md) estabelece que não há recuperação de senha por e-mail
quando o ambiente é isolado, e que a reposição é administrativa e registrada. O que ela não diz é o
que acontece com as sessões que já estão abertas naquela conta.

A omissão importa porque a reposição tem dois motivos possíveis, e eles pedem tratamentos opostos.
Alguém esqueceu a senha: as sessões abertas são legítimas. Alguém suspeita que a conta foi
comprometida: as sessões abertas podem ser de quem invadiu.

## Decisão

**Repor a senha encerra, na hora, todas as sessões ativas daquela pessoa.** A invalidação é do lado
do servidor: não basta o navegador esquecer.

A reposição registra quem repôs, quando, para quem, e quantas sessões foram encerradas pelo ato.

## Recusados

**Manter as sessões abertas até expirarem.** Interromperia menos quem apenas esqueceu a senha, e
tornaria a reposição inútil no caso que mais importa: se a conta foi comprometida, quem já estava
dentro continua dentro até o prazo da sessão vencer. A operação existe para cortar acesso, e uma que
não corta não é a operação.

**Deixar quem repõe escolher.** Cobriria os dois motivos com o tratamento adequado a cada um, e o
padrão errado escolhido por pressa reintroduz exatamente a falha que a decisão evita por construção.
Numa operação que acontece sob suspeita, a escolha certa não deve depender de alguém estar atento.

## Consequências

Quem pede reposição por esquecimento perde o trabalho em andamento nas outras telas. Isso é mitigado
pela preservação do rascunho de anotação
([requisito 033](../requisitos/033-rascunho-de-anotacao-sobrevive.md)), que sobrevive ao fim da
sessão — e é mais uma razão para aquele requisito existir.

A reposição passa a ser operação com efeito visível para a pessoa, não ato silencioso. Isso é
desejável: ela fica sabendo que aconteceu.
