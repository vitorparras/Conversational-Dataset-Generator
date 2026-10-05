# 0008 — Requisito no repositório, tarefa na issue

> Decisão vigente. 2026-10-04.

## Contexto

O repositório é a fonte da verdade: descrição, funcionalidade e requisito vivem nele, e as tarefas
para chegar nisso vivem nas issues. O mapa em `docs/README.md` já diz isso, mas não diz como os dois
lados ficam ligados — e sem elo explícito aparecem os dois defeitos simétricos: issue que inventa
escopo que nenhum requisito pede, e requisito que ninguém está atendendo sem que isso se perceba.

## Decisão

**Toda issue de trabalho cita, no corpo, o documento que a origina, pelo número e nome do arquivo.**
Nenhum documento do repositório lista as issues que o realizam.

A ida é a citação dentro da issue. A volta é busca pelo identificador do documento nas issues, não
uma lista mantida à mão. Um requisito que nenhuma issue cita é requisito sem trabalho em curso, e a
busca mostra isso sem que ninguém precise manter nada atualizado — enquanto uma lista dentro do
documento mostraria o que era verdade na última vez que alguém se lembrou de editá-la.

Issue sem documento de origem existe em duas formas, e só nessas: a que **entrega** o documento,
quando escrever o requisito é o próprio trabalho, e a de defeito ou divergência, que cita o
documento violado em vez do que a origina.

O pull request cita a issue e herda dela a origem. Cada mudança de código chega ao requisito em dois
saltos, e cada requisito chega às mudanças em uma busca.

## Recusados

| Opção | Por quê |
|---|---|
| Requisito listando suas issues | A lista envelhece a cada issue aberta ou fechada, e o documento passa a mudar por motivo que não é mudança de garantia |
| Requisito escrito dentro da issue | Issue fechada sai da leitura corrente; quem clona o repositório fica sem a garantia |
| Rastreabilidade em ferramenta à parte | Fora do repositório: quem clona não consulta, e o elo vale o que vale o acesso de quem pergunta |
| Elo por etiqueta de issue | Etiqueta diz o assunto, não qual requisito; e etiqueta renomeada apaga o elo de tudo que já passou |

## Consequências

Abrir issue fica mais caro: antes de descrever a tarefa é preciso achar o documento de origem, ou
escrever um. Em trabalho pequeno isso incomoda, e a tentação é abrir sem origem — que é exatamente o
que esvazia a decisão.

O elo é texto, e texto erra. Identificador citado errado não quebra nada: desaparece em silêncio, e
só se nota quando alguém procura o trabalho de um requisito e não encontra.

Documento renomeado invalida a citação das issues já fechadas. É a razão de o número nunca ser
reutilizado, e de o documento substituído manter o número mudando apenas a linha de autoridade.

## O que reabre

Se o volume de issues tornar a busca pelo identificador imprecisa a ponto de não responder "este
requisito tem trabalho em curso?", o elo passa a precisar de algo que a ferramenta mantenha, e não
de disciplina de quem escreve.
