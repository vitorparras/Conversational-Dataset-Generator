# 022 — O sistema opera sem serviço externo

> Norma vigente. 2026-10-04.

**Ingestão, sanitização, pseudonimização, anotação, medida e exportação funcionam com a instalação
isolada da rede, e nenhum trecho do material sai dela.**

## Por que existe

A [decisão 0003](../decisoes/0003-cifra-em-repouso.md) recusa guarda de chave por serviço externo
porque o sistema precisa rodar em instalação local, e a
[decisão 0002](../decisoes/0002-stack.md) escolhe distribuição que não instala nada no destino. As
duas apontam para a mesma garantia, e é melhor que ela esteja escrita uma vez.

Material que pode vir de investigação não atravessa a borda da instalação para ser processado.
Dependência de serviço alheio acrescenta ainda uma segunda perda: indisponibilidade de terceiro vira
trabalho de anotação interrompido, e ninguém tem como resolver.

## Critérios de aceitação

- Dado a instalação sem acesso à rede externa, quando o fluxo completo é executado do material à
  exportação, então ele conclui.
- Dado um recurso que exija rede para ser obtido, quando ele é necessário, então ou acompanha a
  instalação, ou sua ausência é falha declarada no início, e não no meio do processamento.
- Dado qualquer passo do fluxo, quando ele processa material, então nenhum trecho do material deixa
  a instalação.
- Dado uma máquina de destino recém-preparada, quando o sistema é posto a rodar, então não é preciso
  instalar dependência nela.

## O que não exige

Não exige que o sistema recuse a rede quando ela existir, nem que ele deixe de usá-la para o que não
é material — atualização e obtenção de recurso podem usá-la. Não exige operar sem o serviço de
processamento de linguagem que acompanha a instalação: ele é parte dela, e não serviço externo.
