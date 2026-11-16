# 0054 — Teto do PR por fatia e migração nomeada por data e hora

> Decisão vigente. 2026-11-16.

## Contexto

Duas fatias paralelas que criam migração disputavam o mesmo número, e o limite de 30 arquivos
cortava fatias verticais que cabem em 400 linhas. A fila colidia por regra, não por defeito.

## Decisão

**O teto vem da issue da fatia, e a ordem das migrações vem do nome.**

1. **Teto:** o `pr_size` lê a linha "Teto da fatia: N linhas" da issue que o PR fecha (`Closes #n`
   fora de comentário e de bloco de código, ou o número da branch), só se a issue tiver a linha
   "Plano conferido pelo Arquiteto: sim". Sem a linha vale 400; issue ilegível reprova. O limite
   duro é 1.500 linhas de produção. Não há limite de arquivos.
2. **Migração nova:** `AAAAMMDDHHMM-nome`, com um instante real do calendário. As numeradas que o
   main já tem ficam como estão e vêm antes. A versão registrada é a posição na ordem do nome.
3. **Teste e passo:** o teste exige cada arquivo registrado, numeradas sem lacuna até a última do
   main e nenhuma numerada nova. O passo `migration_order` reprova a migração do PR que não seja
   datada ou que ordene antes de uma que a base já tem; o PR traz o main e renomeia.

## Alternativas recusadas

- **Renomear as antigas:** mexeria no que já foi aplicado e na comparação do registro.
- **Versão como o instante:** `AAAAMMDDHHMM` passa do inteiro do registro.
- **Número reservado pelo Orquestrador:** cria fila e lacuna por uma coordenação manual.

## Consequências

Duas fatias não disputam número. **Piora:** uma migração datada anterior a uma já aplicada desloca
as posições; o `migration_order` barra isso no PR, e o registro acusa divergência na partida.
Editar a issue ou a descrição não roda o job: refaça o job ou empurre outro commit.

## O que reabre esta decisão

Uma instalação que aplique migrações em ordem diferente da do nome; um teto duro que a prática
mostre baixo ou alto.

Ver [regras de escrita](../regras-de-escrita.md).
