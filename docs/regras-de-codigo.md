# Regras de código

> Operacional. Vale para todo código fora de `docs/`; autorizada pela [decisão 0040](decisoes/0040-principios-de-codigo.md).

**Imposta** reprova o build; **lista de revisão** o revisor responde por escrito antes do PR.

## 1. Princípios (lista de revisão)

SOLID inteiro: responsabilidade única e inversão de dependência em toda classe; aberto/fechado,
substituição e segregação só nos pontos de extensão que a arquitetura declara substituíveis. Nome
diz o quê; função faz uma coisa, num só nível de abstração. DRY a partir da terceira repetição,
não antes. YAGNI: nada para necessidade sem requisito ou issue. Composição sobre herança: herança
só para contrato sem estado. DDD entra na Fase 2, quando houver domínio a modelar.

## 2. Limites (impostos: `Core.Tests.Architecture`, testes Python, `ruff`; dois itens em revisão)

Arquivo até 250 linhas; método até 40; até 4 parâmetros, senão objeto de parâmetros, mas método que
implementa explicitamente interface alheia herda a assinatura dela; aninhamento até 2 níveis; uma
classe pública por arquivo; tipo nomeado em vez de tupla em assinatura pública. Os testes Python são
`language-service/tests/architecture/` e `tools/tests/architecture/`. Em revisão: a tupla em Python
e a classe pública única em `tools/`.

## 3. Composição (imposta: `CompositionRootTests`, teste do serviço; interface, revisão)

Uma raiz de composição por executável, com a injeção de dependência nativa da plataforma; o ponto
de entrada só a chama, em até 20 linhas. Nenhum `new` de serviço fora da raiz. Interface só onde a
arquitetura declara substituição (revisão); o resto é classe concreta injetada.

## 4. Sem gancho de teste (imposto: `NoTestHookTests`, teste do serviço)

Nenhum membro público existe para teste. O teste observa comportamento. Internos visíveis só ao
projeto de teste do próprio módulo.

## 5. Estado e erros (revisão; impostos no `.editorconfig`: logger, S1312; captura, S2221)

Imutável por padrão. Máquina de estados por enumeração, nunca por texto. Concorrência confinada a
um tipo pequeno, testado isolado. Falha esperada volta como tipo de resultado; exceção é defeito.
Captura genérica só no último nível do executável. Mensagem a quem opera em português; mensagem de
exceção interna em inglês ([decisão 0039](decisoes/0039-idioma-do-codigo-e-do-texto.md)); uma
classe não mistura as duas. Logger injetado: construtor primário, nunca campo; S1312 sem exclusão.

## 6. Formatação (imposta; o resto, em revisão)

`dotnet format` e `ruff` reprovam na CI; `.editorconfig` e o projeto Python são a única fonte de
estilo. O `pyright` estrito impõe parâmetro anotado; revisão: retorno e variável anotados, dados
imutáveis, esquema só na borda HTTP. Supressão: [decisão 0042](decisoes/0042-supressao-de-analisador.md).

## 7. Testes (lista de revisão)

Arrange-Act-Assert; um comportamento por teste, nomeado por ele; sem condicional no teste.

## 8. Comentário (lista de revisão)

Documentação de API só na fronteira pública entre módulos; nos outros métodos, no máximo um sumário
de uma frase. Comentário no corpo é raro, curto, diz o porquê e nunca cita regra ou issue.

## 9. Pull request (limite imposto por `pr_size`; task única e descrição, em revisão)

Até 30 arquivos e 400 linhas de diff de produção; teste, lockfile e fixture ficam fora da conta, mas
listados. Uma task por pull request; a descrição cita o princípio de cada escolha não óbvia.
