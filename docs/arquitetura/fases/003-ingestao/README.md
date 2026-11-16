# Fase 3 — o que falta da ingestão

> Norma pendente. 2026-11-15. Aguarda a aprovação do dono antes do primeiro código.

Pacote da fase: o desenho do resto da fase 3, feito antes do código, para que nenhuma fatia
replaneje no meio. Fecha as issues #607, #609, #610 e #775 em duas fatias verticais. Cada fatia
entrega o esquema, o domínio, a rota, a tela, os testes e o documento de uma funcionalidade usável.

O que falta saber é só a aprovação do dono, com as perguntas abertas que o Orquestrador leva a ele.
Aprovado, este pacote vira `Norma vigente` e as issues das fatias saem de "A fazer".

## Índice

| Documento | Conteúdo |
|---|---|
| [Modelo de dados](modelo-de-dados.md) | Migrações das duas fatias, com nome e motivo |
| [Contratos](contratos.md) | O que cada módulo oferece ao outro, e quem chama quem |
| [Riscos](riscos.md) | Concorrência, cancelamento no meio, falha de rede, dado pessoal |
| [Fatia A](fatia-a-entrada-do-material.md) | Entrada do material até o lote (#607, #609, #775) |
| [Fatia B](fatia-b-revisao-por-amostra.md) | Revisão por amostra (#610) |
| [Protótipos](prototipos/index.html) | As telas navegáveis, com todos os estados |

## Roteiro

1. **Fatia A — entrada do material até o lote.** A curadoria envia o arquivo pela tela, é levada
   ao lote aberto e vê as contagens, a procedência e a perda declarada. O cancelamento no meio da
   gravação deixa o lote rejeitado com motivo próprio.
2. **Fatia B — revisão por amostra.** Do lote aberto, a curadoria sorteia a amostra, lê cada item
   e aprova ou reprova o lote inteiro.

A fatia B depende da A, porque a revisão parte da tela do lote. As raias não colidem: a A toca a
submissão e o detalhe do lote, e a B toca a revisão e a reprovação.

## Base

- As telas I1, I3 e I4: [telas da ingestão e do acervo](../../016-telas-ingestao-e-acervo.md).
- Os padrões de cada peça: [padrões de peça](../../padroes/README.md).
- As peças de tela já estão no main: #732 (I1), #755 (I3) e #793 (I4). O que falta é a rota que
  as liga ao domínio, com o que o domínio ainda não oferece.

Ver [regras de escrita](../../../regras-de-escrita.md).
