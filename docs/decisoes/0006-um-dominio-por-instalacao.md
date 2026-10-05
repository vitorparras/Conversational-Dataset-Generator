# 0006 — Um domínio por instalação

> Decisão vigente. 2026-10-04.

## Decisão

Cada instalação do sistema atende a **um** domínio, com uma taxonomia. Não há identificador de
projeto separando domínios dentro da mesma base.

Usar o sistema em outro domínio significa outra instalação, com sua própria base, suas próprias
chaves e sua própria taxonomia.

## Por quê

O sistema guarda material sensível e reverte pseudonimização sob autorização. Separar domínios
dentro de uma base só transforma cada consulta num ponto onde o isolamento pode falhar: basta um
filtro esquecido para material de um domínio aparecer em outro. Separar por instalação torna o
isolamento propriedade do ambiente, não da correção de cada consulta.

Também simplifica a custódia. Chaves, backup, controle de acesso e trilha de auditoria passam a ter
um escopo só, e a pergunta "quem pode ver o quê" tem resposta de uma linha.

A decisão 0001 exige que a ferramenta sirva a mais de um domínio, e isso continua valendo: o que ela
não exige é que sirva a vários **ao mesmo tempo, na mesma base**.

## Recusados

| Opção | Por quê |
|---|---|
| Identificador de projeto na mesma base | Isolamento passa a depender de cada consulta estar correta, com dado sensível em jogo |
| Bases separadas, aplicação compartilhada | Mesma superfície de erro na seleção da base, com operação mais complexa |

## Consequências

Comparar resultados entre domínios acontece sobre datasets exportados, não por consulta ao banco. A
exportação precisa carregar o que torna a comparação possível: taxonomia, versão do pipeline e
contagens.

Rodar duas instalações custa mais operação: dois bancos, dois conjuntos de chaves, dois backups.

## O que reabre

Se a comparação entre domínios passar a ser atividade cotidiana, e não eventual, o custo de exportar
e reimportar supera o risco de isolamento — e a decisão deve ser reavaliada com um modelo de
isolamento explícito, não com um filtro a mais.
