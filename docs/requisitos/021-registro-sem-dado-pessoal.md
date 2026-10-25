# 021 — O registro de operação não contém dado pessoal

> Norma vigente. 2026-10-04.

**Nenhum registro de diagnóstico, erro ou medição guarda texto de material, identidade de pessoa ou
segredo — nem quando a falha acontece no meio do processamento.**

## Por que existe

A [decisão 0002](../decisoes/0002-stack.md) escolhe que a redação de campo sensível aconteça no
próprio caminho do registro, e dá o motivo: assim vira mecanismo, e não disciplina de quem escreve a
mensagem. Disciplina falha exatamente na hora errada — no tratamento de uma exceção inesperada, onde
despejar o que estava em memória é o reflexo mais comum.

A mesma decisão manda dizer o que nunca entra em registro no tráfego até o serviço de processamento
de linguagem, porque é ali que texto sensível atravessa fronteira de processo.

## Critérios de aceitação

- Dado uma exceção durante a sanitização ou a pseudonimização, quando ela é registrada, então a
  mensagem não contém o texto que estava sendo processado.
- Dado um campo marcado como sensível, quando ele atravessa qualquer registro, então aparece
  redigido, independentemente de quem escreveu a chamada.
- Dado o serviço de processamento de linguagem, quando ele recebe e devolve texto, então nem ele nem
  o núcleo retêm esse texto fora do acervo protegido.
- Dado um registro qualquer, quando alguém o lê para diagnosticar, então ele traz identificador do
  item, passo e motivo — o bastante para investigar sem o conteúdo.

A prova é um teste que executa o fluxo completo com material semeado e falha se qualquer trecho dele
aparecer no registro: `core/tests/Core.Host.Tests/Privacy/` (`LogWithoutContentTests` no caminho
normal; `KilledServiceLogTests` e `InFlightDeathLogTests` no de erro) e
`language-service/tests/privacy/test_log_destinations.py` (um só destino no serviço).

## O que não exige

Não exige registro sem identificador de item: identificador sozinho não identifica pessoa, e sem ele
não há diagnóstico possível. Não exige desligar o registro, que é o que permite investigar falha sem
abrir o acervo.
