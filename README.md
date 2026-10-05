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
automática precisa passar. O guia de contribuição entra num pull request seguinte.

## Verificação local

```
python ferramentas/verifica-docs/verificar.py
python ferramentas/verifica-docs/verificar.py --apenas dados,links
python ferramentas/verifica-docs/verificar.py --lista
```

## Licença

**Ainda não definida.** Até que seja, ninguém tem permissão legal de usar, copiar ou derivar este
trabalho — é a consequência jurídica da ausência de licença, e o motivo de o repositório ser
privado.

## Privacidade

Nenhum dado real acompanha este repositório. Exemplos usam identificadores sintéticos. Nome de
pessoa, texto bruto de amostra, chave e credencial não entram em nenhum arquivo — nem em teste. A
verificação automática reprova o pull request que os contenha.
