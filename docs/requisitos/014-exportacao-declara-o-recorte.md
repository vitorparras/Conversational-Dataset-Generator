# 014 — A exportação declara o recorte de que saiu

> Norma vigente. 2026-10-04.

**Todo dataset exportado traz, no próprio artefato, de quais domínios saiu, sob quais taxonomias,
com quais contagens e de qual estado do acervo.**

## Por que existe

A [decisão 0006](../decisoes/0006-dominios-coexistem.md) põe o recorte na saída e não na
instalação, e tira daí a consequência: sem a declaração no artefato, um dataset de conjunto misto é
indistinguível de um de origem única. A
[decisão 0004](../decisoes/0004-taxonomia-configuravel.md) acrescenta que a taxonomia vigente
integra a identidade do que foi exportado, porque anotações feitas sob taxonomias diferentes não são
comparáveis.

O artefato é a única coisa que sobrevive fora do sistema. O que não estiver nele não existe para
quem o recebe.

## Critérios de aceitação

- Dado uma exportação concluída, quando o artefato é aberto, então ele declara os domínios, as
  taxonomias com versão, as contagens por forma de material e por categoria, e o instante de corte.
- Dado dois artefatos exportados, quando se pergunta se um contém o outro, então os recortes
  declarados bastam para responder sem consultar o sistema de origem.
- Dado o mesmo acervo e o mesmo recorte, quando a exportação é repetida, então o conteúdo é idêntico
  e o identificador de integridade coincide.
- Dado um artefato exportado, quando qualquer parte dele é alterada, então a verificação de
  integridade falha.

## O que não exige

Não exige formato de arquivo específico, que é escolha de decisão. Não exige exportar todos os
domínios juntos nem separados: exige declarar qual recorte saiu. Não exige que o artefato leve a
procedência interna de cada item, que fica em [002](002-origem-preservada-e-rastreavel.md) e não
acompanha a saída.
