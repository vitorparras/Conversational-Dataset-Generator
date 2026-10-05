# 0011 — O protocolo com o serviço de linguagem

> Decisão vigente. 2026-10-04.

## Contexto

A decisão 0002 põe o processamento de linguagem natural em Python, como serviço separado, e deixa
pendente o que essa fronteira carrega: texto anterior à sanitização, que é o único dado pessoal a
sair do processo que detém a chave da 0003, e as posições dentro dele.

## Decisão

O serviço recebe texto e **devolve apontamentos, nunca texto transformado**. Quem substitui,
pseudonimiza e guarda o mapa de reversão é o núcleo, que é o lado com a chave. Vão o texto, o
idioma, um identificador opaco de requisição e a versão do contrato; voltam os trechos — posição
inicial, final, tipo e confiança — e a versão do modelo. Nada identifica material, domínio, pessoa
ou anotador.

**As posições são medidas em pontos de código, e converte o lado que indexa diferente: o núcleo.**
Python indexa por ponto de código e o núcleo por unidade UTF-16; as contagens divergem em emoji,
caractere fora do plano básico e sequência combinada — o que texto conversacional tem de sobra. A
divergência não dá erro: desloca a posição, e o sistema pseudonimiza o trecho vizinho, deixando o
dado pessoal no texto e apagando o que estava ao lado.

**A resposta distingue "nada encontrado" de "serviço indisponível".** Lista vazia significa
analisado, e nada achado; indisponível é outro desfecho, e o núcleo recusa o material em vez de
seguir. Sem a distinção os dois chegam iguais, serviço fora do ar produz texto tratado como se não
tivesse nada a pseudonimizar, e o pipeline que devia falhar fechado falha aberto sem acusar nada.

O formato é JSON — inspecionável, sem geração de código no build — sobre transporte **restrito à
máquina local**: o serviço escuta só na interface local, com segredo gerado na inicialização.

**Nunca entram em registro de log** o texto, qualquer trecho dele, e as posições: posição com tipo,
somada ao texto que o núcleo tem, reconstrói o dado pessoal. O log recebe o identificador, o tempo,
a versão do modelo e a contagem de trechos por tipo; em falha, a exceção sem o trecho que a causou.

## Recusados

| Opção | Por quê |
|---|---|
| O serviço devolve o texto já pseudonimizado | Põe a substituição e o mapa de reversão do lado que não tem a chave |
| Posição sem unidade declarada, cada lado assumindo a sua | É a falha que não dá erro: anonimiza o trecho vizinho e deixa o dado pessoal no texto |
| Lista vazia servindo para os dois desfechos | Serviço fora do ar passa a aprovar material sem análise, e o pipeline falha aberto |
| Transporte em interface de rede | Expõe texto anterior à sanitização a quem alcance a rede, e autenticar não reduz esse alcance |

## Consequências

A distribuição do serviço fica em aberto, e a decisão 0002 não a cobre: ela recusa Python e Java no
núcleo por exigirem interpretador ou máquina virtual no destino, e o serviço que ela mantém exige
interpretador e modelo no destino. O tamanho do modelo precisa ser medido antes de a distribuição
ser fechada: é ele que decide se o executável único vale para o conjunto ou só para o núcleo.

O núcleo passa a supervisionar um processo que não é dele, e a indisponibilidade dele interrompe a
ingestão por decisão, não por acidente. Sem texto em log, reproduzir reconhecimento errado exige
reexecutar sobre o material original, e decifrá-lo é operação auditada.

## O que reabre

Se o reconhecimento passar a rodar embarcado no núcleo, a fronteira desaparece. Se o volume exigir
instâncias fora da máquina, entra transporte autenticado e cifrado, por decisão que substitua esta.
