# 031 — A entrada de arquivo é contida

> Norma vigente. 2026-10-07.

**Arquivo submetido é validado antes de ser gravado, e nada do que vem com ele é tratado como
instrução.**

## Por que existe

Receber arquivo é a única porta por onde entra conteúdo que o sistema não produziu, e quem submete
não é necessariamente quem o construiu.

Falha aqui não corrompe apenas o acervo. O processo que recebe o arquivo é o mesmo que detém a chave
de cifra: comprometê-lo compromete tudo o que a chave protege.

E o conteúdo de um arquivo pode carregar estrutura pensada para explorar quem o lê — profundidade
excessiva, referências que se expandem, caminho que aponta para fora do destino previsto.

## Critérios de aceitação

**CA-1** — *Dado* um arquivo acima do tamanho máximo configurado, *quando* for submetido, *então* é
recusado antes de ser gravado em qualquer lugar.

**CA-2** — *Dado* um arquivo cujo formato não corresponde ao declarado, *quando* for analisado,
*então* é recusado, e a verificação é do conteúdo — não da extensão do nome.

**CA-3** — *Dado* um nome de arquivo com caminho, caractere de controle ou referência a diretório
acima, *quando* for recebido, *então* o nome é descartado e substituído por identificador gerado
pelo sistema.

**CA-4** — *Dado* um arquivo estruturado que referencie conteúdo externo ou se expanda ao ser lido,
*quando* for analisado, *então* a referência não é seguida e a expansão é limitada.

**CA-5** — *Dado* qualquer submissão, *quando* ocorrer, *então* fica registrada com quem submeteu,
quando, o tamanho e o resumo criptográfico do que chegou — inclusive quando recusada.

## O que não exige

Não exige análise antivírus: o conteúdo não é executado, e a defesa é não interpretar, não detectar.

Não exige que o arquivo original seja preservado após a extração, decisão que pertence à política de
guarda.
