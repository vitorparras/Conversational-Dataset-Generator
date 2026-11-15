# Teste com banco

> Norma vigente. 2026-11-15.

Teste que precisa de um PostgreSQL real. Marcado `[Trait("Category", "Database")]` e com
`[DatabaseFact]`, que o pula quando não há banco no ambiente.

## Exemplar

`core/tests/Core.Persistence.Tests/Notices/PersonNoticesTests.cs`, com o auxiliar do módulo em
`core/tests/Core.Persistence.Tests/Notices/NoticesDatabase.cs`.

- O banco vem de uma fixture de classe (`IClassFixture<PeopleDatabase>`), migrada uma vez,
  e não de um banco montado dentro do teste (linhas 13 a 20).
- O cenário vem de um auxiliar compartilhado do módulo (`NoticesDatabase`), não de um método
  privado da classe: cada teste cria a própria pessoa com nome sintético (`PeopleDatabase.NewName`)
  e não depende de ordem nem de outro teste.
- Arrange, Act e Assert separados por linha em branco; o Act é uma chamada; o nome do teste diz o
  comportamento inteiro (linhas 22 a 31).
- A verificação lê pela peça de produção (`UnreadAsync`, linha 29), não por SQL escrito no teste.
- O token de cancelamento do contexto do teste (TestContext) em toda chamada à peça de produção,
  inclusive a do auxiliar (`NoticesDatabase`, linha 19).

## Princípio

O teste observa comportamento pela porta pública ([regras de código](../../regras-de-codigo.md),
4), e o cenário repetido mora num lugar só a partir da terceira cópia (regra 1): o auxiliar do
módulo concentra criação de dado e leitura de conferência, e o teste fica com a regra que
verifica, em Arrange, Act e Assert (regra 7).

## Proibido

- Migrar o banco dentro do teste ou montar o próprio banco descartável; hoje 60 arquivos de teste
  citam a migração diretamente.
- `INSERT` cru no teste para preparar estado que a peça de produção sabe criar. Leitura crua como
  dono do banco só no auxiliar do módulo (`TextAsync`) e só para o que nenhuma leitura de produção
  mostra.
- Copiar o auxiliar de criação de pessoa para mais uma classe. Hoje há 16 cópias privadas de
  `NewPersonAsync` em `core/tests/Core.Persistence.Tests`, como a de `PersonChangeScopeTests`
  (linhas 46 e 47), que não passa o token; não a copie, ponha o auxiliar no do módulo.
- Nome de pessoa plausível, mesmo inventado ([regras de escrita](../../regras-de-escrita.md), 4).
- Asserção que depende da ordem de execução ou de dado criado por outro teste.
