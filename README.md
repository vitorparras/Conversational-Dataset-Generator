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
python ferramentas/verifica-docs/verificar.py
python ferramentas/verifica-docs/verificar.py --apenas dados,links
python ferramentas/verifica-docs/verificar.py --lista
```

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
