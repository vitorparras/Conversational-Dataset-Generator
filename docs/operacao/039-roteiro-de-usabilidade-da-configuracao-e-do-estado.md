# 039 — Roteiro de usabilidade: configuração em vigor e estado do sistema

> Operacional. 2026-11-06.

Preenche o modelo de [034](034-roteiro-de-usabilidade.md) para as duas telas de leitura da
administração, as issues 603 (`/configuracao`) e 604 (`/estado-do-sistema`). As pessoas de teste
são `operador-10`, com a função de Administração técnica, `operador-11`, com a de Auditoria, e
`operador-12`, com a de Anotação, criadas só para o ensaio, com a senha combinada para o ensaio e
descartadas no fim. Quem executa não é quem escreveu a tela.

## Antes de executar

| Campo | O que vai |
|---|---|
| Telas | Configuração em vigor e Estado do sistema, nas issues 603 e 604 |
| Quem usa | Quem administra o servidor ou audita o sistema, sem treino na tela |
| Ponto de partida | Núcleo no ar com o reconhecedor simulado, uma conferência da subida falha, a entrada feita |
| Sucesso | Acha a resposta na tela, e a que a tela não dá, entende que não dá |
| Limite | 30 s e 2 passos do início até a resposta; acima disso é difícil |

## Tarefas

1. **Saber se o sistema pode ser usado.** Como `operador-10`, pergunte se o sistema está pronto
   para trabalhar e, se não, o que falhou. A pessoa tem de separar "vivo" de "apto" e dizer qual
   conferência falhou e onde ela se confere.
2. **Achar uma versão.** Peça a versão do núcleo e a do esquema do banco. Anote o que ela diz da
   versão do serviço de linguagem, que aqui não é obtida: tem de ler como "não obtida", não como
   erro.
3. **De onde veio um valor.** Na configuração, peça de onde veio o valor da chave `Host:Urls`.
4. **Um segredo.** Peça o valor da chave de guarda da cópia de segurança (`Custody:Holder`). A
   pessoa tem de entender que o sistema não mostra o valor, só o nome e a origem, e que isso não é
   falha da tela.
5. **Mudar um valor.** Peça que mude o valor de uma chave. A pessoa não pode procurar botão por
   mais de 10 s; tem de dizer que a tela só mostra e que a mudança é no servidor, e onde.
6. **Sem acesso.** Entre como `operador-12` e peça que digite o endereço da configuração. A
   resposta tem de ser a de um endereço que não existe, sem sugerir que a tela existe.

## Variações

Repita as tarefas 1 e 3 só pelo teclado, depois com tema escuro, transparência reduzida e
contraste alto do sistema, e com a janela na largura de um celular (390 px), onde a tabela rola
dentro da sua região. No celular, anote se a pessoa acha a coluna da origem sem ajuda, porque ela
fica à direita e exige rolar a tabela. Repita a tarefa 2 como `operador-11`.

## Registro

| Tarefa | Teclado | Tema | Tempo | Passos | Resultado | O que travou |
|---|---|---|---|---|---|---|
| 1 a 6, uma linha por tarefa e por variação | | | | | | |

Resultado "ajuda" ou "desistência" abre item novo na issue da tela, como em 034.

## Fora do roteiro

A mudança de um valor, submetida e aprovada por outra pessoa, é da issue 634: até lá a tarefa 5
só confere que a tela diz que a mudança é no servidor. A comparação do registro contra a cadeia e
o aviso do selo aparecem só quando há fato real, e entram no roteiro quando a instalação tiver
um. O serviço de linguagem em uso e as versões que ele declara dependem do reconhecedor real.
A prova de que a tela negada e a inexistente são iguais é do teste de navegador e do
`ScreenGateTests`.
