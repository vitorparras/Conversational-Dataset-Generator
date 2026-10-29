# 0043 — Quem escuta na porta prova ter o segredo

> Decisão vigente. 2026-10-29, direção aprovada pelo dono na issue #402.

## Contexto

A [0011](0011-protocolo-com-o-servico-de-linguagem.md) fecha o transporte à máquina local e protege
o serviço com um segredo gerado na inicialização, mandado em `x-service-secret` a cada pedido. O
segredo diz ao serviço quem fala; nada diz ao núcleo quem escuta. Um processo que tome a porta, a de
um serviço que morreu ou a de uma partida ainda sem dono, recebe o segredo e o texto anterior à
sanitização, o único dado pessoal que cruza a fronteira. O serviço agora abre a própria porta e o
núcleo recusa falar com serviço morto (issues #397 e #403), mas resta o intervalo entre ler o estado
e conectar.

## Decisão

**O serviço prova que tem o segredo antes de recebê-lo.** A rota `GET /version` não pede o segredo.
Dado um nonce em `x-service-nonce` (hexadecimal minúsculo, 16 a 64 bytes), responde em
`x-service-proof` o HMAC-SHA256 do nonce, chaveado pelo segredo. O núcleo, a cada conexão nova, pede
a prova de um nonce de 32 bytes aleatórios, a compara em tempo constante e só então manda o segredo
ou o texto por essa conexão. Prova ausente ou errada fecha a conexão e conta como serviço
inalcançável; nonce malformado leva 400 sem prova. As outras rotas seguem exigindo o segredo.

## Alternativas recusadas

- **Só o anúncio da porta pelo cano do processo** (#397): prova de onde veio a porta na partida, não
  de quem atende a conexão que depois a usa.
- **Segredo no próprio `/version`**: entrega o segredo ao estranho antes de conferi-lo.
- **TLS com certificado do serviço**: o transporte é local por decisão da 0011, e certificado pede
  distribuição e validade; a prova usa um segredo que já existe.
- **Prova por pedido, fora da conexão**: deixa a janela entre a prova e o pedido, que pode ir por
  outra conexão.

## Consequências

Substitui a 0011 quanto à autenticação do transporte; a 0011 só troca a linha de autoridade. Cada
conexão nova custa uma ida e volta. Pedido sem segredo, ou com segredo errado, deixa de receber 401
e vira falha de conexão. O `/version` sem segredo mostra as três versões a quem alcance a porta
local, e a prova nada vale contra quem já leia a memória do núcleo ou do serviço.

## O que reabre esta decisão

Se o serviço sair da máquina local, condição da 0011, a prova não basta e entra transporte
autenticado e cifrado. Se o segredo passar a mudar dentro de um mesmo processo, a prova de uma
conexão deixa de valer para o segredo novo.
