# 012 — Módulo de custódia

> Descrição. 2026-10-04.

Custódia é a capacidade de dizer o que aconteceu com o material, em que ordem, e de demonstrar que o
registro não foi mexido depois. Como o controle de acesso, não é etapa: é condição.

Garante que todo evento relevante fica escrito — abertura e rejeição de lote, cada transformação
aplicada, cada anotação, cada autorização pedida, concedida ou negada, cada exportação, cada
decifração do mapa de reversão. Garante que o registro é apenas acrescentado: a tabela é imutável
por concessão e gatilho no banco, não por disciplina de quem escreve o código
([0002](../decisoes/0002-stack.md)).

Consome eventos dos outros módulos. Produz a cadeia de registros, e o resultado da verificação dela.
O evento aponta para o item, o passo e o motivo — nunca para o conteúdo: nenhum registro deste
sistema guarda texto de material, identidade de pessoa ou segredo, nem quando a falha acontece no
meio do processamento ([requisito 021](../requisitos/021-registro-sem-dado-pessoal.md)).

Fala com todos, e depende de ninguém: nenhum módulo precisa do que a custódia diz para funcionar, e
é exatamente por isso que ela pode ser verificada à parte. Dependência no outro sentido tornaria a
verificação circular.

## A cadeia

Cada evento encadeia com o anterior por hash
([requisito 019](../requisitos/019-custodia-encadeada.md)). Alterar um evento no meio rompe o
vínculo com o seguinte, e a verificação aponta o primeiro ponto inconsistente
([requisito 020](../requisitos/020-integridade-verificavel.md)).

Isso não cobre o corte do fim. Remover os últimos eventos deixa um prefixo coerente consigo mesmo, e
a verificação passa — como passa uma base inteira substituída por outra, também coerente. Por isso o
topo da cadeia é anotado periodicamente fora do banco, fora do alcance de quem o administra: sem
esse valor externo a verificação atesta coerência, não integridade. Onde ele mora é pendência
declarada — ver [privacidade](../privacidade/).

O encadeamento exige gravação serializável. Duas gravações simultâneas que leiam o mesmo evento
anterior produzem dois sucessores do mesmo elo, e a cadeia fica ambígua em silêncio
([0002](../decisoes/0002-stack.md)). A alternativa aceitável é a gravação falhar de forma visível;
nunca passar.

## Corrigir é acrescentar

Nada aqui é editado nem apagado, inclusive o que está errado. Evento registrado por engano é
corrigido por um evento que diz isso, com autoria e momento. É o que distingue registro de anotação
em caderno.

Vale para a má notícia também: perda de chave é perda de acervo, e fica registrada como tal
([0014](../decisoes/0014-custodia-da-chave-mestra.md)). Não se finge que o material existe porque as
linhas continuam no banco.

## Verificar, e não só poder verificar

A cadeia é verificável de ponta a ponta, e a verificação precisa rodar sem depender de alguém se
lembrar dela. Verificação que existe e nunca roda não é garantia; é a aparência de uma. Quando e por
quem ela roda é assunto de [operação](../operacao/).

A integridade da cadeia e os invariantes da pseudonimização são afirmações sobre toda execução
possível, e são eles que o teste baseado em propriedades cobre
([0007](../decisoes/0007-garantia-de-qualidade.md)).
