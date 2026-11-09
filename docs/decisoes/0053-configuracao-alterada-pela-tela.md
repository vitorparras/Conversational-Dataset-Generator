# 0053 — Configuração alterada pela tela, com aprovação de outra pessoa

> Decisão vigente. 2026-11-08.

## Contexto

O [requisito 032](../requisitos/032-configuracao-falha-fechada.md) faz a configuração falhar
fechada, e a mudança do que protege é deliberada, sem recarga. A #634 pede que a administração
técnica a proponha pela tela, sem escrita no arquivo pela rede e com duas pessoas, como o
[requisito 030](../requisitos/030-exportacao-exige-duas-pessoas.md) pede para a exportação.

## Decisão

**A tela propõe, outra pessoa aprova, e só o aplicador escreve no arquivo.**

1. **Submissão:** a chave, o valor novo e um motivo da lista fechada. O núcleo passa a configuração
   resultante pelo mesmo carregador da partida; se ele recusar, a tela recusa com a chave e o valor
   nomeados, e nada se grava. A submissão aceita entra na cadeia como evento.
2. **Aprovação:** outra pessoa ativa com a função, nunca quem submeteu, aprova ou recusa. O evento
   aponta a submissão pela sequência. A pendente expira no prazo e não se aprova mais.
3. **Aplicação:** um modo próprio do executável, em serviço separado, sob a única conta do sistema
   operacional com escrita no arquivo e permissão de reiniciar o núcleo. Lê do banco, só com
   leitura, o aprovado e não aplicado; confere na cadeia os dois eventos, os autores distintos,
   ativos e com a função, e que a cadeia fecha. Valida de novo, grava o arquivo de forma atômica
   com a versão anterior guardada e reinicia o núcleo. Na partida, a cadeia registra a alteração
   em vigor. Quando não aplica, o log do servidor leva o motivo e as sequências, nunca o nome nem o
   endereço de pessoa ([requisito 021](../requisitos/021-registro-sem-dado-pessoal.md)).

**O que a tela altera é uma lista positiva no código do aplicador**: chave nova nasce fora dela.
Nunca entram segredos e credenciais, a custódia da chave (0043 a 0045), o certificado, o endereço
de escuta e o banco. Fora da lista, a recusa avisa que a chave só muda no servidor.

**Em aberto para o dono**, com a recomendada (a) valendo até a escolha:
- **Motivos:** (a) cinco códigos: endurecer proteção, ajuste operacional, correção de valor errado,
  exigência de contrato ou norma, resposta a incidente; (b) três: correção, ajuste, incidente.
- **Prazo:** (a) chave, 72 h por padrão, de 1 a 168 h, só no servidor; (b) sem padrão; (c) fixo.
- **Chaves da tela:** (a) tempos e trabalho: `Session:IdleTimeout`, `Session:AbsoluteTimeout`,
  `Passwords:WorkFactor`, `Custody:VerificationInterval`, `Audit:CompareInterval` e os dois prazos
  do serviço de linguagem; (b) a (a) mais `Ingestion:Conditions` (condições de origem, roteiro 023)
  e `Custody:SealSituation` (situação do selo).

## Alternativas recusadas

- **O núcleo grava o arquivo:** o processo exposto à rede ganharia escrita no que o protege.
- **Recarga sem reinício:** o 032 não exige, e a mudança do que protege segue deliberada.
- **Lista das chaves na configuração:** quem altera a configuração alteraria a própria lista.
- **Lista negativa:** a chave nova nasceria alterável, e a omissão liberaria.
- **Motivo em texto livre:** levaria à cadeia o que alguém escrevesse, dado pessoal inclusive.

## Consequências

A instalação ganha uma conta e um serviço; a conta do núcleo perde a escrita no arquivo, e a do
aplicador não tem escrita no banco. **Piora:** cada alteração reinicia o núcleo, o que encerra as
sessões em memória ([0035](0035-reposicao-encerra-sessoes.md)); com uma só pessoa na função, nada
muda pela tela. A edição à mão no servidor continua possível e não entra na cadeia.

## O que reabre esta decisão

Exigência de recarga sem reinício; um incidente que precise mudar chave da lista antes que uma
segunda pessoa responda; um aplicador que precise de escrita no banco.
