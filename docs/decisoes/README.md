# Decisões

> Descrição. Índice e formato das decisões.

Cada decisão é um arquivo. **Mudar de ideia é escrever um arquivo novo que substitui o antigo** —
nunca editar o texto do antigo. O substituído permanece, com a linha de autoridade atualizada.

Isso preserva o histórico de cada mudança de rumo, que é o que demonstra que cada virada foi
deliberada e justificada.

## Índice

| # | Decisão | Autoridade |
|---|---|---|
| [0001](0001-as-duas-contribuicoes.md) | As duas contribuições do projeto | Decisão vigente |
| [0002](0002-stack.md) | A stack | Decisão vigente |
| [0003](0003-cifra-em-repouso.md) | Cifra em repouso | Substituída por 0030 na tabela |
| [0004](0004-taxonomia-configuravel.md) | A taxonomia é configuração, não código | Decisão vigente |
| [0005](0005-unidades-de-analise.md) | As unidades de análise | Decisão vigente |
| [0006](0006-dominios-coexistem.md) | Domínios coexistem; a exportação é que recorta | Decisão vigente |
| [0007](0007-garantia-de-qualidade.md) | A garantia de qualidade dos testes | Decisão vigente |
| [0008](0008-requisito-no-repositorio-tarefa-na-issue.md) | Requisito no repositório, tarefa na issue | Decisão vigente |
| [0009](0009-licenca-do-software-e-dos-dados.md) | Licença do software e licença dos dados | Decisão vigente |
| [0010](0010-tecnologia-da-interface.md) | A tecnologia da interface | Decisão vigente |
| [0011](0011-protocolo-com-o-servico-de-linguagem.md) | O protocolo com o serviço de linguagem | Substituída por 0018 na resposta |
| [0012](0012-pseudonimizacao-reversivel.md) | Pseudonimização reversível, não anonimização | Decisão vigente |
| [0013](0013-chave-de-cifra-por-dominio.md) | Chave de cifra por domínio | Decisão vigente |
| [0014](0014-custodia-da-chave-mestra.md) | Custódia e recuperação da chave mestra | Decisão vigente |
| [0015](0015-versionamento-e-compatibilidade.md) | Versionamento e compatibilidade | Decisão vigente |
| [0016](0016-mudanca-de-taxonomia-com-anotacao-existente.md) | Mudança de taxonomia com anotação existente | Decisão vigente |
| [0017](0017-dupla-anotacao-e-arbitragem.md) | Dupla anotação e arbitragem | Decisão vigente |
| [0018](0018-contrato-da-fronteira.md) | O contrato da fronteira carrega a análise gramatical | Decisão vigente |
| [0019](0019-selo-externo-da-cadeia.md) | Onde mora o selo externo da cadeia | Decisão vigente |
| [0020](0020-exportacao-desloca-datas.md) | A exportação desloca as datas | Decisão vigente |
| [0021](0021-comunicacao-de-incidente.md) | A quem se comunica um incidente | Decisão vigente |
| [0022](0022-forma-do-rotulo.md) | A forma do rótulo: duas dimensões, com principal | Decisão vigente |
| [0023](0023-suficiencia-do-recorte.md) | Suficiência do recorte exportado | Decisão vigente |

## Formato

Nome `NNNN-slug.md`. Autoridade `Decisão vigente`. Seções:

1. **Contexto** — o que torna a decisão necessária, com a origem citada
2. **Decisão** — uma frase imperativa
3. **Alternativas recusadas** — cada uma com o motivo da recusa
4. **Consequências** — o que passa a ser verdade, **incluindo o que piora**
5. **O que reabre esta decisão** — a condição que, se acontecer, obriga reavaliar

A seção 5 é a que mais falta na prática e a que mais importa num projeto longo: transforma "mudei
de ideia" em "a condição que eu mesmo escrevi se realizou".

Ver [regras de escrita](../regras-de-escrita.md).
