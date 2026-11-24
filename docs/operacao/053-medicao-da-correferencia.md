# 053 — Medição da correferência

> Operacional. 2026-11-24.

O grupo de correferência vira o referente do marcador
([0057](../decisoes/0057-escopo-do-marcador-e-a-conversa.md)). Mede-se o modelo antes de escolhê-lo
e a cada troca dele, da sua versão, da biblioteca ou do corpus; o registro segue o
[015](015-medicao-do-modelo-de-linguagem.md), mais o modelo, a base que ele carrega, o corpus e o
limiar, sem nome de máquina nem caminho.

## O que a ferramenta mede

`language-service/tools/measure_coreference.py` roda o modelo sobre cada conversa do corpus ouro
`language-service/tools/coreference/fixtures/conversas.json`: conversas sintéticas, cada pessoa
num identificador (`NOME_A`), com 14 mensagens que misturam línguas. A conversa vai ao modelo na
forma do pedido, uma linha `Nome: texto` por mensagem. Em pares de menções das mensagens, a saída
dá a precisão, a cobertura e a fusão (pessoas que dividem grupo com outra); as mesmas contas só nos
pares que tocam mensagem mista; e a ligação ao nome do autor, com precisão e cobertura à parte.
Depois, conversas de `--sizes` mensagens (padrão 10, 40, 160 e 640) dão tempo, RSS e pico de
memória; o pico é do processo, e o de cada tamanho inclui a carga e os anteriores. O JSON vai à
saída padrão, só com o nome do arquivo do modelo; a tabela e o veredito, à de erro.

A precisão geral e a do recorte misto abaixo de `--min-precision` (padrão 0,95), ou sem nenhum par
ligado, reprovam: cada motivo numa linha e saída 1. Modelo reprovado não entra.

## Como rodar

Mede em Linux e em Windows. Os modelos pt `tokenize` e `mwt` do Stanza ficam baixados antes; a
ferramenta não busca nada. A partir da raiz, com `uv`:

```
uv run --no-project --python 3.12 --with stanza==1.15.1 --with peft --with psutil \
    python language-service/tools/measure_coreference.py stanza <arquivo do modelo .pt>
```

Um modelo por vez, e só se a memória livre projetada com ele carregado ficar acima de 12%. O `.pt`
é carregado por pickle, que executa código: só se mede modelo de origem conhecida.

Ver [regras de escrita](../regras-de-escrita.md).
