# Conversational Dataset Generator

Ferramenta para construir **bases conversacionais anotadas em português brasileiro**: ingestão de
formatos variados, sanitização, pseudonimização que preserva contexto, anotação humana assistida,
validação por métricas de concordância e exportação de datasets rastreáveis.

O domínio **forense/criminal é o estudo de caso mais restritivo**, não o escopo da ferramenta.

Projeto de pesquisa aplicada em processamento de linguagem natural para o português
brasileiro.

## Estado

Repositório em fundação. Contém o aparato de verificação e revisão; a documentação entra em
seguida, um pull request por documento.

## Como isto funciona

| Onde | O que vive lá |
|---|---|
| `docs/` | **A fonte da verdade.** Requisitos, decisões, políticas, arquitetura |
| Issues | As tarefas para chegar ao que `docs/` descreve |

Toda mudança passa por pull request com aprovação do dono do repositório, e a verificação
automática precisa passar. Ver [`CONTRIBUTING.md`](CONTRIBUTING.md), e [`AGENTS.md`](AGENTS.md) para
a hierarquia de autoridade entre os documentos.

## Verificação local

```
python tools/verify-docs/verify.py
python tools/verify-docs/verify.py --only data,links
python tools/verify-docs/verify.py --list
```

O código passa pelo mesmo comando que a verificação contínua executa, na mesma ordem:

```
python tools/verify-code/verify.py
python tools/verify-code/verify.py --list
```

## Núcleo

O núcleo é uma solução .NET em `core/Core.slnx`. A versão exata do SDK está em `global.json`, e a
única fonte de pacotes, em `core/nuget.config`. Os comandos abaixo rodam a partir da raiz do
repositório, nesta ordem:

```
dotnet restore --locked-mode core/Core.slnx
dotnet build core/Core.slnx
dotnet test core/Core.slnx
dotnet run --project core/src/Core.Host -- --urls http://127.0.0.1:5080
```

Com o núcleo de pé, `http://127.0.0.1:5080/status` responde `{"alive":true}`. O teste de
arquitetura em `core/tests/Core.Tests.Architecture` impõe as regras de código a cada construção;
as fixtures que ele reprova de propósito ficam em `core/tests/fixtures/code-rules`, fora de
qualquer projeto.

## Licença

O software é [AGPL-3.0](LICENSE): qualquer um pode usar, estudar, modificar e redistribuir,
inclusive comercialmente, e quem modificar — ou oferecer o sistema como serviço pela rede —
publica as alterações sob a mesma licença.

**O material e os datasets não acompanham o software e não se licenciam.** O acesso é concedido
caso a caso, por contrato que restringe o uso a finalidade acadêmica ou de pesquisa.

## Privacidade

Nenhum dado real acompanha este repositório. Exemplos usam identificadores sintéticos. Nome de
pessoa, texto bruto de amostra, chave e credencial não entram em nenhum arquivo — nem em teste. A
verificação automática reprova o pull request que os contenha.
