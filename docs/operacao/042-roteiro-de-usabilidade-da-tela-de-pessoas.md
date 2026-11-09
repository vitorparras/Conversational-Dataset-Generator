# 042 — Roteiro de usabilidade: tela de pessoas, listar e cadastrar

> Operacional. 2026-11-08.

Preenche o modelo de [034](034-roteiro-de-usabilidade.md) para a tela de pessoas, na issue 599.
Quem usa a tela é `operador-11`, com a função Administração técnica; a pessoa que ela cadastra é
`operador-12`, com a função Anotação e a senha inicial combinada para o ensaio. As duas são criadas
só para o ensaio e descartadas no fim. Quem executa não é quem escreveu a tela.

## Antes de executar

| Campo | O que vai |
|---|---|
| Telas | Pessoas (`/pessoas`) e Cadastrar pessoa (`/pessoas/nova`), na issue 599 |
| Quem usa | Quem administra o sistema, sem treino |
| Ponto de partida | Núcleo no ar, `operador-11` ativa, mais de 20 pessoas cadastradas (para haver página 2) |
| Sucesso | Acha a tela pelo menu; filtra e muda de página sem se perder; cadastra na primeira tentativa certa ou corrige o nome repetido sem refazer o resto |
| Limite | 20 s e 2 passos para abrir a lista; 30 s e 3 passos para achar alguém pelo nome; 60 s e 4 passos para cadastrar; acima disso é difícil |

## Tarefas

1. **Achar a lista** da página inicial. Pergunte o que cada coluna diz. O estado se lê pela
   palavra (Ativa ou Inativa), e não só pelo sinal ou pela cor, e nada de material aparece.
2. **Achar uma pessoa pelo nome**, por um pedaço dele; depois pela função e pelo estado. Pergunte
   como soube que o filtro valeu e como o desfaria (o botão Limpar filtro).
3. **Mudar de página**, voltar pelo botão do navegador e passar o endereço a outra pessoa da
   administração: a lista abre na mesma página e com o mesmo filtro. Anote se entende "Página 1
   de 3, 20 por página".
4. **Cadastrar `operador-12`**: nome, função Anotação e senha inicial. Pergunte o que espera ver
   depois de enviar (a lista filtrada pelo nome novo, com o aviso de cadastrada sem concessão de
   domínio) e como passará a senha a `operador-12`.
5. **Nome repetido.** O formulário volta com o erro ao lado do nome, o digitado continua nos
   campos e a senha não volta. A pessoa corrige só o nome.
6. **Entrar como `operador-12`**, em outro navegador, com a senha inicial. A página inicial diz que
   nenhum domínio foi concedido, sem menu. Pergunte a quem ela pediria acesso.

## Variações

Repita as tarefas 1, 2 e 4 só pelo teclado, depois com tema escuro, transparência reduzida e
contraste alto do sistema, e com a janela na largura de um celular (390 px), onde o formulário não
pode obrigar a rolar para o lado. Na tarefa 2, observe se a pessoa percebe que o filtro só vale ao
acionar Filtrar (ou Enter no campo), e não enquanto digita.

## Registro

| Tarefa | Teclado | Tema | Tempo | Passos | Resultado | O que travou |
|---|---|---|---|---|---|---|
| 1 a 6, uma linha por tarefa e por variação | | | | | | |

Resultado "ajuda" ou "desistência" abre item novo na issue da tela, como em 034.

## Fora do roteiro

A senha de teste não vai a captura de tela, chat nem registro. A prova de que a senha inicial não
aparece na página, no endereço, no log nem no banco, e de que quem não é da administração recebe a
página de um endereço inexistente, é dos testes de navegador do cadastro de pessoas.
