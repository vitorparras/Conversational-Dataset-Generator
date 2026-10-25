# 012 — Dependências e análise estática

> Operacional. 2026-10-07.

O código depende de bibliotecas que ninguém aqui escreveu, e uma falha nelas atinge o sistema sem
violar nenhuma regra deste repositório.

## Por que importa aqui

A cifra que sustenta a garantia de que uma cópia do armazenamento não expõe nada é implementada por
dependência. O mesmo vale para a leitura de formatos de arquivo, que é a superfície que recebe
conteúdo de fora.

Falha nessas peças não aparece em revisão de pull request: o código próprio continua correto.

## O que roda a cada mudança

**Verificação de dependência vulnerável**, sobre a árvore completa — não só o que foi declarado
diretamente. Vulnerabilidade conhecida em dependência de dependência conta igual.

**Análise estática de segurança** sobre o código próprio, procurando o que revisão humana cansa de
procurar: concatenação em consulta, caminho construído com entrada externa, uso de função
criptográfica inadequada, segredo escrito no código.
As quatro famílias são regras próprias em `tools/verify-code/semgrep`, nas duas linguagens; no
serviço, as regras de segurança do `ruff` (`S`) somam-se a elas. Trecho de `core/src` que o
analisador não consegue ler tem teto declarado no passo `static_analysis` e não pode crescer.

As duas reprovam a mudança. Dependência vulnerável sem correção disponível exige decisão escrita de
aceitar o risco, com prazo para revisitar — não silêncio.

Achado da análise estática que, examinado, não é defeito se dispensa no próprio ponto, com o
motivo ao lado, nunca em `core/src` ([decisão 0042](../decisoes/0042-supressao-de-analisador.md));
no serviço, o `# noqa` de regra `S` também traz o motivo.
Regra desligada para o repositório inteiro reprova: deixar de procurar uma classe de defeito é
decisão, não configuração.

## O que roda periodicamente

A verificação de dependência também roda fora de qualquer mudança, porque vulnerabilidade é
descoberta depois de a dependência já estar instalada. Código que não mudou pode ficar vulnerável
sem ninguém tocar nele.

## Atualização

Dependência se atualiza por mudança própria, revisada, não por atualização automática aplicada sem
leitura. O que a atualização muda no comportamento é responsabilidade de quem a propõe.
