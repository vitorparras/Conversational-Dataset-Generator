# 0002 — A stack

> Decisão vigente. 2026-10-04.

Esta decisão escolhe a linguagem do núcleo, onde fica o processamento de linguagem natural, qual
banco de dados usar e como o sistema é distribuído. Serve a quem for escrever a primeira linha de
código. Não define a interface com o usuário nem o formato de comunicação entre as partes — isso
fica para decisões específicas.

## Contexto

O sistema tem quatro responsabilidades bem distintas: receber e tratar material heterogêneo,
processar texto em português com modelos de linguagem, guardar tudo de forma auditável, e oferecer
telas onde pessoas anotam. Cada uma puxa a escolha técnica para um lado, e a tentação é resolver
tudo com uma ferramenta só.

Duas restrições estreitam o campo antes de qualquer preferência.

A primeira é o português. O reconhecimento de nomes, endereços e documentos num texto — que alimenta
a pseudonimização — precisa acontecer junto com a análise gramatical do mesmo trecho, com as
posições de caractere alinhadas, porque é a função sintática da palavra que diz se ela é um nome de
pessoa ou um topônimo. Modelos que entregam as duas coisas num passo só, para português brasileiro,
existem no ecossistema Python. Fora dele, é preciso montar isso com duas peças separadas e conciliar
as posições na mão, o que introduz uma classe de erro cara: um deslocamento de índice anonimiza o
pedaço errado do texto.

A segunda é a cifra. O material que o sistema processa pode conter dados sensíveis, e eles precisam
estar cifrados quando guardados. Isso restringe quais bancos servem, e de que forma.

## Decisão

O núcleo é escrito em **C#**. Isso cobre a aplicação web, a persistência, a autenticação, a
orquestração do pipeline e a validação.

O processamento de linguagem natural fica em **Python**, como serviço separado, acionado pelo
núcleo. É a única parte que depende dos modelos de português, e ela é pequena e bem delimitada.

A persistência é **PostgreSQL**, usando colunas estruturadas onde a integridade importa e campos
JSON onde o domínio varia. Um motor só, não dois.

A distribuição é um **arquivo executável único**, que carrega o runtime consigo. Quem for rodar não
precisa instalar nada além dele.

## Por que C# no núcleo

Três capacidades são necessárias ao mesmo tempo, e poucas linguagens reúnem as três.

**Expressões regulares com verificação de contexto à esquerda, de largura variável.** O
reconhecimento de documentos brasileiros depende do texto que antecede o número: um número de onze
dígitos é CPF quando precedido de "CPF", "documento" ou "inscrição", e é outra coisa quando não é.
Esses prefixos variam em extensão, o que exige que a verificação de contexto aceite trechos de
tamanho variável. Sem isso, cada variação de prefixo vira um padrão separado, e a combinação entre
eles cresce a ponto de deixar de ser auditável — o que é grave num mecanismo cuja falha significa
dado pessoal gravado em claro.

**Distribuição sem dependência instalada no destino.** Quem precisar reproduzir o processamento
recebe um executável e o executa. Qualquer passo adicional de instalação é uma barreira entre o
trabalho e a sua verificação por terceiros.

**Ferramenta madura de teste de mutação.** Cobertura de testes mede quais linhas foram executadas,
não se o teste detectaria um erro nelas. Em código que decide o que é dado pessoal e o que entra
numa cadeia de custódia, a diferença entre as duas medidas é o que separa teste de teatro.

Entre as linguagens avaliadas, C# é a única que oferece as três sem concessão. Java atende a
primeira e a terceira — com a melhor ferramenta de mutação existente — mas exige máquina virtual
instalada no destino. Go e Rust distribuem bem, mas suas bibliotecas de expressões regulares
não suportam verificação de contexto à esquerda, por decisão de projeto: ambas garantem tempo de
execução linear, e essa garantia é incompatível com o recurso.

Somam-se a isso a tipagem estática com análise de nulo verificada na compilação, que serve a um
sistema que precisa falhar fechado; as primitivas de criptografia na biblioteca padrão, sem
dependência de terceiros para cifra e derivação de chave; e um driver PostgreSQL de primeira linha,
com suporte nativo aos campos JSON que a decisão de persistência exige.

## Por que o processamento de linguagem fica em Python

Pela restrição descrita no contexto: os modelos de português que fazem reconhecimento de entidades
junto com análise gramatical vivem lá. Mantê-lo em Python evita reconstruir essa capacidade a partir
de peças soltas.

A separação em dois processos tem um custo que precisa ser reconhecido: texto sensível atravessa uma
fronteira de processo, o que é superfície de vazamento que não existiria se tudo rodasse junto. A
decisão sobre o formato dessa comunicação precisa tratar disso explicitamente — o que trafega, o que
nunca pode aparecer em registro de log, e se o transporte é restrito à máquina local.

