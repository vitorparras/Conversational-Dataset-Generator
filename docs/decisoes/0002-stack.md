# 0002 — A stack

> Decisão substituída por 0041 quanto à distribuição do serviço. 2026-10-04.

| Camada | Escolha |
|---|---|
| Núcleo: web, persistência, autenticação, pipeline | C# |
| Processamento de linguagem natural | Python, serviço separado |
| Banco | PostgreSQL |
| Distribuição | Executável único, sem instalar nada no destino |

## Por que C#

O sistema cifra dado pessoal, reverte pseudonimização sob autorização, registra acesso e nega por
papel. Em .NET as quatro coisas são serviço de plataforma, não código próprio:

- **Chaves** — derivação por finalidade a partir de uma mestra, com rotação e revogação, mantendo a
  capacidade de decifrar o que foi cifrado antes. É a estrutura que a reversão exige.
- **Log sem vazar dado pessoal** — a plataforma redige campos sensíveis no próprio pipeline de log,
  por anotação no tipo. Vira mecanismo, não disciplina de quem escreve a mensagem.
- **Acesso por política** — autorização declarativa avaliada antes do código da rota.
- **Criptografia na biblioteca padrão** — AES-GCM e derivação de chave sem terceiros.

## Por que PostgreSQL

As mesmas garantias, impostas pelo servidor em vez da aplicação:

- **Segurança em nível de linha**, com `FORCE`, vale até contra o dono da tabela. Se a aplicação
  falhar, o banco ainda recusa.
- **Auditoria por extensão**, no servidor: a aplicação não omite o que ela própria faz.
- **Append-only real** — concessão por coluna e gatilhos, sem depender de a aplicação lembrar.
- **Transação serializável**, para encadear registros por hash sem corromper a cadeia em silêncio.
- **Cifra de coluna** por extensão, e campos JSON com restrição onde o domínio varia.

## Por que Python no processamento de linguagem

Os modelos que reconhecem entidades junto com a análise gramatical do mesmo trecho, para português,
estão nesse ecossistema. Fora dele é montar com duas peças e conciliar posições na mão.

## Recusados

| Opção | Por quê |
|---|---|
| Python no núcleo | Tipos só em execução; exige interpretador no destino |
| Java no núcleo | Equivalente em segurança; exige máquina virtual no destino. O mais próximo |
| Go no núcleo | Proteção de chaves e redação ficam por conta da aplicação |
| Rust no núcleo | Ecossistema web e de auditoria menos maduro |
| Banco documental | Na edição aberta: sem cifra em repouso nem imutabilidade no servidor |
| Dois bancos | Duas bases para cifrar e salvar, sem transação entre elas |

## Consequências

A cifra em repouso precisa de decisão própria, e **bloqueia o código que persiste dado sensível**.
Texto sensível atravessa fronteira de processo até o serviço de linguagem, e a decisão sobre esse
formato diz o que trafega e o que nunca entra em log. Duas linguagens custam duas suítes de teste.

## O que reabre

Se não houver, em PostgreSQL, forma de manter a cifra junto ao dado, de modo que copiar o banco para
fora não exponha nada. Ou se o executável único deixar de ser requisito — nesse caso Java volta.
