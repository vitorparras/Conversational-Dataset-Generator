# 001 — O ambiente que o sistema exige

> Descrição. 2026-10-04.

O sistema é distribuído como executável único e não instala nada no destino, então quase tudo o que
o ambiente precisa oferecer está fora dele: um banco que imponha as garantias, um cofre de onde a
chave venha, e um perímetro que contenha o serviço de linguagem. Aqui está o que precisa existir, e
não como se instala — ferramenta muda, exigência não.

## O banco

Um servidor PostgreSQL onde as garantias sejam do servidor, e não da aplicação, conforme a
[decisão 0002](../decisoes/0002-stack.md):

- Política de linha aplicável com `FORCE`, para que o isolamento entre domínios valha até contra o
  dono da tabela, como a [decisão 0006](../decisoes/0006-dominios-coexistem.md) exige
- Registro de auditoria por extensão, no servidor, fora do alcance da aplicação que ele audita
- Isolamento serializável, sem o qual duas gravações simultâneas corrompem a cadeia em silêncio
- Permissão de escrita revogável por coluna, e gatilhos, para que a tabela acrescentável não dependa
  de a aplicação lembrar
- Volume em sistema de arquivos cifrado — segunda camada, nunca a única

O papel que a aplicação usa não é dono de tabela nem superusuário: com esse privilégio o gatilho de
imutabilidade se desabilita, e a [cadeia de custódia](../privacidade/004-cadeia-de-custodia.md)
aponta isso como seu elo mais fraco. A credencial de dono tem guarda própria, em
[010](010-credencial-de-dono-do-banco.md).

## A chave

Ela é fornecida ao processo na inicialização, **de fora do artefato**, e o ambiente precisa ter de
onde fornecê-la: um meio que derive por rótulo — domínio ao lado da finalidade — e mantenha todos os
rótulos já emitidos, porque nenhum se descarta. O servidor do banco não alcança esse meio: se
alcançasse, o critério da [decisão 0003](../decisoes/0003-cifra-em-repouso.md) cairia.

A chave não está no repositório, na imagem, em arquivo ao lado do banco, em argumento de comando
visível a outro processo, nem em log. A guarda está em [002](002-guarda-da-chave.md).

## O serviço de linguagem

Texto ainda não sanitizado atravessa a fronteira de processo até ele, e o transporte é **restrito à
máquina local** por decisão. O que o ambiente responde por, e o único ponto que segue aberto, está
em [011](011-servico-de-linguagem.md).

## O resto

- **Contas individuais.** O acesso é por domínio e o registro nomeia quem agiu; conta compartilhada
  torna o registro inútil no dia em que ele importa
- **Relógio confiável.** A ordem vem da cadeia, não do horário; mas evento com horário anterior ao
  do antecessor é achado, não curiosidade
- **Taxonomia de cada domínio** no lugar de onde a instalação a lê, versionada e válida: o sistema
  não sobe com taxonomia inválida
- **Lugar para backup** separado de onde vive a cópia de guarda da chave — ver [005](005-backup.md)

## O que não pode existir

Material real antes de existirem cópia de guarda da chave e recuperação testada: a
[decisão 0014](../decisoes/0014-custodia-da-chave-mestra.md) faz disso pré-requisito da ingestão, e
não boa prática. Instalação de demonstração roda sem nada disso, e precisa ser distinguível de uma
instalação real a olho nu.
