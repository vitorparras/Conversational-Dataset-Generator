# Como contribuir

Leia [`AGENTS.md`](AGENTS.md) primeiro — ele define a hierarquia de autoridade entre os documentos
e as regras que valem para qualquer contribuição, de pessoa ou de agente.

## Estado atual

Documentação e código vivem aqui. Texto segue [regras de escrita](docs/regras-de-escrita.md);
código segue [regras de código](docs/regras-de-codigo.md), inclusive o teto de pull request.

## O fluxo

Toda mudança passa por pull request com aprovação do dono do repositório, e a verificação
automática precisa passar. Sem exceção, inclusive para quem mantém.

Para qualquer coisa que não seja correção trivial, **abra a issue e espere alinhamento antes de
escrever**. Trabalho descartado desmotiva mais que espera.

## Commits e branches

Branch `<tipo>/<slug>`, com o mesmo tipo do commit (`feat`, `fix`, `docs`, `chore`, `ci`, `test`,
`build`, `refactor`); slug curto, minúsculas, hífens, sem acento; issue única vai na frente, como em
feat/37-comando-unico. [Conventional Commits](https://www.conventionalcommits.org) no commit:
`tipo(escopo): descrição`, imperativo, minúscula, sem ponto final, até 72 caracteres. Um assunto
por commit; corpo só quando acrescenta o que o título não diz. **Nenhuma linha de atribuição a
ferramenta ou agente de IA** — quem assina responde; a declaração de uso vai no gabarito de PR.
Um pull request resolve uma task, dentro do teto das regras de código; o merge é do dono.

## Se você discorda de uma decisão

Cada decisão em `docs/decisoes/` tem uma seção dizendo o que a reabre. Discordar é bem-vindo: abra
issue apontando a decisão e diga qual condição de revisão você considera realizada, ou qual
argumento não foi considerado.

**Não contorne decisão vigente em silêncio.** Mude a decisão — escrevendo outra que a substitua — ou
siga-a.

## O que reprova uma contribuição

A lista está em `AGENTS.md`. A que não admite discussão: **dado pessoal, segredo ou caminho absoluto
de máquina em qualquer arquivo**, inclusive exemplo e teste.

Exemplos usam identificadores sintéticos. Nunca nomes plausíveis de pessoa, mesmo inventados — um
nome inventado pode coincidir com pessoa real, e neste domínio a coincidência tem consequência.

## Contribuição de agentes de IA

São bem-vindos, e o repositório é desenhado para eles. Duas exigências: o pull request **declara**
que foi gerado por agente e quem revisou; e nenhuma afirmação entra sem o arquivo ter sido aberto na
mesma execução.

## Licença

O código é AGPL-3.0: contribuições entram sob a mesma licença. Material e datasets não acompanham o
repositório e têm regime próprio, descrito em `docs/privacidade/`.

## Segurança

Vulnerabilidade não vai para issue pública. Veja [`SECURITY.md`](SECURITY.md).
