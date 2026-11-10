# 047 — Roteiro de usabilidade: selo da custódia

> Operacional. 2026-11-09.

Preenche o modelo de [034](034-roteiro-de-usabilidade.md) para a tela do selo (`/custodia/selo`,
issue 736), conferido com a tela do selo no main (#751). As pessoas de teste são
`operador-21`, com a função de Administração técnica, e `operador-22`, com a de Auditoria, criadas
para o ensaio, com a senha combinada, e descartadas no fim. Quem executa não é quem escreveu a tela.

## Antes de executar

| Campo | O que vai |
|---|---|
| Telas | Selo da custódia, o aviso de selo ausente e os dois caminhos até ela (Custódia e Estado) |
| Quem usa | Administração técnica, sem treino na tela, que nunca emitiu um selo |
| Ponto de partida | Núcleo no ar; um ambiente com selo manual e nenhum selo registrado, e um sem selo manual |
| Sucesso | Emite, guarda o texto fora da base, confirma e vê o aviso sumir; entende o que o texto prova |
| Limite | 30 s e 2 passos do aviso até a tela; emitir e confirmar, 3 min e 6 passos |

## Tarefas

1. **Do aviso à tela.** Como `operador-21`, no ambiente com selo manual, abra o estado do sistema.
   Pergunte o que o aviso pede. Tem de achar "Selo: emitir e confirmar" sem ajuda, também pela
   Custódia.
2. **Emitir.** Peça que emita o selo. Tem de achar "Emitir selo" e ler a frase "Guarde este texto
   onde quem controla a base não alcance." Pergunte onde o guardaria e por quê.
3. **O texto só aparece agora.** Peça que saia da página e volte. O texto não está mais lá, e
   "Último selo registrado" ainda diz "Nenhum selo registrado": emitir não grava nada. Pergunte se
   algo se perdeu: o selo só é gravado ao confirmar, e "Emitir selo" gera um novo.
4. **Confirmar com o texto alterado.** Cole o texto com um caractere trocado em "Selo guardado" e
   "Confirmar selo". O motivo tem de vir junto do campo, o texto digitado tem de continuar lá, e
   nada foi gravado.
5. **Confirmar.** Cole o texto inteiro e confirme. Tem de ler que o selo foi gravado na cadeia, com
   o nome de quem fez, e ver o aviso de selo ausente sumir do estado do sistema.
6. **A espera.** Ao emitir e ao confirmar, pergunte o que a tela diz enquanto responde. Tem de ler
   o aviso de espera e não conseguir enviar de novo. Pergunte se o selo foi para o endereço: o
   texto não aparece nele em nenhum momento.
7. **Sem selo manual.** No outro ambiente, abra o endereço do selo. A tela tem de dizer que o
   núcleo envia o selo sozinho, uma vez por dia, e não oferecer botão nem campo.
8. **Outra função.** Como `operador-22`, digite `/custodia/selo`. A resposta tem de ser a de um
   endereço que não existe, e a Custódia dela não traz o botão.
9. **Voltar.** Em "Voltar à custódia", pergunte como saber que o selo foi aceito e anote onde a
   pessoa procura a prova.

## Variações

Repita as tarefas 2 e 5 só pelo teclado. Antes de emitir, a ordem é o atalho para o conteúdo, o
nome do sistema, a contagem de avisos, o menu, "Emitir selo", "Selo guardado", "Confirmar selo" e
"Voltar à custódia"; depois de emitir, o texto do selo vem antes de "Emitir outro selo". O foco
nunca fica encoberto pelo cabeçalho. Repita com tema escuro, transparência reduzida e contraste
alto do sistema, e com a janela de um celular (390 px), onde o menu vem recolhido. Anote se o texto
do selo cabe e se pode ser selecionado inteiro com Ctrl+A.

## Registro

| Tarefa | Teclado | Tema | Tempo | Passos | Resultado | O que travou |
|---|---|---|---|---|---|---|
| 1 a 9, uma linha por tarefa e por variação | | | | | | |

Resultado "ajuda" ou "desistência" abre item novo na issue da tela, como em 034.
