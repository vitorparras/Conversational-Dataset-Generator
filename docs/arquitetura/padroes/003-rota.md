# Rota

> Norma vigente. 2026-11-15.

Peça que liga um endereço HTTP a uma operação. Vive em `core/src/Core.Host/Endpoints`, uma classe
`internal static` com `Map` e um método por verbo.

## Exemplar

`core/src/Core.Host/Endpoints/ChainVerificationEndpoint.cs` para rota que só lê, e
`core/src/Core.Host/Endpoints/SealScreensEndpoint.cs` para tela com formulário.

- Toda rota declara quem a alcança com `ScreenAccess` nos metadados, inclusive a que devolve JSON
  (`ChainVerificationEndpoint`, linha 16; `SealScreensEndpoint`, linha 18, ligada nas linhas 26 a
  28). A aberta a todos que entraram diz `ScreenAccess.Everyone`.
- A rota não decide: chama a operação de estado (`ChainState`, `SealState`) e traduz o resultado.
  O `switch` sobre o desfecho cobre todos os valores e o resto é defeito (`SealScreensEndpoint`,
  linhas 57 a 64, o braço do resto na linha 63).
- Indisponível responde 503 pela página que diz isso (`ScreenResult.Unavailable`, linha 60, ou a
  própria tela com código 503, linha 85); POST que deu certo responde 303 com `PageRedirect.Of`
  (linha 59).
- Segredo e dado pessoal vão no corpo, nunca no endereço nem no log (comentário da classe).

## Princípio

Responsabilidade única e negação por padrão: a rota é a borda fina entre HTTP e domínio, e nada é
alcançado por omissão (`core/src/Core.Host/Endpoints/ScreenAccess.cs`, linhas 3 a 7).

## Exceção transitória

O exemplar lê o formulário com o ternário de HasFormContentType (linha 54), um de oito casos em
sete arquivos. Até a #817 trazer o leitor único, rota nova copia esse ternário como está e não
inventa um nono jeito.

## Proibido

- Rota sem `ScreenAccess`. A guarda responde 404 à rota que não declara acesso, página ou JSON, e
  `ScreenAccessDeclarationTests` reprova a rota mapeada sem a declaração.
- `Results.StatusCode(503)` cru numa tela (`DomainScreensEndpoint`, linhas 24 a 26); use a página.
  A rota de JSON responde o código cru (`ChainVerificationEndpoint`, linha 20).
- Duas escritas sem transação na mesma rota (`OperatorRegistration`, linhas 32 e 38).
- Proteção contra envio forjado é o `SameSite=Strict` do cookie (`SessionCookie`, linha 8); rota
  que mude estado por GET quebra essa defesa.
