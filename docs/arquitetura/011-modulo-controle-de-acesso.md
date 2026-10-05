# 011 — Módulo de controle de acesso

> Descrição. 2026-10-04.

Controle de acesso não é etapa do pipeline: é condição de qualquer etapa. Por isso aparece em todos
os outros documentos desta pasta e não tem lugar na sequência.

Garante que a autorização é avaliada antes do código que faria o trabalho, e não dentro dele
([0002](../decisoes/0002-stack.md)). Condicional espalhada pelo caminho falha por omissão: o lugar
que esqueceu de verificar não acusa nada, apenas atende.

Garante que o padrão é negar ([requisito 017](../requisitos/017-acesso-negado-por-padrao.md)).
Operação nova que nenhuma política cobre nega, e a negativa não revela se o item existe. Num sistema
que libera por omissão, a próxima operação escrita nasce aberta e ninguém percebe até alguém
alcançar o que não devia; negar por omissão troca vazamento silencioso por erro visível no primeiro
uso legítimo.

Garante também que a concessão é por domínio
([0006](../decisoes/0006-dominios-coexistem.md)). A mesma pessoa pode ter alcance diferente em
domínios diferentes, e concessão, revogação e mudança de papel ficam registradas com quem fez,
sobre quem, quando e com que escopo.

Consome a identidade de quem pediu e o que foi pedido. Produz uma decisão e o registro dela: quem,
quando, qual item, sob qual finalidade declarada. Fala com todos os módulos e com a custódia — a
negativa é registrada tanto quanto a permissão, porque uma sequência de negativas é informação de
segurança, não ruído.

## Duas camadas, não uma

A aplicação decide por política declarada, e o banco decide de novo, por política de linha que vale
até contra o dono da tabela. Não é redundância desperdiçada: a segunda camada é o que sobra quando a
primeira tem defeito, e a primeira é o que dá mensagem inteligível onde a segunda recusaria em
silêncio.

Consulta que esqueça o domínio não devolve material de outro domínio. Não devolve nada — e essa
diferença entre "vazou" e "veio vazio" é o que faz o defeito aparecer em teste em vez de em
incidente.

## As operações que pedem mais

Decifrar o mapa de reversão revela identidade, e é o pedido mais sensível do sistema
([0003](../decisoes/0003-cifra-em-repouso.md)). Exportar é o de maior alcance, porque o resultado
sai do alcance do sistema. Os dois são autorizados caso a caso e registrados sempre.

## As funções moram na política, não aqui

Quais funções existem e o que cada uma alcança está declarado num lugar só, em
[privacidade](../privacidade/): anotação, arbitragem, curadoria do acervo, auditoria e administração
técnica. Este módulo não guarda essa lista; aplica-a. Duas dessas funções não alcançam conteúdo
algum, e é de propósito — quem audita e quem administra o sistema não precisam ler material para
fazer o trabalho.

Reversão de pseudônimo não é alcance de função alguma: é ato autorizado por pedido. A distinção é o
que impede que a autorização se transforme em interruptor.
