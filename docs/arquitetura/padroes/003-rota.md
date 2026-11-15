# Rota

> Norma vigente. 2026-11-15.

Peça que liga um endereço HTTP a uma operação. Vive em `core/src/Core.Host/Endpoints`, uma classe
`internal static` com `Map` e um método por verbo.

## Exemplar

`core/src/Core.Host/Endpoints/ChainVerificationEndpoint.cs` para rota que só lê, e
`core/src/Core.Host/Endpoints/SealScreensEndpoint.cs` para tela com formulário.

- Toda rota declara quem a alcança com `ScreenAccess` nos metadados, inclusive a que devolve JSON
  (`ChainVerificationEndpoint`, linha 16; `SealScreensEndpoint`, linhas 39 e 45 a 50). A aberta a
  todos que entraram diz `ScreenAccess.Everyone`.
- A rota não decide: chama a operação de estado (`ChainState`, `SealState`) e traduz o resultado.
  O `switch` sobre o desfecho cobre todos os valores e o resto é defeito (`SealScreensEndpoint`,
  linhas 78 a 85).
- Indisponível responde 503 pela página que diz isso (`ScreenResult.Unavailable`, ou a própria
  tela com código 503, linhas 105 e 106); POST que deu certo responde 303 com `PageRedirect.Of`.
- Segredo e dado pessoal vão no corpo, nunca no endereço nem no log (comentário da classe).

## Princípio

Responsabilidade única e negação por padrão: a rota é a borda fina entre HTTP e domínio, e nada é
alcançado por omissão (`core/src/Core.Host/Endpoints/ScreenAccess.cs`, linhas 3 a 7).

## Proibido

- Rota sem `ScreenAccess`. Hoje a guarda deixa passar a rota que não é página e não declara
  acesso; a correção está em issue própria, e até ela entrar a declaração é obrigatória no plano e
  na revisão.
- `Results.StatusCode(503)` cru numa tela (`DomainScreensEndpoint`, linhas 24 a 26); use a página.
- Ler formulário com o ternário de HasFormContentType repetido (`SealScreensEndpoint`, linha 75,
  entre oito casos). Copie como está até a refatoração do leitor único; não invente um nono jeito.
- Duas escritas sem transação na mesma rota (`OperatorRegistration`, linhas 32 e 38).
- Proteção contra envio forjado é o `SameSite=Strict` do cookie (`SessionCookie`, linha 8); rota
  que mude estado por GET quebra essa defesa.
