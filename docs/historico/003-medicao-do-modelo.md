# 003 — Medição do modelo de linguagem

> Histórico. 2026-10-19.

Registro de uma medição feita em 2026-10-19 com o procedimento de
[015](../operacao/015-medicao-do-modelo-de-linguagem.md), pedida pela
[decisão 0011](../decisoes/0011-protocolo-com-o-servico-de-linguagem.md) para a
[decisão 0041](../decisoes/0041-distribuicao-do-servico-de-linguagem.md). É o que valeu naquela
máquina, naquele dia; não é regra nem previsão.

## Condições

| Item | Valor |
|---|---|
| spaCy | 3.8.16 |
| Modelos | pt_core_news_sm, pt_core_news_md e pt_core_news_lg, todos 3.8.0 (exigem spaCy `>=3.8.0,<3.9.0`) |
| Licença dos três (campo `license` do meta.json) | CC BY-SA 4.0 |
| Pipeline dos três | tok2vec, morphologizer, parser, lemmatizer, attribute_ruler, ner, completo |
| Python | 3.12.3, do sistema, ambiente descartável do `uv run` |
| Sistema | Ubuntu 24.04.5 LTS, x86_64; 4 núcleos; 16 093 MiB de memória |
| Execuções por modelo | 7 processos novos, em sequência |
| Texto sintético | 20 250 caracteres (pedidos 20 000), 3 465 tokens; três frases repetidas 75 vezes, só marcadores |
| Disco | pacote já lido há segundos: cache de página morno |

**A máquina estava ocupada por outros processos.** Carga média de 1 minuto no início e no fim:
`sm` 5,25 e 5,00; `md` 6,12 e 6,12; `lg` 3,14 e 3,66, com 4 núcleos. Os tempos abaixo são limite
superior do que a máquina faria livre, e a faixa larga de `md` e `lg` vem disso.

## Números

Memória em MiB (1 MiB = 1024 KiB), mediana das 7 execuções; disco em MiB; tempo em segundos.

| Medida | sm | md | lg |
|---|---|---|---|
| Disco do pacote instalado | 15,4 | 51,6 | 601,8 |
| Disco do pacote comprimido (deflate 6, por arquivo) | 12,4 | 40,4 | 541,9 |
| `spacy.load`, mediana | 0,29 | 1,16 | 1,34 |
| `spacy.load`, menor e maior | 0,28 e 0,34 | 0,98 e 2,05 | 1,16 e 5,23 |
| `import spacy`, mediana | 0,69 | 0,67 | 0,69 |
| Análise do texto sintético, mediana | 0,33 | 0,41 | 0,50 |
| RSS do interpretador recém-iniciado | 15,8 | 15,9 | 15,9 |
| RSS depois de importar o spaCy (antes da carga) | 89,9 | 89,7 | 90,0 |
| RSS depois de carregar o modelo | 150,6 | 332,1 | 897,7 |
| RSS depois de analisar o texto | 206,8 | 390,2 | 947,8 |
| `ru_maxrss`, maior das 7 (atrasa no Linux: fica abaixo do RSS lido antes) | 207,5 | 393,6 | 955,0 |
| Vetores de palavras (meta.json) | 0 | 20 000 | 500 000 |

O modelo sozinho acrescenta 60,7 (`sm`), 242,4 (`md`) e 807,7 MiB (`lg`) ao RSS; a análise do
texto acrescenta de 50 a 58 MiB nos três. Em `md` e `lg` a primeira execução foi a mais lenta em
`import spacy` (2,25 e 2,28 s, contra 0,61 a 0,82 s nas outras seis) e em `spacy.load` (2,05 e
5,23 s, contra 0,98 a 1,26 s em `md` e 1,16 a 2,62 s em `lg`).

A medição usou a primeira versão do script; a revisão mudou nomes, idioma das mensagens, passou a
gravar o comprimento real do texto e deixou de contar entidades, que não é medida de recurso; o
resto do que se mede não mudou. Uma repetição de `sm` com a versão final, pelo comando de 015, deu
disco idêntico, RSS depois de analisar de 207,1 MiB e `spacy.load` de 0,33 s (0,31 a 0,51), com a
carga média em 44: memória e disco se repetem, o tempo acompanha a máquina.

## O ambiente que carrega o modelo

Medido pelo método de 015, num Python 3.12.13 autônomo com `spacy==3.8.16`, sem o modelo; a
compressão aqui é `tar` e `gzip -6` do conjunto, diferente do deflate por arquivo do pacote:

| Parte | Instalada (MiB) | Comprimida (MiB) |
|---|---|---|
| Interpretador autônomo | 93,5 | 32,1 |
| spaCy e dependências (`site-packages`; maiores: spaCy 117,8, `blis` 33,4, `numpy` 27,4 e `numpy.libs` 26,2) | 250,3 | 72,7 |

FastAPI 0.143.1, Uvicorn 0.54.0 e suas dependências novas (Starlette 1.9.0 e `opentelemetry-api`
1.45.1, resolvidas sem o lockfile do serviço) somam 1,6 MiB instalados e 0,3 comprimidos; o Pydantic
já vem com o spaCy. Ficam fora dos totais abaixo, como o código do serviço e o núcleo em C#, que não
foi medido. Nenhum congelador (PyInstaller, Nuitka, PyOxidizer) foi experimentado.

Interpretador, spaCy e pacote do modelo juntos: cerca de 359 MiB instalados e 117 comprimidos com
`sm`; 395 e 145 com `md`; 946 e 647 com `lg`.

## Limites desta medição

Uma máquina, um dia, carga alheia, cache morno. Uma análise sequencial de um texto só, feito de três
frases repetidas: o tempo de análise não representa material real, e o serviço sob requisições
simultâneas não foi medido. O tempo de carga é o de `spacy.load`, sem a partida do processo do
serviço nem o servidor HTTP.
