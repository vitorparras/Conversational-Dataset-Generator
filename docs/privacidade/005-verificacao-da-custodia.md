# 005 — O regime de verificação da custódia

> Norma vigente. 2026-10-04.

Cadeia que ninguém confere não é custódia — é a afirmação de que houve custódia. Esta política fixa
quando a conferência acontece, o que acontece quando ela falha, e como se separa acidente de
adulteração. Que a conferência detecte o que deveria detectar é garantia observável, com critérios,
e pertence ao requisito correspondente; não se repete aqui.

Três coisas distintas podem ter acontecido a uma cadeia, e nenhuma delas aparece pelo mesmo exame:
alteração no meio da sequência, base inteira substituída por outra coerente consigo mesma, e remoção
no fim — esta última a única que não deixa elo quebrado para trás. Daí duas consequências que são
desta política: conferir não é uma operação só, e o registro externo do topo descrito em
[cadeia de custódia](004-cadeia-de-custodia.md) não é redundância.

## Quando acontece

Na subida do sistema, periodicamente sem depender de ninguém pedir, e **antes de toda exportação.**
Esta última bloqueia: cadeia que não confere não exporta, porque dataset derivado de acervo não
conferido não tem procedência — e procedência é o que ele oferece.

Também sob demanda, antes de qualquer ato que se apoie no material.

Periodicidade mínima diária é escolha desta política; nenhuma decisão vigente a fixa. Quem roda, com
que credencial e onde fica o resultado é assunto de [operação](../operacao/).

## Quando quebra, o sistema para de escrever

O domínio afetado deixa de aceitar ingestão, anotação e exportação. Leitura continua, porque
investigar exige ler. Abre-se incidente, e o estado de incidente é ele próprio um evento na cadeia.

**O que não se faz em nenhuma hipótese é refazer a cadeia para que volte a fechar.** Isso não
conserta nada: apaga a única evidência existente sobre o que aconteceu e produz um acervo que afirma
integridade que não tem.

## Acidente e adulteração têm assinaturas diferentes

Acidente é contíguo e datado. O dano cobre um intervalo, coincide com algo que aconteceu de fato —
restauração de backup, migração, falha de disco, queda durante gravação —, o conteúdo dos eventos
continua lá, íntegro, só o elo faltando, e o registro operacional do ambiente explica a janela.

Adulteração é limpa e sem janela. Os sinais são estes: o conteúdo de um evento difere do que o
registro externo atesta para aquele ponto; falta um evento e a cadeia ainda assim fecha; a quebra
está num item específico e não num intervalo; nenhum incidente operacional explica a data.

Há uma inversão que vale enunciar, porque contraria a intuição: **cadeia que fecha depois de um dano
é mais grave que cadeia que não fecha.** Refazer elo exige desabilitar gatilho, o que exige papel de
dono — ou seja, aponta para quem tinha privilégio, não para falha de disco.

## Quem conclui é pessoa

O sistema classifica evidência; não decide se houve má-fé. A conclusão é escrita por quem responde
pelo acervo, com o que foi observado, e entra na cadeia como evento.

Acervo cuja quebra foi classificada como adulteração não volta a exportar sem decisão escrita de
quem responde pelo projeto. E o que saiu antes da quebra precisa ser localizado: o registro de
exportação diz o que foi gerado, quando e para quem, e é a única via de avisar quem recebeu material
cuja procedência deixou de se sustentar.
