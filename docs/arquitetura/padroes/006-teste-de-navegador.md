# Teste de navegador

> Norma vigente. 2026-11-15.

Teste num Chromium real, pelo Playwright, contra o host real. Vive em
`core/tests/Core.Host.Tests.Browser` e é marcado `[Trait("Category", "Browser")]`.

## Exemplar

`core/tests/Core.Host.Tests.Browser/Screens/NotAppliedChangesScreenTests.cs`.

- O navegador e o host vêm de fixtures (`BrowserFixture` e uma fixture de host que herda de
  `ScreenHostBase`), injetadas pelo construtor primário (linha 14).
- A sessão entra por `ScreenSession.SignedInAsync`, com uma pessoa que o host já criou
  (linha 65).
- `OpenAsync` abre a tela e espera o título pelo papel de cabeçalho, não por tempo; se falhar,
  descarta a aba antes de relançar (linhas 63 a 77).
- O localizador é por papel ou por `aria-labelledby`, a mesma marca que o leitor de tela usa.
- A acessibilidade se confere com `AccessibilityCheck.AssertNoViolationsAsync` e
  `RealBackgroundContrast.AssertAaAsync`, em claro e escuro (`MediaMode`), a 1280 px e na largura de
  celular, com a verificação de transbordo (linhas 42 a 61).
- O teste afirma também o que **não** pode aparecer: valor e nome de pessoa (linhas 36 a 39).

## Princípio

O teste de navegador verifica o que só o navegador mostra: foco, papel, contraste, largura. A regra
do negócio já foi testada abaixo ([002](002-servico-de-dominio.md), [005](005-teste-com-banco.md)).

## Proibido

- `Task.Delay` para esperar a tela (`EntryScreenTests`, linhas 89 e 177;
  `ResponseWithoutRegionTests`, linha 44): use `Expect` do Playwright, que espera a condição.
- Host de teste que não herda de `ScreenHostBase`; hoje `SystemScreenHost`, `NavigationScreenHost` e
  `EntryScreenHost` montam o host à parte.
- Entrar com login copiado (`SystemSession`) em vez de `ScreenSession`.
- Localizar por classe CSS ou por texto de layout quando há papel acessível.
