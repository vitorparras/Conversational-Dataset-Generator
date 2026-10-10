# 0009 — Licença do software e licença dos dados

> Decisão vigente. 2026-10-07. Substitui a pendência registrada em 2026-10-04.

## Contexto

Sem licença, ninguém tem permissão de usar, copiar ou derivar o trabalho. A decisão 0001 estabelece
duas contribuições, e elas não se licenciam com o mesmo instrumento: uma governa código, a outra um
conjunto de dados que, pela decisão 0012, é dado pessoal pseudonimizado.

Esta decisão esteve pendente por faltar saber quem detém a titularidade e sob que condições o
material circula. O código é do projeto e circula livremente; o material não circula, e o acesso a
ele é contratual.

## Decisão

**O software é licenciado sob AGPL-3.0.** Qualquer pessoa ou organização pode usar, estudar,
modificar e redistribuir, inclusive com finalidade comercial. Quem modificar e redistribuir — ou
**oferecer o sistema como serviço pela rede** — precisa publicar as alterações sob a mesma licença.

**O material e os datasets dele derivados não acompanham o software**, e não se licenciam: o acesso
é concedido caso a caso, por contrato. O caminho é solicitação formal por escrito, seguida de
contrato assinado que restringe o uso a finalidade acadêmica ou de pesquisa, sem fim comercial, e
proíbe repasse a terceiros.

As duas são independentes: receber o código não dá acesso ao material, e o contrário também não.

## Recusados

**Licença permissiva para o software**, como MIT ou Apache. Permitiria que uma versão melhorada da
ferramenta fosse fechada, e a ferramenta é uma das duas contribuições do projeto — não um meio.

**GPL-3.0 sem a cláusula de rede.** O sistema é uma aplicação web: quem o hospedasse como serviço
nunca distribuiria o programa, só o acesso a ele, e ficaria livre de devolver as melhorias. A
brecha é exatamente o modo de uso mais provável.

**Licença de software que proíbe uso comercial.** Não é reconhecida como software livre, afasta
contribuições, e confunde a restrição do material com a do código — ela pertence ao contrato.

**Licença aberta para os dados**, mesmo com atribuição. Dado pessoal pseudonimizado não vira
público por decisão de quem o detém, e a reversão continua tecnicamente possível.

## Consequências

Uma organização pode construir o próprio acervo com esta ferramenta, inclusive comercialmente, sem
pedir nada a ninguém — e precisa publicar o que melhorar. Algumas evitam AGPL por política interna,
o que reduz adoção: fechar a brecha do serviço vale mais que a adoção perdida.

O contrato do material precisa existir antes do primeiro acesso externo, e exige redação jurídica.

**Publicar o repositório deixa de estar bloqueado pela licença do código.** Continua condicionado a
não haver material real versionado, que é garantia de outra ordem.

## O que reabre

Se a titularidade do código deixar de ser do projeto — por vínculo institucional ou financiamento
que imponha outro regime —, a escolha da licença volta a quem detiver os direitos.

Se o sistema deixar de ser servido pela rede, a cláusula que distingue AGPL de GPL perde objeto, e a
licença pode ser reavaliada pelo critério mais simples.
