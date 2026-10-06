# Fase 2 — Custódia e cifra

> Norma vigente. 2026-10-04.

Esta fase constrói o cofre antes de haver o que guardar, e é a razão pela qual nenhuma fase anterior
recebe material. A decisão [0003](../decisoes/0003-cifra-em-repouso.md) fixa o critério numa frase:
copiar o arquivo do banco para fora não pode expor nada. Enquanto isso não for verdade, material
real no sistema é vazamento esperando acontecer.

Entram quatro coisas. A cifra feita pela aplicação antes de gravar, com chave derivada por
finalidade declarada, rotação e revogação que não cegam o que foi cifrado com chave antiga. A cadeia
imutável de custódia, encadeada por hash sob transação serializável, com imutabilidade imposta pelo
servidor — concessão por coluna e gatilho — e não pela memória de quem escreve a aplicação. O eixo
de domínio em toda tabela de conteúdo, com política de linha e `FORCE`, conforme a decisão
[0006](../decisoes/0006-dominios-coexistem.md). E a verificação da cadeia sob demanda.

O eixo de domínio entra aqui, e não mais tarde, porque precisa existir na primeira tabela de
conteúdo que for criada. Acrescentá-lo depois é migrar dado e reescrever consulta.

Os procedimentos de guarda, rotação, perda, backup e restauração da chave já estão escritos em
`docs/operacao/`. O que esta fase deve a eles não é texto: é o ensaio.

## O que precisa estar pronto antes

A [fase 1](001-fundacao-verificavel.md). Os invariantes de custódia são verificados por propriedade,
e propriedade derivada do próprio código confirma o código contra si mesmo.

## Critério de término

1. [003](../requisitos/003-copia-do-armazenamento-nao-expoe.md) é verificado por teste que lê o
   armazenamento fora da aplicação — não por inspeção manual.
2. [019](../requisitos/019-custodia-encadeada.md) e
   [020](../requisitos/020-integridade-verificavel.md) são verificados, inclusive o caso de duas
   gravações simultâneas e o de alteração de evento passado.
3. [018](../requisitos/018-isolamento-entre-dominios.md) vale no banco, e vale também contra a
   credencial de dono da tabela, conforme `docs/operacao/010-credencial-de-dono-do-banco.md`.
4. Depois de uma rotação, o que foi cifrado com a chave anterior continua legível.
5. A restauração a partir de cópia de segurança foi ensaiada seguindo
   `docs/operacao/006-restauracao.md`, por quem não escreveu o procedimento.
6. A perda da chave foi ensaiada: o sistema a reporta como perda definitiva, em vez de degradar em
   silêncio.

## O que destrava

Material real pode entrar no sistema. Nenhuma fase seguinte precisa pedir licença para persistir
dado sensível, porque a licença está no mecanismo.
