# Documentação

> Descrição. Mapa das pastas; o conteúdo de cada uma está no README dela.

**Esta pasta é a fonte da verdade do projeto.** O que o sistema deve garantir, o que foi decidido e
por quê, e como ele funciona vive aqui. As tarefas para chegar nisso vivem nas issues.

| Pasta | O que vive lá |
|---|---|
| [`requisitos/`](requisitos/) | O que o sistema garante, e por quê |
| [`decisoes/`](decisoes/) | O que foi decidido, as alternativas recusadas e o que reabre cada decisão |
| [`arquitetura/`](arquitetura/) | Como o sistema é construído |
| [`privacidade/`](privacidade/) | Tratamento de dado pessoal, custódia e acesso |
| [`operacao/`](operacao/) | Como rodar, implantar, salvar e verificar |
| [`pesquisa/`](pesquisa/) | Metodologia, métricas, protocolo de anotação, bibliografia |
| [`roadmap/`](roadmap/) | A ordem em que as garantias entram, e o que cada inversão custaria |
| [`historico/`](historico/) | Registro do que já aconteceu. **Autoridade zero** |

Antes de escrever qualquer documento, leia [regras de escrita](regras-de-escrita.md).

## Quando duas fontes discordam

1. Decisão vigente em `decisoes/`
2. Requisito em `requisitos/` e política em `privacidade/`
3. `roadmap/` — vincula **só na ordem**: nunca no que o sistema garante. Ordem que exija
   contrariar os dois níveis acima é erro do roadmap, não deles
4. Issues
5. `arquitetura/`, `operacao/`, `pesquisa/` — descrevem, não mandam
6. `historico/` — nunca manda

A lista ordena pastas, não resolve empate dentro do nível 1. **Duas decisões vigentes que se
contradigam são defeito, não hierarquia a interpretar**: uma delas tem de ser substituída, pelo que
o [README das decisões](decisoes/README.md) já manda — arquivo novo, nunca edição do antigo. Até
que isso aconteça, nenhuma das duas está resolvida por ser mais recente.

**Divergência não se resolve sozinha.** Abra uma issue de divergência citando os dois arquivos.
