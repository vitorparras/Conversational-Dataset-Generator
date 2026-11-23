# 0058 — Padrão de usabilidade e visual das telas

> Decisão vigente. 2026-11-23.

## Contexto

As regras de tela são técnicas: acessibilidade, teclado e confirmação em tela própria
([0051](0051-navegacao-gestao-e-relatorio.md)), componentes e tokens
([0052](0052-montagem-das-telas.md)), a [revisão manual](../arquitetura/022-revisao-manual-de-acessibilidade.md)
e os roteiros que medem tempo e passos. Nenhuma limita texto, define hierarquia ou manda recolher o
secundário. O pedido de "usabilidade primeiro" ficou no contexto da 0052 (linha 10), e "um
primário por tela" só existe num comentário de código (`core/src/Core.Host/Screens/Button.razor`,
linha 11). As telas saíram com texto explicativo longo, painéis empilhados e ações do mesmo peso.
Os protótipos da fase 5 foram refeitos sem isso.

## Decisão

**Toda tela nova, a partir da fase 5, segue oito regras:**

1. Um propósito por tela, e o título o diz em até cinco palavras.
2. Texto mínimo: sem parágrafo explicativo; abaixo do título, no máximo uma linha de situação, de
   até 90 caracteres; frases de até 15 palavras. Regra de negócio vai recolhida ou fica na
   documentação.
3. Uma ação principal, preenchida; as outras, com borda ou como link, depois dela. Perigo só para o
   que não se desfaz. Quem desenha a tela escolhe o tipo de cada botão.
4. O essencial na frente, o resto recolhido: quem fez e quando, detalhes do registro e o filtro de
   lista curta.
5. Sem caixas empilhadas: o painel agrupa só o que precisa de borda, nunca dois seguidos; o espaço
   separa os blocos.
6. Poucos tons: o acento para ação e link, as cores de aviso para o retorno de uma ação; a situação
   de um registro é uma marca, nunca um aviso.
7. Aviso de uma frase: o que houve e, se houver, o que fazer.
8. Campo com rótulo curto; dica só quando o formato não é óbvio; "todos os campos são obrigatórios"
   uma vez, e não em cada campo.

O PR de tela traz capturas em 390x844 e 1366x768, no tema do sistema, e quem revisa confere as
regras nelas ([tela](../arquitetura/padroes/004-tela.md)). As telas que já existem são ajustadas
depois da fase 5, uma issue por tela.

## Alternativas recusadas

- **Ajustar todas as telas agora:** misturaria a fase 5 com reforma visual, e o risco de quebrar
  texto que os testes conferem ficaria espalhado por PRs de outro assunto.
- **Deixar a regra no comentário do componente:** ninguém a confere na revisão, e as telas mostram
  que ela não é seguida.

## Consequências

As telas novas e as antigas convivem com duas aparências até os ajustes. **Piora:** o PR de tela
fica mais caro, com capturas e mais uma conferência na revisão. As classes que os protótipos
propõem entram em `screens.css` só quando a fatia de tela precisar delas, como componente
(0052).

## O que reabre esta decisão

Um roteiro de usabilidade que mostre a tarefa mais lenta ou com mais erro por causa de uma das
regras; ou uma tela cuja tarefa não caiba nelas sem perder função.
