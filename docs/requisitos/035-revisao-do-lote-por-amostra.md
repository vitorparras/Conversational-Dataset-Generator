# 035 — A revisão do lote é por amostra, e reprova inteiro

> Norma vigente. 2026-10-07.

**Antes de um lote ser aprovado, uma pessoa revisa uma amostra dele. Qualquer dado pessoal
encontrado reprova o lote inteiro.**

## Por que existe

A revisão do lote é o último ponto em que o material pode ser recusado antes de entrar no acervo.
Ela existe porque o reconhecimento automático não encontra tudo, e é a única barreira anterior à
entrada.

Rever o lote inteiro não é possível: um lote pode ter dezenas de milhares de mensagens, e revisão
que ninguém consegue fazer é revisão que não acontece. Amostra é o que torna a etapa real.

**A amostra aqui não estima taxa — decide aceitação.** Não há meta percentual a atingir, nem
intervalo a calcular: um achado basta para reprovar. É gate, e gate é categórico.

Reprovar o lote inteiro, e não só o item, é consequência de como o lote existe: ele é a unidade que
entra ou não entra. Um item com dado pessoal encontrado na amostra significa que o reconhecimento
falhou naquele material, e nada garante que falhou só ali.

## Critérios de aceitação

**CA-1** — *Dado* um lote aguardando aprovação, *quando* a revisão for feita, *então* ela cobre uma
amostra de tamanho declarado na configuração do domínio, sorteada sem que quem revisa a escolha.

**CA-2** — *Dado* que a revisão encontrou dado pessoal não tratado, *quando* for registrada,
*então* o lote inteiro é reprovado, e nenhum item dele entra no acervo.

**CA-3** — *Dado* um lote reprovado, *quando* a reprovação for registrada, *então* a cadeia guarda
quem revisou, o que foi encontrado e quando — e o registro permanece ainda que o lote nunca entre.

**CA-4** — *Dado* um lote reprovado, *quando* alguém consultar o acervo, *então* nada dele é
alcançável: não há item em estado intermediário nem resíduo do que foi lido.

**CA-5** — *Dado* um lote aprovado, *quando* a aprovação for registrada, *então* ela diz qual
amostra foi revisada, por quem e quando.

## O que não exige

Não exige que quem revisa examine o que o reconhecimento já marcou: o alvo é o que escapou, não o
que foi tratado.

Não exige corrigir o lote reprovado dentro do sistema. A correção acontece fora — ajustando o
reconhecimento, ou o material de origem — e o lote é submetido de novo, como lote novo.

Não exige amostra representativa de cada categoria ou forma: a amostra é do lote, e lote não tem
categoria antes de ser anotado.
