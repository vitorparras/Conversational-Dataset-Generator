# 029 — Avaliar qualidade não exige alcançar o texto

> Norma vigente. 2026-10-07.

**É possível consultar métricas, contagens e o estado de uma campanha sem receber nenhum texto do
acervo.**

## Por que existe

Quem avalia o trabalho — quem orienta, quem audita, quem decide se um dataset está pronto — precisa
de números, não de conteúdo.

Sem uma forma de entregar só os números, a única maneira de mostrar o andamento é conceder uma
função que dá acesso ao texto. A avaliação passa a custar uma exposição que ela não precisava, e a
concessão fica mais larga do que a necessidade.

É o oposto do princípio de conceder o mínimo: aqui, a ausência de um caminho estreito obriga a abrir
um largo.

## Critérios de aceitação

**CA-1** — *Dado* alguém com a função de avaliação, *quando* consultar o andamento de uma campanha,
*então* recebe contagens, métricas e distribuições, e nenhum trecho de material.

**CA-2** — *Dado* a mesma pessoa, *quando* tentar alcançar qualquer texto do acervo por qualquer
caminho do sistema, *então* é recusada — a função não dá acesso a conteúdo por via alguma.

**CA-3** — *Dado* um número que identifique poucos itens, *quando* a contagem for pequena o bastante
para que o número aponte um item específico, *então* a resposta informa que o recorte é pequeno
demais, em vez de devolver a contagem.

**CA-4** — *Dado* qualquer consulta de métrica, *quando* ela ocorrer, *então* fica registrada como
acesso, com quem, quando e a que recorte.

## O que não exige

Não exige que a função de avaliação seja exclusiva: quem tem função mais ampla também consulta
métricas, pelo mesmo caminho.

Não exige que toda métrica esteja disponível a essa função. A política de acesso define quais, e o
critério é se o número, sozinho, revela conteúdo.
