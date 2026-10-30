# 004 — Pré-ensaio de subida isolada

> Histórico. 2026-10-30.

Registro de um **pré-ensaio feito por agente**, para achar lacunas no texto antes do ensaio formal
da [#50](https://github.com/vitorparras/Conversational-Dataset-Generator/issues/50) e da
[#264](https://github.com/vitorparras/Conversational-Dataset-Generator/issues/264). **Não é esse
ensaio**: quem ensaiou foi um agente (Claude Code), não uma pessoa, e o reservado a ele segue em
aberto. É o que valeu naquela máquina, naquele dia; não é regra nem previsão.

## Preparo

O artefato foi construído do commit `ab91d09` (main, árvore limpa), em contêiner Linux x64. A
primeira construção falhou ao montar a pasta do serviço direto no disco do Windows, que não
distingue maiúsculas: o arquivo `terminfo` colidiu (`[Errno 17] File exists`). O procedimento
passou a montar a pasta dentro de um contêiner e copiá-la ao destino em fluxo tar, sem passar por
esse disco. Quem constrói num disco que não distingue maiúsculas precisa disso.

## O ambiente

- Contêiner `debian:12-slim`, 2 GB de memória, em rede interna, sem rota nem DNS externos, sem
  volume, porta nem socket. A prova de isolamento passou nos 7 itens
- Banco PostgreSQL 17 (imagem de `tools/database`) como irmão na mesma rede, com os papéis `owner` e
  `app`. Endereço, porta e credenciais foram dados em ficha de texto, fora do contêiner
- Chave-mestra ausente: espera a decisão D1 da #53
- Pacotes: a base tinha 88. Instalou-se só `libssl3` (3.0.22-1~deb12u2), que a base não traz; já
  estavam `libc6` 2.36-9+deb12u14, `libgcc-s1` e `libstdc++6` 12.2.0-14+deb12u1

## O artefato

| Parte | SHA-256 |
|---|---|
| nucleo/Core.Host | `6c387186f3385c4fcb48c300578e91894dc012d2e956e318430e28772c147b90` |
| nucleo/appsettings.json | `06c8a2065371cec6d32edeb9881b4cb7023aae11c380a87fe88a37744fc4a2ed` |
| servico/SHA256SUMS | `ea7d26b4ce72a4b7ebfbd2471c730a1d7fa8499b31ec0a82c72e67d4cd9b3987` |
| servico/ORIGIN.json | `4b224eefb064e36967c0bd3c303325aa3ac9edc1f148b21ef37c5488b3285f49` |

O `SHA256SUMS` cobre 13319 arquivos da pasta do serviço. Dentro do contêiner, tudo conferiu com
esses valores.

## Rodada 1

Quem ensaiou recebeu só o `README.md`, a pasta `docs/` desse commit e a ficha do banco, e operou só
dentro do contêiner. Resultado: o critério de subida (núcleo e serviço de pé, banco no esquema 3,
conferências `Passed`, uma análise concluída) e o de queda do serviço (503 com `ConnectionRefused` e
nada gravado) passaram. Nenhum passo passou de 15 minutos. Houve oito improvisos:

| # | Improviso | Correção |
|---|---|---|
| 1 | Pasta do serviço posta fora da pasta do executável; a partida recusou | README, 011, 018 e [021](../operacao/021-subir-o-nucleo-publicado.md) dizem "dentro" |
| 2 | Sem cliente HTTP, `ps` nem `python3` na máquina | [022](../operacao/022-conferir-o-nucleo-publicado.md) consulta por bash; [001](../operacao/001-ambiente.md) |
| 3 | Valor de `language` desconhecido | README e 022: qualquer texto; a Fase 1 o ignora |
| 4 | Como manter o núcleo de pé e onde fica o registro | 021: redirecionamento do console |
| 5 | Porta do banco em `Host`? Argumento ou variável? Caixa | README e 021: porta em `Database:Host`, senha em variável, caixa sem efeito |
| 6 | Senha sem terminal | 021: arquivo de modo 600 lido no shell; sem `docker exec -e` |
| 7 | Achar e derrubar o serviço sem `ps` | 022: varredura de `/proc` e o processo no registro |
| 8 | Conferir o banco sem cliente SQL | 022: o 503 e o registro bastam na Fase 1; o banco, de fora |

Escolha sobre as ferramentas do operador (improviso 2): o procedimento usa só o que a máquina tem,
em vez de a 001 listar cliente HTTP, `ps` e cliente SQL como exigência. O artefato não instala nada
no destino, e essas ferramentas servem a quem opera, não ao sistema; listá-las faria delas
dependência dele.

Lacunas sem improviso: o aviso `libgssapi_krb5.so.2` na partida com banco (opcional; 001 e 021); o
cerco da credencial numa demonstração sem cadeia (021 diz o que vale); o 400 sem corpo para
`resumes` que não é GUID (README e 022); `modelVersion` nulo e zero ocorrências, que é o esperado
da Fase 1, cujo reconhecedor é o vazio (`EmptyRecognizer`) e não carrega o modelo (022). Um achado
de código ficou fora do texto: a recusa `manifesto ausente` não diz onde procurou
(`ServiceFolderCheck.cs:37`, `ServiceFolder.cs:19`); foi levado ao Arquiteto para issue.

As correções acima mexem só em texto.

## Rodada 2

Ambiente novo: contêiner e banco recriados do zero, o mesmo artefato de `ab91d09` conferido pelos
mesmos sha256, ficha nova com outras senhas, e só o `README.md` e a pasta `docs/` do commit
`e4ae18d` ao alcance. **Isenção declarada pelo operador:** o reset não apagou a memória da rodada
1, e a rodada 2 foi feita pelo mesmo agente, lembrando dela. Ele diz que não abriu o arquivo da
rodada 1 nem usou auxiliar dela, mas não garante que a lembrança não ajudou. Os resultados da
rodada 2 valem menos que os de uma rodada limpa; a rodada 3 será de outro agente.

Resultado: subida e queda do serviço passaram de novo, com quatro improvisos e duas lacunas de
texto, nenhum passo acima de 15 minutos.

| # | Improviso | Correção |
|---|---|---|
| 1 | A função `rota` não enviava `Content-Type`, e a análise respondeu 415 sem corpo | 022 envia o cabeçalho e cita o 415 |
| 2 | O `&` do 021 (hoje 023, que ficou com a partida) supõe um shell que continua vivo; por `docker exec` não se sabia como manter o núcleo | 021 dá a forma com `docker exec -d` e `exec` |
| 3 | O 021 só tinha o arquivo de senha do papel da aplicação, e o `migrate` pede o do dono | 021 grava um arquivo por papel e dá o comando do `migrate` |
| 4 | Sem `ps`, o PID do núcleo para o SIGTERM | 022 ganha a função `pid`, para o núcleo e para o serviço |

As lacunas eram erros meus no texto da rodada 1, e o operador os achou: o laço de procura do
022 tinha o `tr` sem o `\0`, e o bloco de partida do 021 vinha com as quebras de linha trocadas
por espaços. A causa é do meu modo de escrever os arquivos (um script que interpretou as barras
invertidas), e o 022 chegou a ter bytes nulos no meio dele. Os dois blocos foram reescritos,
e cada comando corrigido foi rodado de fora do ambiente do operador, num contêiner descartável
de mesma base: `migrate` (0 e depois 10), partida com `docker exec -d` e com `&` (saída 0 ao
SIGTERM), `rota` com 200, 503 e 400, e `pid` achando o núcleo e o serviço. Nesse teste o `&` por
`docker exec` sem terminal também sobreviveu; o texto passou a dizer que ele depende disso.
