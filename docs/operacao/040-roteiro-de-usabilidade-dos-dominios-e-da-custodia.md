# 040 — Roteiro de usabilidade: domínios e custódia

> Operacional. 2026-11-06.

Preenche o modelo de [034](034-roteiro-de-usabilidade.md) para as duas telas de leitura da
administração que vêm depois da configuração, as issues 602 (`/dominios`) e 605 (`/custodia`, com
a confirmação em `/custodia/verificar`). As pessoas de teste são `operador-13`, com a função de
Administração técnica, `operador-14`, com a de Auditoria, e `operador-15`, com a de Curadoria do
acervo e nenhum domínio concedido, criadas só para o ensaio, com a senha combinada para o ensaio e
descartadas no fim. Quem executa não é quem escreveu a tela.

## Antes de executar

| Campo | O que vai |
|---|---|
| Telas | Domínios (issue 602) e Custódia, com a confirmação da verificação (issue 605) |
| Quem usa | Quem administra o servidor ou audita o sistema, sem treino na tela |
| Ponto de partida | Núcleo no ar, a entrada feita; um ambiente com dois domínios, um sem nenhum e um com a cadeia quebrada por fixture |
| Sucesso | Acha a resposta na tela, e a que a tela não dá, entende que não dá |
| Limite | 30 s e 2 passos do início até a resposta; o pedido de verificação, 60 s e 4 passos |

## Tarefas

1. **Ver os domínios.** Como `operador-13`, no ambiente com dois domínios, pergunte quais
   domínios existem. Anote o que ela diz das colunas de versão da taxonomia e das dimensões, que
   aqui dizem "Nenhuma carregada": tem de ler como "ainda não há versão", não como erro.
2. **Nenhum domínio.** No ambiente sem domínio, pergunte o que ela faria para ter o primeiro. Tem
   de achar o comando do servidor, `create-domain`, e dizer que a tela não cria.
3. **Sem acesso.** Entre como `operador-15` e peça que digite o endereço dos domínios. A resposta
   tem de ser a de um endereço que não existe, sem sugerir que a tela existe.
4. **Saber se a cadeia está íntegra.** Como `operador-13`, pergunte se a cadeia de custódia
   confere, desde quando e quantos eventos foram lidos.
5. **Cadeia quebrada.** No ambiente com a cadeia quebrada, peça que diga qual evento foi o
   primeiro a não conferir e o que isso significa para o domínio afetado: só aceita leitura.
6. **Pedir uma verificação agora.** Peça uma, na tela da custódia. Ela lê a página de confirmação
   e envia. Depois tem de achar o próprio nome no histórico, como quem pediu.
7. **Pedir de novo.** Logo em seguida, peça outra. Tem de entender a espera.
8. **Refazer a cadeia.** Peça que refaça a cadeia quebrada. Tem de dizer que a tela não tem esse
   caminho: a verificação só lê e registra, e não conserta nada.

## Variações

Repita as tarefas 4 e 6 só pelo teclado, depois com tema escuro, transparência reduzida e
contraste alto do sistema, e com a janela na largura de um celular (390 px), onde as tabelas rolam
dentro da sua região. No celular, anote se a pessoa acha a coluna "Pedida por" sem ajuda. Repita a
tarefa 4 como `operador-14`.

## Registro

| Tarefa | Teclado | Tema | Tempo | Passos | Resultado | O que travou |
|---|---|---|---|---|---|---|
| 1 a 8, uma linha por tarefa e por variação | | | | | | |

Resultado "ajuda" ou "desistência" abre item novo na issue da tela, como em 034.

## Fora do roteiro

A versão real da taxonomia e das dimensões entra quando o modelo as guardar; até lá a tarefa 1 só
confere que a tela diz que não há. A criação de domínio pela tela não existe: é do servidor. O
selo externo real e o aviso do selo dependem de uma instalação com selo registrado.
