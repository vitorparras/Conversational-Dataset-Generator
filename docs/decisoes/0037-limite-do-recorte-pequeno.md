# 0037 — Onde se declara o limite do recorte pequeno

> Decisão vigente. 2026-10-07. Completa o requisito 029.

## Contexto

O [requisito 029](../requisitos/029-metrica-sem-texto.md) exige que uma contagem pequena o bastante
para apontar um item específico seja recusada, em vez de devolvida. Ele não diz onde esse limite é
declarado.

Sem a declaração, cada consulta aplicaria o seu — ou nenhum.

## Decisão

**O limite fica na configuração do domínio**, ao lado dos pisos de suficiência e do tamanho da
amostra de revisão, e carrega pelo mesmo mecanismo: chave desconhecida ou ausente impede a
inicialização ([requisito 032](../requisitos/032-configuracao-falha-fechada.md)).

Não há valor padrão. Domínio sem o limite declarado não inicia.

## Recusados

**Norma fixa na política de acesso.** Um limite só para todo domínio, que ninguém pode afrouxar por
conveniência — o mesmo argumento do tratamento uniforme das categorias. E domínios têm acervos de
tamanhos muito diferentes: o limite que protege um acervo pequeno deixaria um grande recusando menos
do que deveria, e o que serve ao grande silenciaria toda métrica do pequeno.

**Derivar do tamanho do acervo.** Escalaria sozinho e dispensaria justificar um número. E o limite
mudaria quando o acervo crescesse: a mesma consulta passaria a ser respondida onde antes era
recusada, sem que nada na política mudasse. Para quem audita, isso é indistinguível de afrouxamento.

## Consequências

Duas instalações do mesmo domínio podem divergir se a configuração não for versionada junto com a
taxonomia — e é por isso que ela é versionada junto.

Declarar o limite é decisão de quem define o domínio, com o acervo à vista, e fica registrada na
configuração em vigor, que é consultável
([requisito 032](../requisitos/032-configuracao-falha-fechada.md)).
