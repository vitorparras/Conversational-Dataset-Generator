# 0029 — Identidade e sessão

> Decisão vigente. 2026-10-07.

## Contexto

O sistema decide o que cada pessoa alcança com base na função dela, e isso está escrito. O que não
estava é **como alguém prova ser quem diz ser** — e autorização impecável sobre identidade
indefinida não protege nada.

Sem decisão, quem implementar escolhe: pode guardar senha de um jeito frágil, manter sessão viva
indefinidamente, ou aceitar identidade de fora sem verificar.

## Decisão

**A identidade é local ao sistema**, com senha guardada por função de derivação própria para senha —
com fator de trabalho configurável e sal por pessoa. Nunca resumo criptográfico simples, que é
rápido demais para resistir a tentativa em massa.

**A sessão expira por inatividade e tem prazo máximo absoluto**, ambos configuráveis. Encerrar a
sessão invalida o que a identificava do lado do servidor: não basta o navegador esquecer.

**Toda troca com o sistema é cifrada em trânsito**, inclusive em rede local. Rede interna não é rede
confiável, e material sensível não trafega em claro por conveniência de instalação.

**Tentativa repetida de entrada é retardada e registrada.** O retardo cresce com as tentativas, e o
registro é de auditoria — quem tentou e quando, nunca a senha tentada.

Autenticação de dois fatores não é exigida, e a decisão é consciente: o sistema roda em instalação
controlada, com acesso por concessão nominal, e o custo operacional de um segundo fator numa equipe
pequena supera o ganho. O que reabre está abaixo.

## Recusados

**Identidade delegada a serviço externo.** Seria menos código e mais seguro em geral, e contraria a
operação sem dependência externa — que existe para permitir instalação isolada.

**Sessão sem prazo absoluto.** Sessão que só expira por inatividade sobrevive indefinidamente a
quem a mantenha ativa, inclusive a quem já perdeu o acesso.

**Registrar a senha tentada, mesmo cifrada.** Transformaria o registro de auditoria em depósito de
credenciais, e pessoas erram a senha digitando a de outro sistema.

## Consequências

Cada instalação administra suas próprias contas, e não há recuperação de senha por e-mail quando o
ambiente é isolado. A reposição é administrativa, feita por quem tem a função técnica, e registrada.

Trocar o fator de trabalho da derivação exige recalcular as senhas existentes no próximo acesso de
cada pessoa, o que leva tempo para se completar numa base inteira.

## O que reabre

Se o sistema passar a ser alcançável fora da instalação controlada, o segundo fator deixa de ser
custo desproporcional e passa a ser necessário.
