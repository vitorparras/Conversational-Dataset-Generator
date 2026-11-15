# Tela

> Norma vigente. 2026-11-15.

Componente Razor que a rota entrega dentro da moldura comum das telas. Vive em
`core/src/Core.Host/Screens`.

## Exemplar

`core/src/Core.Host/Screens/PasswordScreen.razor`.

- A tela só desenha: recebe por `[Parameter]` o que mostrar e não chama serviço, banco nem sessão.
- O que pode dar errado é uma enumeração fechada da própria tela (`Problem`, linhas 26 a 33), e cada
  valor tem um lugar fixo: o erro de campo vai no `Error` do `Field`, a falha geral num `Notice` de
  tom `Error` no topo do formulário (linhas 7 a 16).
- Os nomes dos campos e o título são constantes da tela (linhas 35 a 42), que a rota e o teste usam;
  nenhum texto de campo se repete fora dela.
- Peças comuns em vez de HTML solto: `Field`, `Button`, `Notice`. Formulário com `novalidate`,
  `autocomplete` correto em cada campo (linhas 11, 13 e 15) e aviso de espera ligado por
  `data-wait` (linhas 6 e 17).
- O texto ao usuário é português, diz o que aconteceu e o que fazer, e não revela o que não deve
  (uma só mensagem para senha errada e para espera, comentário da linha 25).

## Princípio

Separação entre apresentação e decisão: a rota ([003](003-rota.md)) decide o estado e a tela o
mostra, o que deixa a tela testável por renderização e a regra testável sem navegador. Os padrões
comuns de navegação, formulário, confirmação e acessibilidade são da
[0051](../../decisoes/0051-navegacao-gestao-e-relatorio.md).

## Exceção transitória

Há dois jeitos de aviso de espera: o componente WaitNotice e o parágrafo com `class="wait"` do
exemplar (seis telas entre os dois). Até a #818 escolher o jeito único, tela nova copia o do
exemplar e não cria um terceiro.

## Proibido

- Estado de falha como vários `bool` soltos ou texto livre; as telas de hoje variam entre formatos.
  Tela nova usa uma enumeração como `Problem`.
- Aviso de resultado por cookie em tela nova: o retorno de um POST bem-sucedido é marca no endereço,
  como `RecordedMark` em `core/src/Core.Host/Endpoints/SealScreensEndpoint.cs`.
- Nome de pessoa, valor de configuração ou segredo no HTML que não é o próprio campo da pessoa.
