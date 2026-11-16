# Teste de navegador

> Norma vigente. 2026-11-15.

Teste num Chromium real, pelo Playwright, contra o host real. Vive em
`core/tests/Core.Host.Tests.Browser` e é marcado `[Trait("Category", "Browser")]`.

## Exemplar

`core/tests/Core.Host.Tests.Browser/Screens/NotAppliedChangesScreenTests.cs`.

- O navegador e o host vêm de fixtures de classe do xunit (`BrowserFixture` e
  `ChangeHostFixture`), injetadas pelo construtor primário (linha 14). A fixture de host é selada
  e compõe o host: guarda a instância, sobe uma vez e descarta no fim.
- A sessão entra por `ScreenSession.SignedInAsync`, com uma pessoa que o host já criou
  (linha 65).
- `OpenAsync` abre a tela e espera o título pelo papel de cabeçalho, não por tempo; se falhar,
  descarta a aba antes de relançar (linhas 63 a 77).
- O teste localiza por papel ou por `aria-labelledby` (linha 17), a mesma marca que o leitor de
  tela usa. O auxiliar comum de entrada (`ScreenSession`, linhas 21 a 24) localiza por id do
  formulário de entrada; é o único lugar em que isso vale.
- A acessibilidade se confere com `AccessibilityCheck.AssertNoViolationsAsync` e
  `RealBackgroundContrast.AssertAaAsync`, em claro e escuro (`MediaMode`), a 1280 px e na largura de
  celular, com a verificação de transbordo (linhas 42 a 61).
- O teste afirma também o que **não** pode aparecer: valor e nome de pessoa (linhas 36 a 39).

## Princípio

O teste de navegador verifica o que só o navegador mostra: foco, papel, contraste, largura. A regra
do negócio já foi testada abaixo ([002](002-servico-de-dominio.md), [005](005-teste-com-banco.md)).

## Host por composição

Todo host de teste é um `ScreenHost` selado, montado por `ScreenParts`. As partes são:

- `Services` e `Screens`: os serviços e as rotas da tela, atrás do pipeline padrão.
- `Pipeline`: o pipeline inteiro, só quando o padrão não serve (entrada, prova sem guarda de tela).
- `Prepare` e `Finish`: o que se escreve antes de subir e depois de existirem as pessoas.
- `Configuration`, `WithPeople` (falso quando o caso faz as próprias pessoas) e `Disposing`.

O `*Case` tem duas formas, pelo que o teste precisa guardar:

- `sealed class` que envolve o `ScreenHost`: o caso tem estado ou ajudantes próprios (a pessoa
  extra, o log, o banco semeado). A fixture de classe expõe o `Case` (exemplar: `ChangeCase`).
- `static class` que só devolve o `ScreenParts`: o host basta como está. A fixture sobe o
  `ScreenHost` com `ScreenHost.StartAsync` e expõe o próprio host (exemplar: `ChainCase`).

A pessoa extra de um caso nasce com `ScreenHost.AddPersonAsync`, que devolve o identificador; não
releia o id por nome. Não existe classe-base para herdar: host novo é um `*Case` novo com o seu
`ScreenParts`, conforme a regra 1 de [regras de código](../../regras-de-codigo.md).

## Proibido

- `Task.Delay` para esperar a tela (`EntryScreenTests`, linhas 89 e 177;
  `ResponseWithoutRegionTests`, linha 44): use `Expect` do Playwright, que espera a condição.
- Classe abstrata nova com estado para herdar, ou mais um host montado à parte do `ScreenHost`
  e do `ScreenParts`.
- Entrar com login copiado em vez de `ScreenSession`.
- Localizar por classe CSS, por id ou por texto de layout no teste quando há papel acessível.
