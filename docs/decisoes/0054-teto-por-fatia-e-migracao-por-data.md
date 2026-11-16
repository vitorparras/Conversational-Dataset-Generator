# 0054 — Teto do PR por fatia e migração nomeada por data e hora

> Decisão vigente. 2026-11-16.

## Contexto

Duas fatias paralelas que criam migração disputavam o mesmo número, e o limite de 30 arquivos
cortava fatias verticais que cabem em 400 linhas. A fila colidia por regra, não por defeito.

## Decisão

**O teto vem da issue da fatia, e a ordem das migrações vem do nome.**

1. **Teto:** o `pr_size` lê a linha "Teto da fatia: N linhas" da issue que o PR fecha (`Closes #n`,
   ou o número da branch). Sem a linha, ou com a issue ilegível, vale 400. O limite duro é 1.500
   linhas de produção, e a issue que declara mais reprova. Não há limite de arquivos.
2. **Migração nova:** `AAAAMMDDHHMM-nome`, com um instante real do calendário. As 54 numeradas
   ficam como estão e vêm antes. A versão registrada é a posição na ordem do nome, de um em diante.
3. **Teste:** exige ordem estritamente crescente, cada arquivo registrado no build, numeradas sem
   lacuna e nenhuma datada antes da última numerada.

## Alternativas recusadas

- **Renomear as antigas:** mexeria no que já foi aplicado e na comparação do registro.
- **Versão como o instante:** `AAAAMMDDHHMM` passa do inteiro do registro.
- **Número reservado pelo Orquestrador:** cria fila e lacuna por uma coordenação manual.

## Consequências

Duas fatias não disputam número. **Piora:** uma migração datada anterior a uma já aplicada desloca
as posições, e o registro acusa divergência na partida em vez de aplicar fora de ordem.

## O que reabre esta decisão

Uma instalação que aplique migrações em ordem diferente da do nome; um teto duro que a prática
mostre baixo ou alto.

Ver [regras de escrita](../regras-de-escrita.md).
