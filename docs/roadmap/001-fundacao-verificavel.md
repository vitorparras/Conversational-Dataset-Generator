# Fase 1 — Fundação verificável

> Norma vigente. 2026-10-04.

A primeira fase não produz nenhuma capacidade do sistema. Produz a régua: o aparato que faz cada
garantia posterior ser verificável por máquina, e o esqueleto que prova que a forma de distribuição
escolhida é viável enquanto ainda é barato mudá-la.

A decisão [0007](../decisoes/0007-garantia-de-qualidade.md) exige que os invariantes estejam
escritos antes dos testes que os verificam — e eles já estão, em `docs/requisitos/`. O que falta é o
outro lado: a lista de módulos críticos e os pisos de cobertura, que a mesma decisão manda viver em
documento operacional, e o arranjo que faz propriedade, mutação e cobertura rodarem de verdade.

Entra o esqueleto das duas partes da decisão [0002](../decisoes/0002-stack.md) — o núcleo e o
serviço de linguagem como processo separado, ambos vazios de domínio — e a construção que empacota
o núcleo num executável único e o serviço numa pasta ao lado dele, pela
[decisão 0041](../decisoes/0041-distribuicao-do-servico-de-linguagem.md). A verificação da documentação já existe em
`tools/verify-docs/verify.py`; esta fase estende a mesma régua ao código.

Entra, por fim, a redação de campo sensível no próprio pipeline de log. É mecanismo de plataforma e
custa pouco agora; instalado depois, cada log escrito no intervalo é candidato a resíduo.

## O que precisa estar pronto antes

Nada além das decisões e dos requisitos vigentes. É a única fase sem fase anterior, e é por isso que
deve ser curta: quanto mais ela crescer, mais longe fica o primeiro material real.

## Critério de término

1. A construção, a partir de uma cópia limpa do repositório, produz o executável único do núcleo e a
   pasta do serviço, e os dois sobem numa máquina recém-preparada sem que nada seja instalado nela —
   a parte de [022](../requisitos/022-operacao-sem-servico-externo.md) que já se fecha aqui.
2. O núcleo conversa com o serviço de linguagem, e derrubar o serviço produz erro visível, não
   silêncio nem resultado parcial.
3. [021](../requisitos/021-registro-sem-dado-pessoal.md) tem teste próprio, e esse teste reprova de
   fato um log que carregue texto de material.
4. A lista de módulos críticos e os pisos de cobertura estão em `docs/operacao/`, e o teste de
   mutação roda sobre essa lista.
5. A verificação de pull request reprova uma mudança que cruze um piso para baixo.
6. Uma propriedade proposital errada é reprovada pela própria verificação — a régua foi medida com
   um caso que ela deveria recusar.

## O que destrava

Todo o resto. A partir daqui, "está pronto" é afirmação verificável em vez de opinião, e é essa
diferença que torna possível declarar o fim de qualquer fase seguinte.
