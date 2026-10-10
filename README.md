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

## Núcleo

O núcleo é uma solução .NET em `core/Core.slnx`. A versão exata do SDK está em `global.json`, e
as fontes de pacote, em `core/nuget.config`.

```
dotnet restore --locked-mode core/Core.slnx
dotnet build core/Core.slnx
dotnet test core/Core.slnx
dotnet run --project core/src/Core.Host
```

Com o núcleo de pé, `http://127.0.0.1:5080/status` responde se o processo está vivo, se está apto
a atender e o resultado de cada conferência feita antes de abrir a porta; a rota de configuração, no
mesmo endereço, mostra a configuração em vigor com a origem de cada valor.

Os arquivos que acompanham a instalação são declarados em `Installation:BundledFiles`, separados
por ponto e vírgula, e a falta de qualquer um impede a inicialização.

| Código de saída | Significado |
|---|---|
| 0 | Encerramento pedido (Ctrl+C ou SIGTERM), concluído |
| 1 | Falha inesperada |
| 2 | Configuração recusada |
| 3 | Conferência de inicialização reprovada |

A configuração é fechada: chave desconhecida, chave exigida ausente ou valor fora do aceito impedem
a inicialização, e a mensagem nomeia a chave. São três fontes, e a de cima vence:

| Fonte | Forma |
|---|---|
| Argumento | `--Server:Port=5081` ou `--Server:Port 5081` |
| Ambiente | `CDG__Server__Port=5081` |
| Arquivo | `core/src/Core.Host/appsettings.json`, copiado para junto do executável |

O que nenhuma fonte declara usa o padrão da chave, quando ela tem um. O registro vai ao console e,
se `Logging:FilePath` tiver um caminho, também a esse arquivo; campo marcado como material,
identidade ou segredo sai apagado nos dois.

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
