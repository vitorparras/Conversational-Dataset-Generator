# Roadmap

> Norma vigente. 2026-10-04.

A ordem das fases vincula. Cada uma instala uma garantia de que a seguinte depende, e inverter duas
não adianta trabalho: troca garantia por dívida, que só aparece quando já houver material real no
sistema. Fase não tem data — tem pré-requisito e critério de término verificável.

Isto não é lista de tarefas; tarefa vive em issue. É o plano que dá sentido à sequência: o sistema
aprende primeiro a guardar, depois a receber, a limpar, a esconder, a negar, a anotar, a medir, a
publicar — e só então a demonstrar que o método não era específico de um domínio.

Cada fase termina quando os requisitos que ela fecha estão verificados. É por isso que os critérios
de término apontam para [`requisitos/`](../requisitos/) em vez de repetir o que está escrito lá.

## A sequência

1. [Fundação verificável](001-fundacao-verificavel.md) — a régua de qualidade e o esqueleto, com o
   núcleo num executável único. Sem ela, nenhuma fase posterior tem como declarar que terminou.
2. [Custódia e cifra](002-custodia-e-cifra.md) — chave derivada por finalidade, cifra feita pela
   aplicação, cadeia imutável e o eixo de domínio em toda tabela de conteúdo. É o que permite que
   material real exista no sistema.
3. [Ingestão](003-ingestao.md) — as quatro formas de material, cada uma com a validação que a
   define. O acervo deixa de ser pasta de arquivos.
4. [Sanitização](004-sanitizacao.md) — o resíduo sai, o sinal linguístico fica. O invariante é
   verificado por propriedade, não por exemplo.
5. [Pseudonimização](005-pseudonimizacao.md) — identidade substituída por rótulo estável, mapa de
   reversão cifrado, reversão restrita. É aqui que o texto passa a poder ficar em claro.
6. [Acesso](006-acesso.md) — papel, domínio e registro. Antes de o acervo se abrir a mais de uma
   pessoa, a negação precisa vir do banco.
7. [Anotação](007-anotacao.md) — taxonomia carregada na inicialização, procedência da sugestão e a
   tela onde o trabalho humano acontece.
8. [Concordância](008-concordancia.md) — mais de uma anotação sobre o mesmo item, a medida da
   divergência e a arbitragem que a resolve.
9. [Exportação](009-exportacao.md) — dataset com o recorte declarado e as métricas recalculáveis
   por quem o recebe.
10. [O segundo domínio](010-segundo-dominio.md) — a generalidade deixa de ser hipótese.

## Também aqui

[Dependências](011-dependencias.md) diz o que não pode começar antes do quê, e qual garantia se
perderia na inversão. [Decisões pendentes](012-decisoes-pendentes.md) lista o que as decisões
vigentes deixaram em aberto, e qual fase cada pendência trava.
[Fora de escopo](013-fora-de-escopo.md) registra o trabalho futuro deliberado, para que ampliação
não se confunda com atraso.

Esta pasta ordena a execução, e nada mais. O que ela não pode fazer é contrariar uma decisão vigente
ou um requisito: se a ordem proposta aqui exigir isso, é o roadmap que está errado.
