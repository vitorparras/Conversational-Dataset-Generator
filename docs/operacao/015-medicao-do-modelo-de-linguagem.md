# 015 — Medição do modelo de linguagem

> Operacional. 2026-10-19.

Disco, memória durante a análise e tempo de carga do modelo de português decidem o que a
distribuição do serviço de linguagem pode prometer, como a [decisão 0041](../decisoes/0041-distribuicao-do-servico-de-linguagem.md) expõe.
Aqui está como medir; os números vivem em [003](../historico/003-medicao-do-modelo.md) e nos
registros que o seguirem, porque mudam com o modelo, o spaCy e a máquina.

## Quando medir

Antes de fechar ou reabrir a distribuição e a cada troca de modelo ou de versão do modelo ou do
spaCy. Medição de outra máquina ou versão não se compara sem dizer isso.

## O pacote do modelo

`language-service/tools/measure.py` mede: disco (soma dos arquivos do pacote instalado, sem
`__pycache__`, e o mesmo conjunto por deflate nível 6, arquivo a arquivo); `spacy.load` em processo
novo, repetido, com mediana e faixa; RSS do interpretador recém-iniciado, depois de importar o
spaCy, depois de carregar e depois de analisar um texto sintético de marcadores (`PESSOA_1`,
`CIDADE_A`), nunca nome plausível. O `ru_maxrss` atrasa no Linux e pode ficar abaixo do último
RSS lido: não o leia como pico; o pico é o maior RSS amostrado. A partir da raiz, com `uv`:

```
M=pt_core_news_md V=3.8.0 S=3.8.16
R=https://github.com/explosion/spacy-models/releases/download
uv run --no-project --python 3.12 --with spacy==$S --with $R/$M-$V/$M-$V-py3-none-any.whl \
    python language-service/tools/measure.py $M
```

Para outro modelo ou versão, troque `M`, `V` e `S`; o campo `spacy_version` do meta.json do pacote,
que a saída repete, diz qual spaCy casa. `--runs` (padrão 7) e `--chars` (padrão 20 000, e o texto
sai com o comprimento real, não menor) ficam declarados na saída. O JSON vai à saída padrão, a
tabela à de erro. O script lê `/proc` e só mede em Linux.

## O ambiente que carrega o modelo

O script não o mede. Interpretador autônomo e spaCy, sem o modelo, medem-se à parte: soma dos
arquivos sem `__pycache__` e o conjunto por `tar` e `gzip -6` (outra compressão), ambos em bytes.

```
D=$(mktemp -d); export UV_PYTHON_INSTALL_DIR=$D/py; S=3.8.16
uv python install 3.12 --no-bin
uv venv --python "$(echo $D/py/cpython-3.12.*/bin/python3.12)" $D/venv
uv pip install --python $D/venv/bin/python spacy==$S
for d in $D/py/cpython-3.12.*/ $D/venv/lib/python3.12/site-packages; do
    find $d -name __pycache__ -prune -o -type f -printf '%s\n' | awk '{s+=$1} END {print s}'
    tar -C $d --exclude=__pycache__ -c . | gzip -6 | wc -c
done
```

## A correferência

`language-service/tools/measure_coreference.py` mede um modelo de correferência sobre conversas
inteiras do corpus ouro `coreference/fixtures/conversas.json`: conversas sintéticas, cada pessoa
num identificador (`NOME_A`), com 14 mensagens que misturam línguas. Cada conversa vai ao modelo
na forma do pedido, uma linha `Nome: texto` por mensagem. A saída dá, em pares de menções das
mensagens, a precisão, a cobertura e a fusão (pessoas que dividem grupo com outra); as mesmas
contas só nos pares que tocam mensagem mista; e a ligação ao nome do autor, com precisão e
cobertura à parte. Depois, conversas de `--sizes` mensagens (padrão 10, 40, 160 e 640) dão tempo,
RSS e pico de memória. A precisão geral e a do recorte misto abaixo de `--min-precision` (padrão
0,95), ou sem nenhum par ligado, reprovam: cada motivo numa linha e saída 1. Mede em Linux e em
Windows. A partir da raiz, com os modelos pt `tokenize` e `mwt` do Stanza já baixados:

```
uv run --no-project --python 3.12 --with stanza==1.15.1 --with peft --with psutil \
    python language-service/tools/measure_coreference.py stanza <arquivo do modelo .pt>
```

Rode um modelo por vez, e só se a memória livre projetada com ele carregado ficar acima de 12%.

## O que registrar

Num documento novo de `docs/historico/`, em tabela: modelo, versão e licença (do meta.json),
versões do spaCy e do Python, sistema operacional, núcleos e memória, **carga média no início e no
fim**, execuções, comprimento do texto e cada medida. Sem nome de máquina nem caminho. Carga média
acima do número de núcleos vai dita: o tempo de carga é então limite superior. O cache de disco é
morno em toda execução; a primeira carga depois de ligar a máquina é mais lenta e não se mede.

Ver [regras de escrita](../regras-de-escrita.md).
