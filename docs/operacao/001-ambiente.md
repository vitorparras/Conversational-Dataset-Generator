# 001 — O ambiente que o sistema exige

> Descrição. 2026-10-04.

O núcleo é distribuído como executável único e o serviço de linguagem numa pasta dentro da do
executável, pela [decisão 0041](../decisoes/0041-distribuicao-do-servico-de-linguagem.md); nada se
instala no destino. Fora deles, o ambiente oferece a máquina, o banco, a chave e o
[perímetro](011-servico-de-linguagem.md) do serviço. Aqui, o que precisa existir.

## O banco

Um servidor PostgreSQL onde as garantias sejam do servidor, e não da aplicação ([decisão 0002](../decisoes/0002-stack.md)):

- Política de linha aplicável com `FORCE`, para que o isolamento entre domínios valha até contra o
  dono da tabela, como a [decisão 0006](../decisoes/0006-dominios-coexistem.md) exige
- Registro de auditoria por extensão, no servidor, fora do alcance da aplicação, uma linha por
  tabela tocada (`pgaudit.log_relation`), em `csvlog` em UTC fora do diretório de dados (`0640`;
  a aplicação só lê, pelo grupo `postgres`) e o texto do comando que altera (`log_statement = mod`),
  sem valor; retenção da cadeia, sem apagar
- Isolamento serializável, sem o qual duas gravações simultâneas corrompem a cadeia em silêncio
- Permissão de escrita revogável por coluna, e gatilhos, para que a tabela acrescentável não dependa
  de a aplicação lembrar
- Volume em sistema de arquivos cifrado — segunda camada, nunca a única

O papel que a aplicação usa não é dono de tabela nem superusuário: com esse privilégio o gatilho de
imutabilidade se desabilita, e a [cadeia de custódia](../privacidade/004-cadeia-de-custodia.md)
aponta isso como seu elo mais fraco. A credencial de dono tem guarda própria, em [010](010-credencial-de-dono-do-banco.md).

## A chave

Ela é fornecida ao processo na inicialização, **de fora do artefato**, e o ambiente precisa ter de
onde fornecê-la: um meio que derive por rótulo — domínio ao lado da finalidade — e mantenha todos os
rótulos já emitidos, porque nenhum se descarta. O servidor do banco não alcança esse meio: se
alcançasse, o critério da [decisão 0003](../decisoes/0003-cifra-em-repouso.md) cairia.

A chave não está no repositório, na imagem, em arquivo ao lado do banco, em argumento de comando
visível a outro processo, nem em log. A guarda está em [002](002-guarda-da-chave.md) e [024](024-arquivos-de-custodia-da-chave.md).

## A máquina

O núcleo carrega do sistema, no Linux, a glibc, a libgcc_s, a libstdc++ e a OpenSSL 3, e não sobe
sem elas; no Windows, só o próprio sistema. A criptografia da biblioteca padrão da
[decisão 0002](../decisoes/0002-stack.md) é a da plataforma: OpenSSL 3 no Linux, CNG no Windows. A
lista vem do [histórico 002](../historico/002-medidas-do-artefato.md), que não mediu o `win-x64` nem a pasta do serviço. A `debian:12-slim` não traz a `libssl3`. A `libgssapi_krb5.so.2` é opcional: sem ela sai o aviso de [023](023-iniciar-o-nucleo-publicado.md), e a conexão segue.

## O resto

- **Contas individuais.** O registro nomeia quem agiu; conta compartilhada o torna inútil
- **Relógio confiável.** A ordem vem da cadeia; horário anterior ao do antecessor é achado
- **Taxonomia de cada domínio** versionada e válida onde a instalação a lê: sem ela, nada sobe
- **Certificado e chave do canal cifrado**, em PEM e só do dono, fora da pasta do executável
  ([0046](../decisoes/0046-material-do-canal-cifrado.md)). Conferir o núcleo pede um cliente TLS
  (`openssl s_client` ou `curl`): proposta ao dono, que ainda confirma
- **Lugar para backup** separado de onde vive a cópia de guarda da chave — ver [005](005-backup.md)

## O que não pode existir

Material real antes de existirem cópia de guarda da chave e recuperação testada: a
[decisão 0014](../decisoes/0014-custodia-da-chave-mestra.md) faz disso pré-requisito da ingestão, e
não boa prática. Instalação de demonstração roda sem nada disso, e precisa ser distinguível de uma
instalação real a olho nu.
