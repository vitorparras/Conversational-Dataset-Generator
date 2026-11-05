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

Cada uso é cercado assim, e o sistema grava sozinho o que está marcado:

1. Antes: o `migrate` com passo pendente verifica a cadeia, guarda o topo e grava o evento de
   abertura (versão de partida, de chegada e topo; o papel da sessão é o "quem"). Sem passo
   pendente ou com o banco vazio não há janela; banco anterior ao 0026 recebe o evento depois dele
2. Durante: o que foi executado fica no registro de versões e no de falhas da migração
3. Depois: a partida seguinte, só com a credencial da aplicação, verifica a cadeia, confere as
   garantias da [007](007-verificacao-da-custodia.md) e grava o fechamento. Se algo faltar ou a
   cadeia recusar o evento, a partida é recusada nomeando a causa e a janela segue aberta,
   visível em `/custody/verification`
4. Continua sendo processo humano: guardar a credencial, decidir quem a usa e conferir o motivo

## O dono também declara o domínio

A política de linha das tabelas de conteúdo vale contra o dono
([0006](../decisoes/0006-dominios-coexistem.md)): sem declarar um domínio, a credencial de dono não
vê linha alguma. Ela pode declarar um, como a aplicação, para migrar ou restaurar dado de um
domínio; cada uso é janela registrada, como qualquer outro.

## Por que a janela importa depois

A [verificação da custódia](../privacidade/005-verificacao-da-custodia.md) distingue acidente de
adulteração em parte pela existência de uma janela operacional que explique o dano. Migração sem
registro é dano sem explicação, e dano sem explicação tem a assinatura de adulteração — feita por
quem tinha privilégio, que é exatamente o que essa credencial concede.

Registrar a janela protege, portanto, quem fez a migração. É o único documento que separa um
trabalho legítimo de uma suspeita que não se consegue afastar depois.
