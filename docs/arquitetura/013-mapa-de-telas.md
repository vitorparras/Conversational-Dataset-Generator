# 013 — Mapa de telas

> Descrição. 2026-11-04.
> Estado no código: nenhuma tela existe; as rotas estão em `core/src/Core.Host/Endpoints/`.

Toda operação de quem usa o sistema passa por uma tela; a linha de comando fica com a gestão do
servidor: instalação, migração, cópia, restauração, chave, ensaio de recuperação, primeira pessoa
e primeiro domínio. A origem do mapa é a #589. Os padrões comuns de navegação, lista, formulário,
confirmação, ato de duas pessoas, negativa uniforme, sessão vencida e acessibilidade (WCAG 2.1 AA)
são da [0051](../decisoes/0051-navegacao-gestao-e-relatorio.md); a tecnologia e a anotação, da
[0010](../decisoes/0010-tecnologia-da-interface.md).

## Funções

| Função | Fluxo | Documentos |
|---|---|---|
| Toda pessoa | Entrar, ver o que alcança, trabalhar, sair | [014](014-telas-entrada-sessao-e-conta.md) |
| Administração técnica | Cadastrar pessoas, repor senha, ver domínios, configuração, estado e custódia | [015](015-telas-administracao-tecnica.md) |
| Curadoria do acervo | Submeter, acompanhar, revisar, ler o acervo, declarar campanha, pedir exportação e reversão, autorizar a reversão de outra pessoa, decidir relatos de dado pessoal | [016](016-telas-ingestao-e-acervo.md), [019](019-telas-campanha-avaliacao-e-parecer.md), [020](020-telas-exportacao-e-comparacao.md) |
| Anotação | Lista de trabalho, anotar, relatar dado pessoal | [018](018-telas-anotacao-e-arbitragem.md) |
| Arbitragem | Fila de divergências, arbitrar às cegas | [018](018-telas-anotacao-e-arbitragem.md) |
| Avaliação | Andamento, concordância, parecer do recorte | [019](019-telas-campanha-avaliacao-e-parecer.md) |
| Auditoria | Eventos e acessos, registro do servidor, conferir a custódia | [021](021-telas-auditoria.md), [015](015-telas-administracao-tecnica.md) |
| Quem responde pelo acervo | Conceder, renovar e revogar acesso (nunca a si mesmo); aprovar exportação | [015](015-telas-administracao-tecnica.md), [020](020-telas-exportacao-e-comparacao.md) |
| Função própria do esvaziamento | Esvaziar um item por identificador | [017](017-telas-reversao-esvaziamento-e-relatos.md) |

## Áreas

| Área | Telas | Documento | Fase |
|---|---|---|---|
| Entrada, sessão e conta | E1–E5, C1–C3 | [014](014-telas-entrada-sessao-e-conta.md) | 3, 7 |
| Administração técnica | A1–A8 | [015](015-telas-administracao-tecnica.md) | 3, 6 |
| Ingestão e acervo | I1–I4, K1–K3 | [016](016-telas-ingestao-e-acervo.md) | 3, 6, 7 |
| Reversão, esvaziamento, relatos | R1–R3, V1, U2 | [017](017-telas-reversao-esvaziamento-e-relatos.md) | 3, 5, 7 |
| Anotação e arbitragem | N1, N2, B1, B2 | [018](018-telas-anotacao-e-arbitragem.md) | 7, 8 |
| Campanha, avaliação e parecer | P1, P2, M1, M2 | [019](019-telas-campanha-avaliacao-e-parecer.md) | 6, 8 |
| Exportação e comparação | X1–X6 | [020](020-telas-exportacao-e-comparacao.md) | 9, 10 |
| Auditoria | U1 | [021](021-telas-auditoria.md) | 6 |

## Transversal

- Teste de navegador na CI, com o Playwright para .NET: #590.
- Acessibilidade das telas, nível WCAG 2.1 AA: #591.
- Fora das telas: a criação da primeira pessoa e do primeiro domínio, que a tela só sinaliza.
