# 005 — Medição da correferência

> Histórico. 2026-11-25.

Registro da medição do modelo de correferência pelo procedimento do
[053](../operacao/053-medicao-da-correferencia.md), para a
[decisão 0060](../decisoes/0060-correferencia-na-resposta-da-fronteira.md). É o que valeu naquela
máquina, naquele dia; não é regra nem previsão.

## Condições

| Item | Valor |
|---|---|
| Modelo | Stanza 1.15.1, `udcoref_xlm-roberta-lora` (pacote `es` do Stanza), 136 MB de LoRA |
| Base que ele carrega | xlm-roberta-large, 2,1 GiB de pesos |
| Tokenização | Stanza pt, `tokenize` e `mwt` do pacote `bosque` |
| Bibliotecas | torch 2.15.0+cpu, transformers 5.24.0, peft 0.22.0, psutil 7.2.2 |
| Python | 3.12.16, ambiente descartável do `uv` |
| Sistema | Linux x86_64 do GitHub Actions (run 45553459284), 4 núcleos, 15.700 a 15.983 MiB |
| Corpus | `language-service/tools/coreference/fixtures/conversas.json`: 20 conversas, 123 mensagens, 14 que misturam línguas |
| Limiar | 0,95 na precisão geral e na do recorte misto |
| Tamanhos | 10, 40, 160 e 640 mensagens, em três execuções: 10 e 40; 160; 640 |

Numa máquina Windows de 27,7 GiB a carga levou a memória livre a 1,79 GB e foi interrompida, sem
número; por isso a medição foi ao Actions. Uma primeira execução lá perdeu o runner aos 48 minutos.

## Números

| Recorte | Acertos | Valor |
|---|---|---|
| Precisão | 102/124 | 0,823 |
| Cobertura | 102/112 | 0,911 |
| Fusão | 12/58 | 0,207 |
| Precisão no recorte misto | 14/19 | 0,737 |
| Fusão no recorte misto | 4/19 | 0,211 |
| Precisão da ligação ao autor | 9/37 | 0,243 |
| Cobertura da ligação ao autor | 27/34 | 0,794 |

Reprovado: a precisão geral e a do recorte misto ficam abaixo de 0,95, e a saída foi 1 nas duas
execuções que terminaram, com as mesmas razões.

A carga levou de 7,97 a 9,01 s, e o RSS carregado ficou entre 1.633 e 1.664 MiB.

| Mensagens | Caracteres | Tempo | RSS | Pico |
|---|---|---|---|---|
| 10 | 523 | 2,33 s | 3.930 MiB | 4.004 MiB |
| 40 | 2.049 | 5,26 s | 5.110 MiB | 5.622 MiB |
| 160 | 7.943 | 12,34 s | 6.497 MiB | 12.760 MiB |
| 640 | — | — | — | — |

Com 640 mensagens, o vigia encerrou o processo cerca de 40 s depois de começar a análise, com
690 MiB de memória disponível na máquina; não há número.

Ver [regras de escrita](../regras-de-escrita.md).
