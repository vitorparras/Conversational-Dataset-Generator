# 0014 — Custódia e recuperação da chave mestra

> Decisão vigente. 2026-10-04.

## Contexto

A decisão 0003 cifra pela aplicação e conclui que perder a chave é perder o texto bruto e a reversão
em definitivo — e que o procedimento de guarda e recuperação precisa estar escrito antes de haver
dado real. Este é o documento que ela exige. Sem ele, a cifra protege contra vazamento e cria, no
mesmo movimento, um modo de perda total que ninguém endereçou.

## Decisão

A chave mestra é **fornecida ao processo na inicialização, de fora do artefato**. Não está no
repositório, na imagem, no banco, nem em arquivo ao lado do banco.

Antes do primeiro dado real existem duas coisas, e sem elas a ingestão não começa:

1. Uma **cópia de guarda** da chave e dos rótulos de derivação, fora da máquina que roda o sistema,
   em meio cifrado e com custódia declarada: quem detém, onde, e quem pode pedir.
2. Uma **recuperação testada** — restaurar banco e chave num ambiente limpo e conferir que o texto
   bruto decifra e o mapa de reversão abre. Recuperação nunca exercitada não é recuperação; é
   expectativa.

Perda da chave é perda de acervo, e é registrada como tal. Não se finge que o material existe porque
as linhas continuam no banco.

## Recusados

| Opção | Por quê |
|---|---|
| Chave em arquivo de configuração junto do banco | Quem copia o banco copia a chave, e a decisão 0003 perde todo o efeito |
| Chave digitada por pessoa a cada inicialização | O sistema deixa de subir sozinho; reinício fora de hora vira indisponibilidade, e a chave acaba anotada em lugar pior |
| Chave em serviço de nuvem | Acrescenta dependência de rede a um sistema que precisa rodar em instalação local |
| Sem cópia de guarda, confiando no backup do banco | Backup sem a chave é um arquivo de bytes: é o cenário que a 0003 protege, visto do outro lado |
| Chave partida entre pessoas, com quórum para montar | Protege contra custódia desonesta e cria indisponibilidade a cada ausência; não é o risco deste acervo |

## Consequências

Passa a haver um segredo administrado fora do sistema, e o sistema não tem como verificar se a
custódia está sendo cumprida. A garantia depende de procedimento humano, e procedimento humano só
vale exercitado — por isso o teste de recuperação, que é trabalho recorrente e sem resultado
visível quando dá certo.

A cópia de guarda é, ela mesma, um alvo. Duas cópias da chave são dois lugares de onde ela pode
sair, e a decisão aceita isso: perda permanente do acervo é pior que o acréscimo de superfície.

Ingerir dado real passa a ter pré-requisito operacional. Instalação de demonstração roda sem nada
disso, e a diferença entre as duas precisa ser explícita — senão alguém ingere material de verdade
numa instalação que nunca teve cópia de guarda.

## O que reabre

Se o sistema passar a rodar em infraestrutura com cofre de chaves próprio, condição que a decisão
0003 já prevê, a guarda passa a ser daquele serviço e o teste de recuperação muda de objeto.
