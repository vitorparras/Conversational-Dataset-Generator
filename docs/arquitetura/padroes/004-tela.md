# Tela

> Norma vigente. 2026-11-23.

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

## Usabilidade e visual

Tela nova, a partir da fase 5, segue a [0058](../../decisoes/0058-padrao-de-usabilidade-e-visual.md):

- Título de até cinco palavras; abaixo dele, no máximo uma linha de situação (até 90 caracteres).
  Sem parágrafo explicativo; regra de negócio vai num recolhido "Como funciona".
- Um `Button` principal por tela; os outros, secundário ou link, depois dele; perigo só para o que
  não se desfaz. O tipo de cada botão é escolhido na tela.
- O secundário (quem fez e quando, detalhe do registro, filtro de lista curta) vai recolhido.
- Nada de painéis seguidos; situação de registro é marca, e aviso é só retorno de ação, numa frase.
- "Todos os campos são obrigatórios" uma vez; dica de campo só quando o formato não é óbvio.

`screens.css` fica como está. As classes que os protótipos da fase 5 propõem (recolhido, linha de
situação, lista em linhas) entram como componente quando a fatia de tela precisar delas. O PR de
tela traz capturas em 390x844 e 1366x768, no tema do sistema; quem revisa confere nelas estas
regras e a primeira dobra ([022](../022-revisao-manual-de-acessibilidade.md)).

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
