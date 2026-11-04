# 021 — Telas de auditoria

> Descrição. 2026-11-04.
> Estado no código: nenhuma tela existe; as rotas estão em `core/src/Core.Host/Endpoints/`.

Função de auditoria: consulta de eventos e acessos sem conteúdo (requisitos 021 e 023). As demais
telas da função estão em outros documentos, listados abaixo.

| ID | Tela | Função | Mostra | Faz | Fase | Issue |
|---|---|---|---|---|---|---|
| U1 | Eventos e acessos | Auditoria | Quem, quando, qual item, sob qual finalidade (a vigente no momento), recusas; nenhum conteúdo | Filtra por pessoa, domínio, período | 6 | #631 (regra #117) |

## Telas da função em outros documentos

| ID | Tela | Documento | Issue |
|---|---|---|---|
| A7 | Custódia | [015](015-telas-administracao-tecnica.md) | #605 |
| A8 | Registro do servidor | [015](015-telas-administracao-tecnica.md) | #606 |
| R2 | Autorizar reversão | [017](017-telas-reversao-esvaziamento-e-relatos.md) | #615 |
| U2 | Relatos de dado pessoal | [017](017-telas-reversao-esvaziamento-e-relatos.md) | #618 |

## Observações

- A U1 mostra a finalidade vigente no momento do acesso, não a atual: renovar a concessão não
  reescreve o passado.
- A recusa aparece na lista como evento, sem dizer se o item existe (requisito 017).
- Nenhuma tela da auditoria mostra texto de item; só identificadores e contagens.
