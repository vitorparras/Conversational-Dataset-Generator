# 001 — Visão geral

> Descrição. 2026-10-04.

O sistema é um pipeline com pessoas no meio. O material entra bruto, atravessa transformações
automáticas que o deixam legível e sem identidade, chega a uma tela onde pessoas decidem o que ele
quer dizer, e sai como dataset exportado com a procedência declarada. Nada do que foi registrado é
apagado depois: corrigir é acrescentar.

Quatro peças sustentam isso, e a fronteira entre elas é deliberada.

O núcleo é onde o pipeline mora. Lê o material, coordena as etapas, serve a interface de anotação,
grava, autoriza e registra. É também o único que conhece chave de cifra, porque a cifra acontece
antes de gravar ([0003](../decisoes/0003-cifra-em-repouso.md)).

O serviço de linguagem responde perguntas sobre texto: onde há entidade, qual a análise gramatical
de um trecho. Não guarda estado, não conhece o banco, não decide o que fazer com a resposta. É
processo separado porque os modelos de português vivem em outro ecossistema
([0002](../decisoes/0002-stack.md)), e é essa separação que permite trocá-lo sem tocar no pipeline.

O banco não é apenas onde o dado descansa. Ele impõe o isolamento entre domínios e a imutabilidade
do registro de custódia, para que uma consulta mal escrita no núcleo não vaze material nem apague
história ([0006](../decisoes/0006-dominios-coexistem.md)).

A taxonomia é um arquivo versionado, carregado na inicialização
([0004](../decisoes/0004-taxonomia-configuravel.md)). Não é peça de software, mas determina o que a
anotação pode dizer e o que a exportação aceita, e por isso faz parte deste desenho.

## A sequência, e o que atravessa toda ela

Seis módulos formam a sequência: ingestão, sanitização, pseudonimização, anotação, validação e
exportação. Cada um tem documento próprio nesta pasta; o que passa de um para o outro está em
[fronteiras](002-fronteiras.md), e o percurso completo em
[fluxo do material](003-fluxo-do-material.md).

Dois módulos não são etapas. Controle de acesso decide se quem pediu pode; custódia registra que
foi pedido. Valem em qualquer ponto da sequência, não num ponto dela, e é por isso que não entram
na fila. Tratá-los como etapa é o que produz sistema com auditoria em três lugares e nenhuma no
quarto.

## O que o desenho assume

Uma instalação atende vários domínios ao mesmo tempo, e o recorte de domínio acontece na
exportação, não na instalação. Material, anotação e métrica pertencem a um domínio, e a comparação
entre domínios é operação de rotina, não procedimento manual.

O material existe em quatro formas — conversa, comentário, documento e sequência rotulada —, e cada
forma é uma unidade de análise com validação própria
([0005](../decisoes/0005-unidades-de-analise.md)). O
[modelo de dados](004-modelo-de-dados.md) mostra como as quatro se acomodam nas mesmas entidades,
em vez de produzirem quatro modelos paralelos.
