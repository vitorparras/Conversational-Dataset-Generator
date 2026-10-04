# 0001 — A ferramenta é a contribuição

> Decisão vigente. 2026-10-04.

**O que é:** a decisão que define o que este trabalho entrega como contribuição.
**A quem serve:** quem precisa decidir se um investimento em generalidade se justifica.
**O que não cobre:** como a reutilização é implementada — isso vem de decisões posteriores.

## Contexto

Este projeto constrói bases conversacionais anotadas, e adota o domínio forense como seu caso mais
exigente. Daí nasce uma ambiguidade que precisa ser resolvida por escrito, porque as duas leituras
possíveis levam a decisões de design **opostas**:

| Se a contribuição é | Então |
|---|---|
| **O dataset** | Toda escolha se subordina à qualidade dele. Investimento em generalidade é desperdício, e cortar é a atitude correta |
| **A ferramenta** | Generalidade é requisito. O dataset é a prova de que a ferramenta funciona |

Enquanto a ambiguidade existir, cada decisão posterior a herda: quem precisar desempatar escolhe
sozinho, em silêncio, sem que a escolha fique registrada. É assim que um projeto passa a ter dois
rumos ao mesmo tempo sem ninguém perceber.

## Decisão

**A contribuição deste trabalho é a ferramenta: um pipeline reutilizável para construção de bases
conversacionais anotadas. O dataset forense é o caso de validação mais restritivo dessa ferramenta,
não o produto final.**

Três razões, na ordem em que pesam:

1. **O que se transfere é o método, não o dado.** Um dataset serve ao domínio que o originou; o
   pipeline que o produz serve a qualquer domínio que precise do mesmo tipo de garantia.
2. **Um corpus anotado envelhece; o procedimento de construí-lo, não.** Dados perdem atualidade,
   mudam de regime legal e deixam de poder ser distribuídos. O método sobrevive a isso.
3. **Funcionar em mais de uma base é o que separa método de caso isolado.** Um pipeline validado
   numa base só não demonstra ser pipeline — demonstra ter resolvido aquela base.

A pergunta que passa a governar as decisões de design é:

> Isso serve a mais de um domínio sem perder a garantia que o domínio mais restritivo exige?

Duas travas deliberadas na formulação:

- Generalidade é requisito, mas **generalidade que custa garantia é recusada**. A ferramenta não
  fica mais permissiva para ficar mais geral.
- O domínio forense é a régua. O que não sobrevive às exigências de cadeia de custódia, privacidade
  e auditoria não entra, ainda que sirva a outros domínios.

## Alternativas recusadas

### A contribuição é o dataset; generalidade fica como trabalho futuro

**Recusada.** Tem um efeito que costuma passar despercebido: uma norma que subordina tudo à
qualidade de um dataset faz com que **qualquer pessoa ou agente que a leia conclua, corretamente,
que generalidade é corte**. Manter uma regra e esperar comportamento contrário a ela é pedir que se
ignore o que está escrito.

Também deixa o trabalho sem resposta para a pergunta mais previsível que se pode fazer a ele: o
pipeline funciona fora da base que o autor construiu?

### A ferramenta é tudo; o dataset é incidental

**Recusada.** Descarta a razão pela qual as garantias existem. Sem um domínio que exija cadeia de
custódia, pseudonimização reversível sob autorização e auditoria de acesso, a ferramenta perde o que
a distingue de um utilitário de anotação qualquer. Precisa haver ao menos um caso exigente, ou não
há como saber se as garantias funcionam.

### Duas linhas de trabalho paralelas, uma para a ferramenta e outra para o dataset

**Recusada.** Duplica esforço e cria duas fontes de verdade sobre o mesmo pipeline. Duas descrições
do mesmo fato divergem com o tempo, e a divergência só aparece quando alguém age sobre a versão
errada.

## Consequências

### O que passa a ser verdade

- **A taxonomia do domínio não pode estar fixada no código.** Se as categorias forem um tipo usado
  em assinaturas de função, ou se um gate de validação iterar sobre elas, uma instalação em outro
  domínio não passa no próprio gate do pipeline. Decisão específica sobre isso ainda será escrita.
- **Relatórios e métricas são capacidade do produto**, não subproduto. A ferramenta precisa relatar
  qualidade de anotação por base e comparar bases, porque é assim que se demonstra que ela funciona
  fora de uma base só.
- **Investimento em fronteiras explícitas e módulos independentes é justificado** pela norma, não
  apesar dela.

### O que piora, e é aceito

- **Mais superfície para manter.** Generalidade custa pontos de extensão, e cada ponto de extensão é
  código que precisa de teste. O prazo não melhora com esta decisão — piora, e isso é consciente.
- **Risco de generalidade especulativa.** Uma base que serve para tudo é difícil de projetar e fácil
  de errar. Vale como contrapeso permanente a esta decisão: **generalidade se justifica por um
  segundo caso concreto, nunca por hipótese**.
- **O escopo precisa ser delimitado explicitamente** em três faixas: o que é implementado e
  validado, o que é implementado sem validação formal, e o que é trabalho futuro declarado. Sem
  isso, a ampliação de escopo vira fragilidade em vez de contribuição.

## O que reabre esta decisão

1. **A validação acadêmica do reposicionamento não se confirmar.** Esta decisão interpreta o
   objetivo do trabalho; se a interpretação não for aceita, ela cai, e com ela a justificativa de
   boa parte do que vem depois.
2. **Nenhum segundo domínio ser identificado até o início da fase de anotação.** Generalidade sem
   segundo caso concreto é especulação, e o risco registrado acima passa a pesar mais que esta
   decisão.
3. **O prazo estreitar ao ponto de a ferramenta geral inviabilizar a entrega do caso de validação.**
   Nesse caso a ordem se inverte: entrega-se o caso restritivo, e a generalidade vira trabalho
   futuro declarado — por uma decisão sucessora, nunca por omissão.
