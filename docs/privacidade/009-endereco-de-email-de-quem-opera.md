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

A administração técnica, que cadastra, e a própria pessoa, na tela da sua conta. Depois do cadastro,
a administração técnica vê e corrige o endereço de outra pessoa ativa, na página de e-mail que a
lista de pessoas abre, porque quem esqueceu a senha não entra para corrigi-lo. A correção entra na
cadeia de custódia como ato da administração sobre a pessoa, sem o endereço, e a pessoa vê o
endereço novo na própria conta. Nenhuma outra função o vê, nem em lista, nem em detalhe.

## Onde ele não aparece

Em log, em evento da cadeia de custódia e em métrica, nunca (requisito 021). A falha de um envio é
registrada sem o endereço e sem o link. A configuração em vigor mostra a credencial do servidor de
e-mail pelo nome e pela origem, nunca pelo valor.

## Na desativação

Quando a pessoa é desativada, o endereço é apagado: a finalidade acaba com a conta, porque quem está
desativado não repõe senha. O cadastro mantém o nome de uso e a função, que a cadeia de custódia
precisa para dizer quem fez o quê.

## Quem impõe

Três camadas, cada uma com o que impõe e o que acontece quando falha.

**O banco.** Uma restrição na tabela recusa o que não tem forma de endereço ou passa do tamanho, e
um gatilho apaga o endereço na mesma transação que desativa a pessoa, qualquer que seja o caminho da
desativação. Se a restrição falhar, entra um texto que não é endereço, e o envio do link falha para
aquela pessoa sem afetar as outras. Se o gatilho falhar, o endereço de quem foi desativado
fica guardado sem finalidade: é a falha que mais importa aqui, e a defesa é o teste de banco que
desativa uma pessoa e confere que o endereço sumiu.

**A aplicação.** Só a tela da própria conta, o cadastro e a página de correção da administração
mostram o campo, e o endereço vai ao banco sempre como parâmetro, nunca no texto do comando, que é
o que o log do servidor guarda. Se ela falhar, o endereço aparece a quem não devia ou entra num log.
A defesa são os testes de rota, que conferem quem vê, e o teste que procura o endereço no log do
driver, no histórico da pessoa e na cadeia.

**A configuração.** Ela recusa iniciar com o servidor de e-mail declarado em parte ou sem transporte
cifrado ([0029](../decisoes/0029-identidade-e-sessao.md)), para que o link nunca viaje em claro. Se
ela falhar, o sistema pode iniciar com um transporte em claro, e o link viajaria aberto. A defesa é
o teste da configuração, que recusa todo transporte fora de `Tls` e `StartTls`, e o envio
([#600](https://github.com/vitorparras/Conversational-Dataset-Generator/issues/600)), que só abre
conexão cifrada.
