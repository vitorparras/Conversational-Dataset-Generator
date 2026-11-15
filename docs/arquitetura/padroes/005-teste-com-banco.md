# Teste com banco

> Norma vigente. 2026-11-15.

Teste que precisa de um PostgreSQL real. Marcado `[Trait("Category", "Database")]` e com
`[DatabaseFact]`, que o pula quando não há banco no ambiente.

## Exemplar

`core/tests/Core.Persistence.Tests/People/PersonChangeScopeTests.cs`.

- O banco vem de uma fixture de classe (`IClassFixture<PeopleDatabase>`), migrada uma vez,
  e não de um banco montado dentro do teste (linhas 9 a 19).
- Cada teste cria os próprios dados com nome sintético (`PeopleDatabase.NewName`) por um auxiliar de
  uma linha (linhas 46 e 47), então os testes da classe não dependem de ordem nem uns dos outros.
- Arrange, Act e Assert separados por linha em branco; o Act é uma chamada; o nome do teste diz o
  comportamento inteiro (linhas 21 a 32).
- A verificação lê pela fixture (`PersonAsync`, `ChangesOfAsync`), não por SQL escrito no teste.
- O token de cancelamento do contexto do teste (TestContext) em toda chamada assíncrona.

## Princípio

O teste observa comportamento pela porta pública e o cenário é dado, não remontado: uma fixture por
banco de módulo concentra migração, criação de dado e leitura de conferência, e o teste fica com a
regra que verifica ([regras de código](../../regras-de-codigo.md), 4 e 7).

## Proibido

- Migrar o banco dentro do teste ou montar o próprio banco descartável; hoje 60 arquivos de teste
  citam a migração diretamente.
- `INSERT` cru no teste para preparar estado que a peça de produção sabe criar.
- Copiar o auxiliar de criação de pessoa para mais uma classe: se a fixture do módulo não o tem,
  ele entra na fixture.
- Nome de pessoa plausível, mesmo inventado ([regras de escrita](../../regras-de-escrita.md), 4).
- Asserção que depende da ordem de execução ou de dado criado por outro teste.
