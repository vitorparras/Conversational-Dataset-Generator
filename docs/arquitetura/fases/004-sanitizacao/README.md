# Fase 4 — sanitização

> Norma pendente. 2026-11-15. Aguarda a aprovação do dono antes do primeiro código.

Pacote da fase: o desenho da sanitização, feito antes do código, para que nenhuma fatia replaneje
no meio. Fecha as issues #87 a #98 em cinco fatias verticais. Cada fatia entrega o esquema, o
domínio, a rota, a tela, os testes e o documento de uma funcionalidade usável.

A sanitização roda na entrada, depois da decisão de destino e antes da amostra, porque a revisão
lê o sanitizado ([telas](../../016-telas-ingestao-e-acervo.md), linha 14). O sanitizado e o rastro
das edições são cifrados como o bruto; só números ficam em claro
([0030](../../../decisoes/0030-qual-texto-fica-em-claro.md), linhas 22, 23 e 31).

## Índice

| Documento | Conteúdo |
|---|---|
| [Modelo de dados](modelo-de-dados.md) | Migrações das fatias, com nome e motivo |
| [Contratos](contratos.md) | O que cada módulo oferece ao outro, e quem chama quem |
| [Riscos](riscos.md) | Concorrência, cancelamento no meio, falha de rede, fila travada, queda do processo, dado pessoal |
| [Fatia A](fatia-a-destinos-na-entrada.md) | A entrada reconhece e grava os destinos (issue nova) |
| [Fatia B](fatia-b-passos-e-destinos.md) | Passos declarados e aplicação dos destinos (#87, #90, #92, #93, #98) |
| [Fatia C](fatia-c-normalizacao.md) | Normalização sem achatar o sinal (#88, #89, #95, #96) |
| [Fatia D](fatia-d-identificador-e-residuo.md) | Identificador partido e sem resíduo (#91, #94) |
| [Fatia E](fatia-e-reexecucao.md) | Reexecução sobre o bruto, em segundo plano, com fila e acompanhamento (#97) |
| [Protótipos](prototipos/index.html) | As telas navegáveis, com todos os estados |

## Roteiro

1. **Fatia A — a entrada reconhece e grava os destinos.** Cada texto do material é analisado
   antes do primeiro item; o item nasce com as ocorrências e o destino de cada uma. Serviço de
   linguagem caído rejeita o lote, sem itens, e a tela do lote diz por quê.
2. **Fatia B — passos declarados e aplicação dos destinos.** A lista de passos tem ordem e escopo
   declarados e uma versão derivada. O item nasce com o sanitizado e o rastro, cifrados; a tela do
   lote mostra as contagens por passo e a amostra lê o sanitizado.
3. **Fatias C, D e E, em paralelo.** C e D somam passos à lista; E reexecuta a lista vigente sobre
   o bruto, em segundo plano: a Curadoria confirma, a fila guarda e a tela acompanha.

A B depende da A, e C, D e E dependem da B. As raias de C, D e E não colidem: C e D escrevem cada
uma os seus passos, e a única linha em comum é a lista de passos; E toca a reexecução e a tela dela.

## Base

- O roadmap: [fase 4](../../../roadmap/004-sanitizacao.md).
- As telas I3 e I4: [telas da ingestão e do acervo](../../016-telas-ingestao-e-acervo.md).
- O pacote da fase 3, de que este parte: [fase 3](../003-ingestao/README.md).
- Os padrões de cada peça: [padrões de peça](../../padroes/README.md).

Ver [regras de escrita](../../../regras-de-escrita.md).
