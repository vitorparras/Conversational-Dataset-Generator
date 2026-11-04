# 016 — Telas de ingestão e do acervo

> Descrição. 2026-11-04.
> Estado no código: nenhuma tela existe; as rotas estão em `core/src/Core.Host/Endpoints/`.

Função de curadoria do acervo: entra material, acompanha o lote, revisa por amostra e lê o acervo
na versão que a função alcança ([006](../privacidade/006-acesso.md)).

| ID | Tela | Função | Mostra | Faz | Fase | Issue |
|---|---|---|---|---|---|---|
| I1 | Submeter material | Curadoria | Domínio, origem declarada e condição, limite de tamanho; porta bloqueada sem ensaio de recuperação | Envia o arquivo, validado antes de gravar | 3 | #607 (regra #75, #85, #584) |
| I2 | Lotes | Curadoria | Estado (em curso, aceito, rejeitado), motivo, quem submeteu, tamanho; o rejeitado continua listado | Abre o lote | 3 | #608 (regra #74) |
| I3 | Lote | Curadoria | Itens, procedência, reenvio, perda declarada, conteúdo replicado, trechos por destino (contagem) | Acompanha | 3 | #609 (regra #76–#82, #84) |
| I4 | Revisão por amostra | Curadoria | A amostra sorteada, sem escolha de quem revisa, trecho a trecho | Registra achado; aprova ou reprova o lote | 3 | #610 (regra #83) |
| K1 | Acervo do domínio | Curadoria | Itens com filtro por lote, origem, estado; paginação; vazio sem concessão | Filtra, abre o item | 6 | #611 (regra #111) |
| K2 | Item | Curadoria | Texto na versão da função, procedência, trilha da sanitização, marca de esvaziado | Lê e registra o acesso | 6 | #612 (regra #117) |
| K3 | Taxonomia, glossário, critérios | Curadoria, anotação | Categorias, dimensões, glossário, decisões de critério com vigência | Só leitura | 7 | #613 |

## Observações

- A revisão por amostra é do lote inteiro: aprovar ou reprovar vale para o lote, não para o item
  (requisito 035). A #83 deixou a interface para outra fase; a tela é a #610.
- A K2 registra o acesso com a finalidade vigente (requisito 023); a versão mostrada depende da
  função: pseudonimizada, sanitizada ou bruta.
- A edição de taxonomia continua em arquivo versionado; a K3 só lê (decisão
  [0004](../decisoes/0004-taxonomia-configuravel.md)).
- Glossário e critérios já aparecem dentro da anotação (#132, #133); a K3 é a leitura avulsa.
- A I1 diz, antes de aceitar o arquivo, se a porta está bloqueada por falta de ensaio.
- Acessibilidade das listas e dos formulários: #591.
