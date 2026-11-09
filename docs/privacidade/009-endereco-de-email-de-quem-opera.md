# 009 — O endereço de e-mail de quem opera

> Norma vigente. 2026-11-09.

O cadastro de quem opera o sistema guarda o nome de uso e a função. Com a
[#635](https://github.com/vitorparras/Conversational-Dataset-Generator/issues/635), ele pode guardar
também um endereço de e-mail. É dado pessoal de quem opera, não do material, e esta política diz
para que ele serve, quem o vê e quando deixa de existir.

## Para que serve

Para uma coisa só: levar o link de reposição de senha ([#600](https://github.com/vitorparras/Conversational-Dataset-Generator/issues/600))
quando a instalação declara um servidor de e-mail. Nenhuma outra mensagem vai para ele. O campo é
opcional: sem ele, ou sem servidor declarado, a reposição é entregue por fora do sistema
(requisito 022), e o sistema funciona inteiro.

## Quem vê

A administração técnica, que cadastra, e a própria pessoa, na tela da sua conta. Nenhuma outra
função o vê, nem em lista, nem em detalhe.

## Onde ele não aparece

Em log, em evento da cadeia de custódia e em métrica, nunca (requisito 021). A falha de um envio é
registrada sem o endereço e sem o link. A configuração em vigor mostra a credencial do servidor de
e-mail pelo nome e pela origem, nunca pelo valor.

## Na desativação

Quando a pessoa é desativada, o endereço é apagado: a finalidade acaba com a conta, porque quem está
desativado não repõe senha. O cadastro mantém o nome de uso e a função, que a cadeia de custódia
precisa para dizer quem fez o quê.

## Quem impõe

A aplicação, no cadastro e na conta, que só mostram o campo a quem o vê, e no registro, que não
recebe o endereço. A configuração recusa iniciar com o servidor de e-mail declarado em parte ou sem
transporte cifrado ([0029](../decisoes/0029-identidade-e-sessao.md)), para que o link nunca viaje em
claro.
