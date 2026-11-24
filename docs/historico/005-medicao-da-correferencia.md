# 005 — Medição da correferência

> Histórico. 2026-11-24.

**Falta a medição: este registro tem só as condições, e os números entram quando ela rodar.**

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
| Bibliotecas | torch 2.15.0, transformers 5.24.0, peft 0.22.0, psutil 7.2.2 |
| Python | 3.12, ambiente descartável do `uv` |
| Sistema | Windows 11, x86_64; 27,7 GiB de memória |
| Corpus | `language-service/tools/coreference/fixtures/conversas.json`: 20 conversas, 123 mensagens, 14 que misturam línguas |
| Limiar | 0,95 na precisão geral e na do recorte misto |
| Tamanhos | 10, 40, 160 e 640 mensagens |

A medição só roda com a memória livre projetada acima de 12% depois da carga.

## Números

A medir: precisão, cobertura e fusão; as mesmas no recorte misto; precisão e cobertura da ligação
ao autor; carga, RSS carregado e, por tamanho, caracteres, tempo, RSS e pico.

Ver [regras de escrita](../regras-de-escrita.md).
