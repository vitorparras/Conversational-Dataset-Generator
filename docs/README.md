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
| [`historico/`](historico/) | Registro do que já aconteceu. **Autoridade zero** |

Antes de escrever qualquer documento, leia [regras de escrita](regras-de-escrita.md).

## Quando duas fontes discordam

1. Decisão vigente em `decisoes/`
2. Requisito em `requisitos/` e política em `privacidade/`
3. Issues
4. `arquitetura/`, `operacao/`, `pesquisa/` — descrevem, não mandam
5. `historico/` — nunca manda

**Divergência não se resolve sozinho.** Abra uma issue de divergência citando os dois arquivos.
