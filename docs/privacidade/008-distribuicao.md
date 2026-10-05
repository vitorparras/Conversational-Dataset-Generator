# 008 — Licenciamento e distribuição de dados

> Norma vigente. 2026-10-04.

O material não acompanha a ferramenta. Quem clona este repositório recebe código, documentação e
exemplos com identificador sintético — nunca acervo. O diretório de dados não é versionado, por
`.gitignore`, e a verificação automática reprova o pull request que traga dado pessoal ou segredo.

São duas entregas e **dois instrumentos separados**, pela
[decisão 0009](../decisoes/0009-licenca-do-software-e-dos-dados.md), que é ela própria a pendência:
nenhuma das duas licenças está escolhida, e o regime interino é a ausência — ninguém tem permissão
de usar, copiar ou derivar este trabalho, nem de contribuir sabendo o que cede. O
[README](../../README.md) explica a consequência jurídica.

Duas coisas já vinculam. **Nada é publicado antes de as duas licenças existirem**, nem código nem
amostra. E a dos dados não pode prometer redistribuição irrestrita: pela
[decisão 0012](../decisoes/0012-pseudonimizacao-reversivel.md), enquanto o mapa de reversão existir
o material é dado pessoal. O que segue é o piso que esse instrumento precisa satisfazer, não ele.

## Um dataset derivado só sai se

Veio de exportação que passou pela verificação de integridade e pela auditoria de resíduo de dado
pessoal. A primeira está em [verificação](005-verificacao-da-custodia.md). A segunda é a última
defesa contra o que o detector não encontrou na ingestão. As duas bloqueiam: achado interrompe a
exportação, em vez de virar ressalva num relatório que ninguém lê.

Declara o próprio recorte no artefato — quais domínios, sob quais taxonomias, com quais contagens
—, pelas decisões [0004](../decisoes/0004-taxonomia-configuravel.md) e
[0006](../decisoes/0006-dominios-coexistem.md). Sem isso, um dataset de conjunto misto é
indistinguível de outro com origem diferente.

Carrega o próprio resumo criptográfico e o topo da cadeia no momento da exportação, para que quem
recebe verifique que tem o que foi gerado. O mesmo resumo entra na cadeia como evento: o acervo
registra o que saiu, quando e para quem.

Não leva mapa de reversão, nem chave, nem texto bruto, em nenhuma circunstância e sob nenhuma
autorização. Isso não é concessão negociável — é a diferença entre distribuir um dataset e
distribuir um acervo.

## O que o receptor assume

Não tentar reidentificar ninguém, por método nenhum, cruzamento com outra base incluído. Não
redistribuir além do que o instrumento permitir. Manter o material sob controle de acesso ao menos
equivalente ao daqui. Relatar incidente. E citar a origem pela identidade exata da exportação —
domínio, taxonomia e resumo —, porque citar "o dataset" sem isso impede que alguém reproduza a
medida publicada.

## A parte que nenhum mecanismo impõe

Depois que o arquivo sai, nada técnico sustenta o parágrafo acima. É obrigação assumida, cobrável
em palavra e em reputação, e é tudo.

Daí a consequência de desenho: **o controle acontece antes.** O que merece cuidado é o que entra na
exportação, não o que o termo proíbe fazer com ela. Cláusula é remédio depois do dano; recorte
estreito é prevenção.

Por isso também não existe exportação de conveniência fora do caminho auditado. Cópia de tela,
despejo de banco e consulta exportada à mão não são exportações: são vazamentos com boa intenção, e
a [cadeia de custódia](004-cadeia-de-custodia.md) não os registra — o que significa que ninguém
sabe que aconteceram.
