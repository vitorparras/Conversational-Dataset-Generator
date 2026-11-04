# 0051 — Navegação, gestão e relatório

> Decisão vigente. 2026-11-04.

## Contexto

A [0010](0010-tecnologia-da-interface.md) fixou a tecnologia e a tela de anotação, e deixou a
família de navegação, gestão e relatório sem forma. O dono quer o sistema inteiro com tela: a
linha de comando fica só com a gestão do servidor (instalação, cópia, chave, ensaio de
recuperação). As funções vêm da [política de acesso](../privacidade/006-acesso.md); a sessão, da
[0029](0029-identidade-e-sessao.md).

## Decisão

**A navegação é por função**: a pessoa vê só o menu das funções que tem, e a página inicial é o
que ela tem a fazer. Uma rota fora das funções dela responde como rota que não existe: o texto
não distingue "negado" de "inexistente" (requisito 017), vale para item de lista e para acesso
direto por endereço.

**As listas de gestão têm filtro e paginação no servidor**, com ambos no endereço, para que voltar e
compartilhar funcionem. O tamanho da página é fixo e declarado na tela; não há rolagem infinita
nem lista virtualizada nelas. O teto da unidade de anotação, em aberto na 0010, não é fechado aqui.

**Todo formulário valida no servidor e devolve o mesmo formulário com os erros**, sem perder o que
foi digitado. Ação que não se desfaz pede confirmação numa tela própria que diz o que será
afetado, não num diálogo do navegador.

**O ato de duas pessoas mostra os dois lados**: quem propõe vê o pedido pendente e quem falta;
quem aprova vê o que aprova, e nunca a própria proposta. O que a tela mostra do pedido é só o que
a política de acesso permite àquela função.

**Sessão expirada leva à entrada e, depois dela, de volta à página pedida**, nunca a uma ação
repetida. A camada de cliente da 0010 não ganha regra de decisão por causa destas telas.

**O nível de acessibilidade é WCAG 2.1 AA** em toda tela, com operação completa só por teclado,
foco visível e movido de propósito após a troca de região, erro ligado ao campo e anunciado, e
nenhuma informação só por cor, inclusive na anotação.

**O teste de navegador usa o Playwright para .NET**, na CI, sem interface. Cobre o teclado e a troca
parcial da 0010, a navegação acima e a verificação automática de acessibilidade de cada tela nova.

## Alternativas recusadas

| Opção | Por quê |
|---|---|
| Menu único, com a negativa na hora do clique | Revela que a tela existe e dá à pessoa um beco sem saída |
| Rolagem infinita ou lista virtualizada nas listas de gestão | Perde a página no endereço e exige estado de cliente |
| Diálogo do navegador para confirmar | Não se estiliza, não se testa e não diz o que será afetado |
| WCAG AAA | Não é atingível para todo conteúdo e não vira critério verificável |

## Consequências

Tela nova segue estes padrões ou declara a exceção. A CI passa a baixar um navegador, o que a
deixa mais lenta. A verificação automática acha só parte dos problemas de acessibilidade; o
resto fica com quem revisa.

## O que reabre esta decisão

Se o teste de navegador não rodar estável na CI, ou uma função exigir lista que a paginação do
servidor não atenda, a decisão é refeita por escrito.
