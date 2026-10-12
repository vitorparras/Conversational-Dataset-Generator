# Regras de código

> Operacional. Vale para todo código fora de `docs/`; autorizada pela [decisão 0040](decisoes/0040-principios-de-codigo.md).

O que é mensurável é imposto por analisador, `.editorconfig` ou teste de arquitetura, e reprova o
build. O resto é a lista que o revisor responde por escrito antes de o pull request abrir.

## 1. Princípios

SOLID inteiro: responsabilidade única e inversão de dependência em toda classe; aberto/fechado,
substituição e segregação só nos pontos de extensão que a arquitetura declara substituíveis. Nome
diz o quê; função faz uma coisa, num só nível de abstração. DRY a partir da terceira repetição,
não antes. YAGNI: nada para necessidade sem requisito ou issue. Composição sobre herança: herança
só para contrato sem estado. DDD entra na Fase 2, quando houver domínio a modelar.

## 2. Limites (impostos)

Arquivo até 250 linhas; método até 40; até 4 parâmetros, senão objeto de parâmetros; aninhamento
até 2 níveis; uma classe pública por arquivo; tipo nomeado em vez de tupla em assinatura pública.

## 3. Composição (imposta)

Uma raiz de composição por executável, com a injeção de dependência nativa da plataforma; o ponto
de entrada só a chama, em até 20 linhas. Nenhum `new` de serviço fora da raiz. Interface só onde a
arquitetura declara substituição; o resto é classe concreta injetada.

## 4. Sem gancho de teste (imposto)

Nenhum membro público existe para teste. O teste observa comportamento. Internos visíveis só ao
projeto de teste do próprio módulo.

## 5. Estado e erros

Imutável por padrão. Máquina de estados por enumeração, nunca por texto. Concorrência confinada a
um tipo pequeno, testado isolado. Falha esperada volta como tipo de resultado; exceção é defeito.
Captura genérica só no último nível do executável. Mensagem a quem opera em português; mensagem de
exceção interna em inglês ([decisão 0039](decisoes/0039-idioma-do-codigo-e-do-texto.md)); uma
classe não mistura as duas.

## 6. Formatação (imposta)

`dotnet format` e `ruff` reprovam na CI; `.editorconfig` e o projeto Python são a única fonte de
estilo. Python: tipo anotado em tudo, em modo estrito; dados imutáveis; esquema só na borda HTTP.

## 7. Testes

Arrange-Act-Assert; um comportamento por teste; o nome diz o comportamento; sem condicional no
teste. Um teste de arquitetura impõe as regras 2, 3 e 4.

## 8. Comentário

Documentação de API só na fronteira pública entre módulos; nos outros métodos, no máximo um sumário
de uma frase. Comentário no corpo é raro, curto, diz o porquê e nunca cita regra ou issue.

## 9. Pull request

Uma task por pull request; até 30 arquivos e 400 linhas de diff de produção; teste, lockfile e
fixture fora da conta, mas listados. A descrição cita o princípio de cada escolha não óbvia. O
revisor responde esta lista por escrito antes de o pull request abrir.
