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
  volume, porta nem socket. A prova de isolamento passou nos 7 itens: sem rota padrão; DNS externo
  (`example.com`, `deb.debian.org`) sem resposta; TCP a `1.1.1.1:443` e `8.8.8.8:53` sem conexão;
  DNS interno resolvendo o banco (`db`); TCP ao banco em `db:5432`
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
(`ServiceFolderCheck.cs:37`, `ServiceFolder.cs:19`); virou a issue
[#447](https://github.com/vitorparras/Conversational-Dataset-Generator/issues/447).

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

As lacunas eram erros do DEVOPS, autor do texto, na rodada 1, e o operador os achou: o laço de
procura do 022 tinha o `tr` sem o `\0`, e o bloco de partida do 021 vinha com as quebras de linha
trocadas por espaços. A causa foi o modo como o DEVOPS escreveu os arquivos (um script que interpretou as
barras invertidas), e o 022 chegou a ter bytes nulos no meio dele. Os dois blocos foram reescritos,
e cada comando corrigido foi rodado de fora do ambiente do operador, num contêiner descartável
de mesma base: `migrate` (0 e depois 10), partida com `docker exec -d` e com `&` (saída 0 ao
SIGTERM), `rota` com 200, 503 e 400, e `pid` achando o núcleo e o serviço. Nesse teste o `&` por
`docker exec` sem terminal também sobreviveu; o texto passou a dizer que ele depende disso.

## Rodada 3

Ambiente novo de novo, com os docs do commit `34acefb`. O operador foi o Frontend, um agente que
nunca operou o projeto. Ele disse não ter usado nada da memória de projeto do time, só os
documentos permitidos. Subida e queda do serviço passaram, e nenhum passo passou de 15 minutos.
Houve três improvisos e uma lacuna:

| # | Improviso | Correção |
|---|---|---|
| 1 | Sem critério de pronto da partida; ele adivinhou um `sleep 15` | 023 manda esperar a linha `Now listening on`, ou a saída do núcleo, com limite de 60 s |
| 2 | `cat > arquivo # Ctrl-D` supõe terminal, e não há com `docker exec` | 021 dá a forma de fora, `printf` na entrada do `docker exec -i`, sem quebra de linha no fim |
| 3 | Sem como gravar `/tmp/f.sh` a partir de fora | 022 grava as funções por `docker exec -i` com documento aqui, e chama por outro |
| 4 | (lacuna) O código de saída 0 do SIGTERM não se lia com `docker exec -d` | 023: o shell pai grava o código em `/tmp/nucleo.saida` |

Cada comando corrigido foi rodado, extraído literalmente dos documentos, num contêiner descartável:
`migrate` (10, esquema em dia), partida com `-d` pronta em 4 a 5 s, saída 0 ao SIGTERM lida no
arquivo, e senha recusada dando saída 69 em 1 s, com a causa no registro.

## O que se achou sobre os resets

Os `/clear` dessa sessão não funcionavam: o Git Bash convertia o caminho do comando. Por isso a
memória da rodada 1 sobreviveu na rodada 2, o que o operador já declarara, e a rodada 2 vale menos
que uma rodada limpa. A rodada 3 não teve esse problema, porque foi feita por outro agente. Com o
caminho sem conversão, o reset passou a funcionar, e a rodada 4 conta com isso.

## Rodada 4

O operador foi o Backend, com o contexto zerado de verdade, e os docs do commit `6c81d4f`. Subida,
queda do serviço e encerramento com saída 0 passaram, com quatro improvisos e quatro lacunas.

| # | Improviso | Correção |
|---|---|---|
| 1 | `/tmp/...` reescrito pelo Git Bash do Windows em todo `docker exec` (ele sabia por uma nota de memória, não pelo texto) | 021 diz `MSYS_NO_PATHCONV=1` num parágrafo único |
| 2 | Só a partida e as senhas tinham a forma de fora; as aspas aninhadas da espera não se copiavam | Todo bloco passa a ser comando de um shell na máquina, e a forma de fora é uma só: o bloco inteiro em `docker exec -i CONTEINER bash -s` com documento aqui |
| 3 | Sem critério de que o núcleo sumiu depois do SIGTERM | 023: `/tmp/nucleo.saida` deixa de estar vazio, com limite de 30 s |
| 4 | `sleep 2` chutado depois do `kill -9` | 022 consulta o `/status` até aparecer `Unavailable`, com limite de 30 s |

Lacunas: a segunda linha `Error: libgssapi_krb5...`, que assusta, passou a constar do aviso (023);
a senha vinha de um arquivo da ficha e foi digitada, e agora é lida dele com `sed` (021);
as senhas não eram apagadas, e 023 as apaga no fim; e "nada gravado" continua provado só pelo corpo
do 503 e pelo registro, pois o operador não tem cliente SQL.

A decisão de forma foi do Arquiteto: o procedimento é escrito para um shell na máquina, com comandos
simples. Os blocos foram rodados do jeito do operador, do Git Bash, cada um inteiro em `docker exec
-i bash -s`, num banco novo: `migrate` (0), partida (volta em 4 s), espera (1 s), rotas, `kill -9`
com espera pelo `Unavailable`, SIGTERM com saída 0, e senha recusada dando saída 69 em 1 s. Sem
`MSYS_NO_PATHCONV=1`, o `ls /tmp` do contêiner vira um caminho do Windows, como o texto avisa.

## Rodada 5

O operador foi o Backend, sem memória do sistema (tinha só um índice de notas do projeto, que não
ensina a operar), com os docs do commit `58282b2` sem este registro. Subida, queda do serviço,
encerramento com saída 0 e `migrate` com 0 passaram, e nenhum valor foi adivinhado: banco, usuários,
porta e senhas vieram da ficha, e o resto do texto. Houve três improvisos leves:

| # | Improviso | Correção |
|---|---|---|
| 1 | O texto não dizia se o núcleo em segundo plano sobrevive ao fim do `docker exec` nem se a partida e a espera cabem num bloco | 023 diz que ele sobrevive e que a espera pode ir no mesmo bloco ou noutro |
| 2 | Sem comando para ler a linha `interrompida` no registro | 022 acrescenta a leitura, com espera, porque a linha chega um instante depois da resposta |
| 3 | Conferências a mais que o texto não pede (listar pastas, contar bytes das senhas) | Nada a corrigir |

Também ficaram dois ajustes de texto: o 021 diz que `FICHA` e `CONTEINER` são marcadores a trocar,
com exemplos de caminho no Git Bash, e a função `rota` do 022 passou a terminar a saída com uma
quebra de linha, porque o corpo da resposta não a traz e a linha seguinte do terminal vinha colada
nele. Os blocos foram rodados de novo do jeito do operador, inteiros em `docker exec -i bash -s`,
com a partida e a espera no mesmo bloco, o núcleo vivo na chamada seguinte, a linha `interrompida`
lida pelo 022 e o encerramento com saída 0.

## Rodada 6

O operador foi o Backend, de novo sem memória do sistema, com os docs do commit `66b3381` sem este
registro. Subida (CA-1), queda do serviço (CA-2) e encerramento com saída 0 passaram, com o
`migrate` em 0 e **nenhum improviso**: cada comando veio de um bloco do texto, trocando só os
marcadores `FICHA` e `CONTEINER` e pondo `MSYS_NO_PATHCONV=1`. Uma observação, sem improviso: o
`pid` do 023 vem do `/tmp/f.sh` criado no 022, e quem encerrasse sem passar pelo 022 não o teria.

**Ressalva.** "Nada parcial gravado" foi provado só pelo corpo do 503 e pela linha `interrompida` do
registro, sem consulta SQL, porque a máquina do operador não tem cliente SQL. Na Fase 1 também não
há tabela que receba análise, de modo que a consulta não teria o que mostrar.

## Conclusão

Passou sem improviso na rodada 6, por agente. Isso encerra o pré-ensaio: o texto de 021, 022 e 023
basta a um agente sem memória do sistema para subir o núcleo publicado, ver a queda do serviço e
encerrar, num contêiner Debian sem rede, com banco irmão. **Não é o ensaio da #50 nem da #264.** Foi
um agente, num ambiente montado por quem escreveu o texto, e a rodada 2 teve memória residual da 1
(o reset que não funcionava). O que vale daqui é o texto corrigido, não uma aprovação. Os commits
citados (`e4ae18d` a `66b3381`) são desta branch; com squash, sobrevivem na referência do PR #450.

## O que fica para o ensaio formal, por uma pessoa (#50 e #264)

- **Uma pessoa**, que nunca viu o projeto, com o tempo dela medido; o pré-ensaio mediu agentes
- **O artefato publicado** pelo fluxo de empacotamento, com os hashes dele, e não o construído
  localmente do commit; e o mesmo para o destino `win-x64`, que não foi tocado
- **Uma máquina**, e não um contêiner de rede interna: usuário do serviço, permissões da pasta,
  como o núcleo é mantido de pé e reiniciado, e o isolamento de rede do ambiente de verdade
- **Instalação real**, com a chave-mestra (decisão D1 da #53, que ficou de fora) e o cerco da
  credencial de [010](../operacao/010-credencial-de-dono-do-banco.md): a verificação da cadeia antes
  e depois do `migrate` e o registro da janela, que a demonstração dispensou
- **Guarda da chave, backup e restauração** ([002](../operacao/002-guarda-da-chave.md),
  [005](../operacao/005-backup.md), [006](../operacao/006-restauracao.md)), fora do pré-ensaio
- **Conferência no banco** por quem opera o banco, com cliente SQL, de que a análise recusada não
  deixou nada; só faz sentido quando houver tabela que receba análise
- **Reconhecedor com modelo**: o da Fase 1 é o vazio, e `modelVersion` nulo e zero ocorrências não
  provam o serviço de linguagem em uso real
- **A mensagem de `LanguageServiceFolder`**, que não diz onde procurou (`ServiceFolderCheck.cs:37`),
  fica para a issue #447 e para o texto do ensaio, que hoje avisa disso
- **O registro do ensaio formal** é o 001 de histórico, reservado a ele; este documento não o
  substitui
