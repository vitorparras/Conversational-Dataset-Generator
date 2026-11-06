# 036 — O formato cdg-conversa/1 da conversa exportada

> Operacional. 2026-11-06.

A primeira versão lê conversa exportada e só ([decisão 0036](../decisoes/0036-formatos-da-primeira-versao.md)).
O formato é este: um arquivo JSON em UTF-8, estrito, que traz remetente, ordem e momento separados,
para que nada seja inferido. Exportações de outros programas entram depois, cada uma com leitor
próprio.

## Exemplo

```json
{
  "formato": "cdg-conversa/1",
  "mensagens": [
    {"id": "m1", "remetente": "REMETENTE_A", "instante": "2031-03-04T08:00:00Z", "texto": "TEXTO_1"},
    {"id": "m2", "remetente": "REMETENTE_B", "instante": "2031-03-04T05:01:30-03:00",
     "texto": "TEXTO_2", "responde_a": "m1"}
  ]
}
```

## O que o arquivo tem

- **formato:** exatamente **cdg-conversa/1**. Outra versão é recusada como formato não lido.
- **mensagens:** a lista. Cada mensagem tem `id`, `remetente`, `instante` e `texto`, todos texto.
- **id:** único no arquivo. É só um nome para **responde_a**, e não dá ordem a nada.
- **instante:** RFC 3339 com fuso escrito, como `Z` ou `+03:00`. Sem fuso o arquivo é recusado: o
  leitor não supõe o fuso de ninguém.
- **responde_a:** opcional. Traz o `id` de outra mensagem do mesmo arquivo; o leitor entrega o
  remetente dela. Um `id` que não existe recusa o arquivo.

## O que o leitor recusa

O JSON é lido sem comentário, sem vírgula final e sem campo que o formato não tem. Chave repetida,
tipo trocado, campo faltando, profundidade acima de 4 níveis e arquivo acima de 16 MiB recusam o
arquivo inteiro. Não existe leitura parcial: ou o arquivo foi lido todo, ou nada dele passa adiante.

O motivo da recusa nomeia o defeito (arquivo danificado, cortado antes do fim ou formato que esta
versão não lê) e **nunca traz palavra do conteúdo**.

## O que o leitor não faz

Não ordena: a ordem do arquivo não vira ordem, e quem ordena é a conferência, pelo `instante`. Não
confere a forma: conversa sem mensagem ou com autor em branco é lida e recusada depois. Não grava
nem cifra, não lê disco, rede ou relógio, e não recebe o nome do arquivo.

## Para quem prepara o material

Gere o arquivo já na forma acima, com um remetente e um instante com fuso por mensagem. Dois
instantes iguais na mesma conversa são recusados pela conferência, porque a ordem não pode vir da
posição no arquivo.