Em compensação, a fronteira é pequena: o núcleo manda texto e recebe de volta as posições e os tipos
do que foi encontrado. Isso mantém o Python contido na parte que realmente precisa dele.

## Por que PostgreSQL

A custódia exige esquema: colunas tipadas, integridade referencial, transações. Sem isso não há como
afirmar que um registro não foi adulterado, porque não existe o "esperado" contra o qual comparar o
inesperado.

A reutilização em outros domínios exige o contrário: campos que variam conforme o material. Um
domínio tem categorias que outro não tem, e metadados que só fazem sentido ali.

PostgreSQL atende às duas coisas no mesmo motor, com JSON em colunas específicas ao lado das colunas
estruturadas. Isso evita manter dois bancos, com duas estratégias de cifra, dois backups e nenhuma
transação entre eles.

## Alternativas recusadas

**Tudo em Python, inclusive o núcleo.** A opção mais simples de operar, com uma stack só, e
atende bem às expressões regulares. Recusada por duas razões: a verificação de tipos acontece em
tempo de execução, o que num sistema que precisa falhar fechado transfere para o teste aquilo que o
compilador poderia garantir; e a distribuição exige interpretador e dependências instalados no
destino, o que contraria a entrega por executável único.

**Go no núcleo.** Produz executável único pequeno, tem concorrência simples de raciocinar, e a forma
como declara interfaces favorece fronteiras explícitas. Recusada por duas razões concretas: a
biblioteca padrão de expressões regulares não suporta verificação de contexto à esquerda, recurso
necessário para reconhecer documentos de identidade pelo texto que os precede; e as ferramentas de
teste de mutação ainda são imaturas, o que enfraquece a garantia de qualidade dos testes.

**Java no núcleo.** Atende às três capacidades exigidas, e tem a melhor ferramenta de teste de
mutação disponível. Recusada por um ponto só, mas que não tem contorno barato: exige máquina virtual
instalada no destino. É a alternativa mais próxima, e a que deve ser reconsiderada primeiro se
alguma das premissas desta decisão cair.

**Rust no núcleo.** O melhor em garantias do compilador. Recusada porque sua biblioteca padrão de
expressões regulares também não suporta verificação de contexto à esquerda, e as alternativas
disponíveis abandonam a garantia de tempo de execução linear — propriedade que importa quando se
processa texto de origem não confiável.

**Banco documental.** Atraente pela flexibilidade de esquema. Recusada porque, na edição aberta, não
oferece cifra de dados em repouso nem mecanismo no servidor para impedir alteração de registro. As
duas coisas são justamente o que a custódia exige, e supri-las na aplicação transforma garantia em
promessa: basta esquecer de cifrar um campo para ele ser gravado em claro, sem que nada acuse.

**Dois bancos, um estruturado e outro documental.** Recusada por custo operacional: duas bases para
manter, cifrar e salvar em backup, sem transação entre elas, com a consistência virando
responsabilidade da aplicação.

## Consequências

A cifra dos dados em repouso precisa de decisão própria, e ela bloqueia o início do código que
persiste material sensível. PostgreSQL oferece caminhos diferentes — cifra de coluna, cifra de
volume, cifra feita pela aplicação — e eles diferem no que acontece quando alguém copia o arquivo do
banco para fora. Enquanto isso não estiver decidido, não se escreve persistência de dado sensível.

A garantia de qualidade dos testes precisa ser decidida por escrito, não presumida. As ferramentas
disponíveis variam conforme a linguagem, e a composição que de fato sustenta a qualidade — revisão
por pull request, teste baseado em propriedades sobre os invariantes de custódia, cobertura mínima
por área, teste de mutação onde houver ferramenta madura — precisa estar definida antes do primeiro
código.

Manter duas linguagens tem custo permanente: duas suítes de teste, duas cadeias de dependências e
duas formas de empacotar. O contrapeso é a fronteira entre elas ser estreita e estável.

## O que reabre esta decisão

Se não houver, em PostgreSQL, forma aceitável de manter a cifra junto ao dado — de modo que copiar o
banco para fora não exponha o conteúdo —, a escolha de banco precisa ser reaberta. Cifra é
requisito, não preferência.

Se a entrega por executável único deixar de ser requisito — por exemplo, se o sistema passar a
rodar apenas em ambiente controlado pela própria equipe —, Java volta a ser candidato, com vantagem
em teste de mutação.

Se o serviço de processamento de linguagem passar a ser usado por outro sistema, a fronteira de
processo deixa de ser custo e vira benefício, o que reforça esta decisão em vez de enfraquecê-la.
