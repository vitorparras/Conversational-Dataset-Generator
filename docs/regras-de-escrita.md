# Regras de escrita

> Operacional. Vale para todo documento deste repositório.

**O que é:** o formato obrigatório dos documentos.
**A quem serve:** quem escreve ou revisa documentação, pessoa ou agente.
**O que não cobre:** o que cada pasta contém — isso está no README de cada uma.

A verificação automática (`ferramentas/verifica-docs/verificar.py`) impõe boa parte destas regras e
reprova o pull request que as viole.

## 1. Todo documento declara sua autoridade

Logo após o título, uma linha de citação diz se o documento vincula. **Sem ela a verificação
reprova** — é o único lugar onde a autoridade fica explícita, já que não usamos cabeçalho de
metadados.

```markdown
# 0002 — A stack

> Decisão vigente. 2026-10-04.
```

Vocabulário fechado:

| Linha | Significado |
|---|---|
| `Norma vigente` | Vincula. Contrariar exige substituir |
| `Norma substituída por X` | Não vale mais; X diz o que vale |
| `Norma revogada` | Não vale mais, e nada substitui |
| `Decisão vigente` | Vincula. Usado em `decisoes/` |
| `Decisão substituída por X` | Não vale mais; X diz o que vale |
| `Decisão revogada` | Não vale mais, e nada substitui |
| `Descrição` | Descreve o sistema. **Se divergir do código, o código ganha** |
| `Histórico` | Autoridade zero. Nunca é regra, mesmo que pareça |
| `Operacional` | Procedimento a seguir |

## 2. Cada fato vive em um lugar só

Os demais documentos **referenciam**; nunca copiam. Duas cópias do mesmo fato divergem com o tempo,
e a divergência só aparece quando alguém age sobre a versão errada.

## 3. Abertura de três linhas

Todo documento começa dizendo o que é, a quem serve e **o que não cobre**. Quem chega por busca
precisa saber em uma tela se está no lugar certo.

## 4. Nada de dado pessoal, segredo ou caminho de máquina

Nem em exemplo, nem em teste, nem em documentação. Exemplos usam identificadores sintéticos
(`PESSOA_1`, `REMETENTE_A`), **nunca nomes plausíveis** — um nome inventado pode coincidir com
pessoa real, e num dataset forense a coincidência tem consequência.

A verificação procura CPF, CNPJ, RG, telefone, e-mail, caminho absoluto, token e chave. **É a regra
sem margem de discussão.**

## 5. Só cite o que existe

Arquivo, caminho, classe ou identificador citado em `código inline` precisa existir. A verificação
confere. Critério de aceitação que cita um campo inexistente não é implementável, e o defeito só
aparece quando alguém tenta cumpri-lo.

## 6. Formatação

- Até **100 colunas** por linha. Tabela, link e bloco de código são isentos
- Espaços, nunca tab
- Sem espaço no fim da linha
- No máximo duas linhas vazias seguidas
- Arquivo termina com quebra de linha

## 7. Nomes de arquivo

`NNN-slug-descritivo.md`, numeração sequencial **nunca reutilizada**, slug em minúsculas com
hífens, sem acento. Um documento substituído mantém o número e muda a linha de autoridade.

## 8. Idioma

Português brasileiro, com acentuação correta. Identificadores de código em inglês.

## 9. Promessa de privacidade cita o teste

Todo requisito que promete proteção de dado pessoal cita o teste que a prova. Promessa sem teste
citado reprova na revisão — é o que separa garantia de intenção.
