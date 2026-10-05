# Fase 6 — Acesso

> Norma vigente. 2026-10-04.

Até aqui o acervo foi manipulado por quem o constrói. A anotação é o primeiro momento em que ele se
abre a mais de uma pessoa, e abri-lo antes de a negação existir é apostar que nenhuma consulta vai
esquecer um filtro — exatamente a opção que a decisão
[0006](../decisoes/0006-dominios-coexistem.md) recusa por escrito.

A política de linha já existe desde a fase 2. O que entra agora é o outro lado dela: identidade das
pessoas, papéis, concessão por domínio, autorização declarativa avaliada antes do código da rota, e
o registro de concessão e de revogação que o requisito
[017](../requisitos/017-acesso-negado-por-padrao.md) exige.

Papel importa mais aqui que lista de permissões. Quem anota não precisa reverter pseudonimização, e
é a separação entre as duas capacidades que mantém a reversão como evento raro e justificado, em vez
de rotina disponível a quem passa o dia na tela.

## O que precisa estar pronto antes

A [fase 2](002-custodia-e-cifra.md), que instala o isolamento no banco, e a
[fase 5](005-pseudonimizacao.md), que cria a operação cujo controle mais importa.

## Critério de término

1. [017](../requisitos/017-acesso-negado-por-padrao.md) é verificado, inclusive o padrão: pessoa
   sem concessão não alcança material, anotação nem métrica.
2. [018](../requisitos/018-isolamento-entre-dominios.md) passa a ser verificado com pessoas, e não
   só com consultas — e a negação continua vindo do banco quando o filtro é esquecido.
3. O papel que anota não consegue reverter pseudonimização, e a tentativa fica registrada.
4. Revogar o acesso de alguém interrompe o acesso sem exigir reinício do sistema.
5. [021](../requisitos/021-registro-sem-dado-pessoal.md) continua valendo sobre o registro de
   acesso, que é o registro mais tentado a guardar quem leu o que.

## O que destrava

A anotação por mais de uma pessoa e, com ela, tudo o que depende de haver mais de uma anotação sobre
o mesmo item.
