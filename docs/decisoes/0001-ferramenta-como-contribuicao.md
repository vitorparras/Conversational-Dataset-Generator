# 0001 — A ferramenta é a contribuição

> Decisão vigente. 2026-10-04.

**O que é:** a decisão que define o que este trabalho entrega como contribuição.
**A quem serve:** quem precisa decidir se um investimento em generalidade se justifica.
**O que não cobre:** como a reutilização é implementada — isso vem de decisões posteriores.

## Contexto

O planejamento anterior governava toda decisão de design por uma única pergunta:

> Isso protege a qualidade do dataset? Se não protege, corta. Velocidade, modularidade, elegância
> arquitetural — tudo está subordinado a este critério.

Essa regra era coerente com o escopo de maio de 2026: produzir **um** dataset forense anotado. Sob
ela, relatórios analíticos estavam fora de escopo, e qualquer investimento em generalidade era corte
legítimo.

A reunião de orientação de 2026-10-01 reposicionou o trabalho. A ata registra, na seção 2, o pedido
de que a solução possa ser reutilizada em outros contextos além do criminal; e cobra, no resumo, a
delimitação entre o que será efetivamente implementado e o que será validado.

A análise arquitetural de 2026-10-04 mediu o efeito de manter a regra antiga sob a direção nova: das
dez tensões identificadas entre a ata e a norma vigente, **seis eram de norma, não de desenho** — e
a regra acima era o critério de desempate invocado por quase todas as outras. A conclusão registrada
foi que, pela norma vigente, todo investimento em generalidade seria corte legítimo, e **qualquer
pessoa ou agente que lesse a norma concluiria isso corretamente**.

A norma não falhava por omissão: ela impedia ativamente a direção nova. Um repositório novo que não
a substituísse herdaria o impedimento intacto.

## Decisão

**A contribuição deste trabalho é a ferramenta: um pipeline reutilizável para construção de bases
conversacionais anotadas. O dataset forense é o caso de validação mais restritivo dessa ferramenta,
não o produto final.**

A pergunta que governa as decisões de design passa a ser:

> Isso serve a mais de um domínio sem perder a garantia que o domínio mais restritivo exige?

Duas consequências da formulação, ambas deliberadas:

- Generalidade deixa de ser desperdício e passa a ser requisito — mas **generalidade que custa
  garantia continua recusada**. A ferramenta não fica mais permissiva para ficar mais geral.
- O domínio forense mantém o papel de régua. O que não sobrevive às exigências de cadeia de
  custódia, privacidade e auditoria não entra, ainda que sirva a outros domínios.

## Alternativas recusadas

### Manter o foco no dataset e tratar generalidade como trabalho futuro

**Recusada.** É o estado anterior, e seu efeito foi medido: o requisito mais central da ata fica não
atendido, e quem lê a norma conclui corretamente que generalidade é corte. Manter uma regra e
esperar comportamento contrário a ela é pedir que as pessoas ignorem o que está escrito.

### Inverter por completo: a ferramenta é tudo, o dataset é incidental

**Recusada.** Descarta a razão pela qual as garantias existem. Sem um domínio que exija cadeia de
custódia, pseudonimização reversível sob autorização e auditoria de acesso, a ferramenta perde o que
a distingue de um utilitário de anotação qualquer. A ata também pede validação em mais de uma base,
o que exige que ao menos uma seja exigente.

### Duas linhas de trabalho paralelas, uma para a ferramenta e outra para o dataset

**Recusada.** Duplica esforço num trabalho com prazo apertado, e cria duas fontes de verdade sobre o
mesmo pipeline — o problema estrutural que motivou recomeçar o projeto.

## Consequências

### O que passa a ser verdade

- **A taxonomia do domínio não pode estar fixada no código.** No projeto anterior, as categorias
  criminais eram um tipo usado em assinaturas de função, e um gate de validação iterava sobre elas
  sendo bloqueante de exportação, de modo que uma instalação em outro domínio não passava no
  próprio gate do pipeline. Decisão específica sobre isso ainda será escrita.
- **Relatórios e métricas passam a ser capacidade do produto**, não subproduto. A ferramenta precisa
  relatar qualidade de anotação por base e comparar bases, porque é assim que se demonstra que ela
  funciona fora de uma base só.
- **Investimento em fronteiras explícitas e módulos independentes é justificado** pela norma, não
  apesar dela.

### O que piora, e é aceito

- **Mais superfície para manter.** Generalidade custa pontos de extensão, e cada ponto de extensão é
  código que precisa de teste. O prazo não melhora com esta decisão — piora, e isso é consciente.
- **Risco de generalidade especulativa.** A ata registra, na seção 8, a advertência do orientador de
  que uma base que serve para tudo é difícil e precisa ser bem pensada. Essa advertência vale como
  contrapeso permanente: generalidade se justifica por um segundo caso concreto, nunca por hipótese.
- **A dissertação precisa delimitar explicitamente** o que é ferramenta implementada, o que é
  ferramenta validada e o que é trabalho futuro. Sem isso, a ampliação de escopo vira fragilidade na
  defesa em vez de contribuição.

## O que reabre esta decisão

1. **O orientador discordar do reposicionamento.** A ata registra o pedido de reutilização, mas a
   inversão formal entre dataset e ferramenta é interpretação desta decisão, e precisa de
   confirmação explícita antes da qualificação.
2. **Nenhum segundo domínio ser identificado até o início da fase de anotação.** Generalidade sem
   segundo caso concreto é especulação, e a advertência da ata passa a pesar mais que esta decisão.
3. **O prazo estreitar ao ponto de a ferramenta geral inviabilizar a entrega do dataset.** Nesse
   caso a ordem se inverte: entrega-se o caso restritivo, e a generalidade vira trabalho futuro
   declarado — por uma decisão sucessora, nunca por omissão.
