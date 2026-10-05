# 0013 — Chave de cifra por domínio

> Decisão vigente. 2026-10-04.

## Contexto

A decisão 0006 põe vários domínios na mesma instalação e observa que, se eles tiverem sensibilidade
diferente, chave de cifra por domínio passa a fazer sentido — o que a decisão 0003 não trata. Esta é
a decisão que ela adia, e precisa existir antes do segundo domínio real: cifrar depois obriga a
decifrar e recifrar o acervo, com ele em claro no meio da operação.

## Decisão

**Uma chave mestra. As chaves de domínio são derivadas dela, com o domínio no rótulo de derivação,
ao lado da finalidade.** Não há chave mestra por domínio.

Cada dado cifrado carrega, em claro, o rótulo com que foi cifrado. Rotação acrescenta rótulo novo
para o que passa a ser cifrado, e o antigo continua derivável, porque descartá-lo é apagar dado.

A derivação por domínio dá o que importa: material de um domínio não se decifra com a chave de
outro, e rotacionar ou revogar um domínio não toca nos demais. O que ela não dá é independência de
custódia, e aí está o motivo de não haver uma mestra por domínio. A decisão 0003 concentrou o
segredo num lugar porque é mais fácil proteger bem um do que razoavelmente dez; uma mestra por
domínio desfaz exatamente essa escolha — multiplica cópias de guarda, procedimentos de recuperação e
oportunidades de perda — para cobrir o cenário de comprometer um domínio sem comprometer a máquina,
que é o menos provável dos que a 0003 enumera.

## Recusados

| Opção | Por quê |
|---|---|
| Chave mestra independente por domínio | Multiplica a custódia, que é o elo frágil, para cobrir o cenário menos provável |
| Uma única chave para todos os domínios | Texto cifrado vazado de um domínio é decifrável com a chave que serve a todos, e rotação atinge o acervo inteiro |
| Chave de domínio guardada por quem opera o domínio | Quem opera um domínio passa a poder negar acesso ao acervo, e a instalação deixa de subir sem reunir pessoas |
| Decidir só quando existir o segundo domínio | Cifrar depois obriga a decifrar e recifrar tudo, expondo o acervo durante a operação |

## Consequências

O comprometimento da chave mestra expõe todos os domínios ao mesmo tempo. A separação é lógica, não
física: protege contra vazamento de material cifrado, não contra quem alcance o segredo onde ele
vive.

O conjunto de rótulos de derivação só cresce, porque nenhum é descartado. Saber quais existiram e
para que serviram passa a fazer parte do que precisa ser preservado junto com a chave — rótulo
perdido é dado ilegível com a chave em mãos.

Acrescentar domínio deixa de ser operação de cadastro: cria rótulo, e o rótulo entra na custódia.

## O que reabre

Se um domínio exigir que sua chave seja detida por outra parte, por exigência legal ou contratual,
ele deixa de caber nesta instalação. É a mesma condição que faz a decisão 0006 voltar à separação
por ambiente, e as duas se reabrem juntas.
