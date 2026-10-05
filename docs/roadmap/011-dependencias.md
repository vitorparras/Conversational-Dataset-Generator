# Dependências

> Norma vigente. 2026-10-04.

Nenhuma ordem abaixo é conveniência. Cada uma existe porque invertê-la troca uma garantia por uma
dívida — e, quase sempre, por uma dívida que não se paga depois, porque o dano já ficou gravado em
algum lugar de onde não sai.

## As ordens que não se invertem

**Custódia antes de material real.** Cifra aplicada depois não descifra o passado: cópia de
segurança tirada antes dela sai em claro e continua em claro. Cópia tirada não se desfaz.

**Eixo de domínio antes da primeira tabela de conteúdo.** Acrescentar a coluna depois abre um
intervalo em que o isolamento depende de cada consulta ter o filtro certo — a opção que a decisão
[0006](../decisoes/0006-dominios-coexistem.md) recusa por escrito.

**Redação de log antes do primeiro log.** O mecanismo vale para frente, e
[021](../requisitos/021-registro-sem-dado-pessoal.md) não alcança o que já foi escrito.

**Invariante escrito antes do teste que o verifica.** Propriedade derivada do código confirma o
código contra si mesmo — consequência declarada da decisão
[0007](../decisoes/0007-garantia-de-qualidade.md), não preferência de estilo.

**Sanitização antes de pseudonimização.** Posição calculada sobre texto que ainda vai mudar aponta
para outro trecho depois, e é disso que
[009](../requisitos/009-anotacao-de-trecho-estavel.md) depende. Invertida, a normalização ainda pode
deformar o rótulo que substituiu a identidade, sem saber que aquilo era um rótulo.

**Pseudonimização antes de a coluna em claro receber material real.** A decisão
[0003](../decisoes/0003-cifra-em-repouso.md) aceita texto sanitizado em claro porque a ligação com a
identidade está no mapa cifrado. Antes disso, a coluna em claro guarda identidade.

**Acesso antes de anotação.** A anotação é o primeiro momento com mais de uma pessoa no acervo;
abrir antes é confiar em filtro de consulta, quando é o banco que precisa negar.

**Taxonomia antes de anotação.** Sem validação de rótulo na escrita, erro de digitação vira
categoria nova em silêncio, e a medida passa a relatar divergência que não houve.

**Procedência e independência antes da anotação de produção.**
[010](../requisitos/010-sugestao-nao-e-anotacao.md) e
[011](../requisitos/011-anotacoes-independentes.md) não se acrescentam depois: não há como dizer de
onde veio um rótulo já gravado, nem desfazer o fato de alguém ter visto o rótulo alheio.

**Sobreposição atribuída antes da medida.** Medir o que coincidiu por acaso é medir amostra de
conveniência: a proporção de itens com mais de uma anotação é escolhida, não observada.

**Taxonomia, domínio e contagens antes da exportação.** Sem os três dentro do artefato, recorte
misto e recorte de origem única são indistinguíveis, e o dataset deixa de ser citável.

**Segundo domínio identificado antes de a anotação começar.** Não é ordem de construção: é a
condição que a decisão [0001](../decisoes/0001-as-duas-contribuicoes.md) escreveu para reabrir a si
mesma. Anotar sem ela cumprida é deixar a condição vencer em silêncio.

## O que não está ordenado

A taxonomia de cada domínio — definição de categoria, exemplos do que inclui e do que não inclui —
não toca código e pode ser escrita a qualquer momento. É o trabalho substancial que não espera fase
alguma, e adiantá-lo encurta o caminho até a fase 7.
