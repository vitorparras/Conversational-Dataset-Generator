# 0050 — O banco amarra a mudança do conteúdo a um evento da cadeia

> Decisão vigente. 2026-11-02, decisão do Orquestrador na issue #514, com o dono fora.

## Contexto

O esvaziamento de um item (#73) e a troca da chave embrulhada (a rotação da
[0049](0049-chave-por-item-e-epoca.md)) só estavam ligados à cadeia de custódia pela aplicação. Um
defeito nela perderia conteúdo sem rastro: perda, não vazamento. As outras operações de custódia
já são amarradas no banco, como a imutabilidade dos eventos, e a R4 da #73 pede o mesmo aqui.

## Decisão

**O banco recusa, para todo papel e o dono incluído, esvaziar um item ou trocar a chave embrulhada
de uma linha sem que o evento dessa mudança já esteja na cadeia, escrito na mesma transação.**

- O esvaziamento do item e o do mapa dele exigem o evento de esvaziamento daquele item.
- A troca da chave embrulhada ou da época exige o evento de giro da época para a época nova. Um
  evento só cobre todas as chaves que o giro reembrulha, e o giro não grava evento se não houver
  chave a reembrulhar.
- O evento de giro é do sistema, sobre a época, e o banco confere a forma dele.
- A recusa tem o SQLSTATE CE005 e uma mensagem que nomeia a tabela e nenhum valor.
- O gatilho vale também em sessão com a replicação desligada, como os da custódia.

## Como se reconhece a mesma transação

A linha do evento guarda o número da transação que a escreveu (xmin), e o banco o compara com o
da transação corrente. Evento de outra transação, confirmada ou não, nunca casa, então um escritor
concorrente não libera mudança que não é dele. Evento escrito dentro de um ponto de salvamento
carrega o número do ponto e também não casa: a recusa falha fechada. O número tem 32 bits, então o
evento também não pode ser mais antigo que o início da transação (`occurred_at`); um relógio
recuado no meio recusa do mesmo jeito, e repetir resolve. O gatilho roda por linha, por isso a
procura do evento tem índice próprio (`item_kind`, `item_id`, `step`).

## Alternativas recusadas

- **Variável de sessão que a aplicação ajusta.** Um defeito na aplicação a ajustaria sem evento.
- **Um evento por chave reembrulhada.** Encheria a cadeia a cada giro sem dizer mais que um.
- **Deixar a aplicação como única guarda.** É o risco que esta decisão fecha.

## Consequências

O esvaziamento e o giro passam a depender de a cadeia aceitar o evento: cadeia quebrada ou
concorrente no mesmo elo recusa a mudança, e o giro repete. Quem edita o banco à mão precisa
escrever o evento antes. Piora: o giro compete pelo elo da cadeia com quem grava eventos, e o evento dele o segura até o
commit.

## O que reabre esta decisão

Um caso em que a mesma transação não possa escrever o evento, ou o banco deixar de expor o número
da transação na linha.
