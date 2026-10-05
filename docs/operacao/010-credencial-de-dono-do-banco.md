# 010 — A credencial de dono do banco

> Operacional. 2026-10-04.

A imutabilidade da cadeia é imposta por gatilho no servidor, e gatilho se desabilita com privilégio
de dono. A [cadeia de custódia](../privacidade/004-cadeia-de-custodia.md) reconhece isso como seu
elo mais fraco e diz que ele é processo humano, não mecanismo: a credencial existe, e o que resta é
controlar quando ela sai da gaveta. Este documento é essa gaveta.

## Onde ela vive

Sob guarda separada da chave de cifra e separada de quem administra o sistema no dia a dia. Quem
tem a credencial de dono não tem a chave, e quem tem a chave não tem a credencial — a
[política de acesso](../privacidade/006-acesso.md) só consegue afirmar que administrar o banco rende
bytes enquanto isso for verdade.

Ela não fica no cofre que o processo da aplicação alcança. Se a aplicação pode buscá-la, ela é, na
prática, a credencial da aplicação.

## Quando sai

Em migração de esquema, e em mais nada. Restauração não precisa dela se o procedimento de
[006](006-restauracao.md) recriar o banco com as garantias já no lugar; precisar de dono para
restaurar é sinal de que o procedimento está errado, não de que a regra deve ceder.

Cada uso é cercado assim:

1. Antes: rode a verificação de [007](007-verificacao-da-custodia.md) e guarde o topo da cadeia
2. Durante: a janela fica registrada — quem, quando, para quê, o que foi executado
3. Depois: verifique de novo, e confira que gatilhos, permissões de coluna e política de linha
   voltaram ativos
4. O resultado entra no registro de custódia, e a janela também

## Por que a janela importa depois

A [verificação da custódia](../privacidade/005-verificacao-da-custodia.md) distingue acidente de
adulteração em parte pela existência de uma janela operacional que explique o dano. Migração sem
registro é dano sem explicação, e dano sem explicação tem a assinatura de adulteração — feita por
quem tinha privilégio, que é exatamente o que essa credencial concede.

Registrar a janela protege, portanto, quem fez a migração. É o único documento que separa um
trabalho legítimo de uma suspeita que não se consegue afastar depois.
