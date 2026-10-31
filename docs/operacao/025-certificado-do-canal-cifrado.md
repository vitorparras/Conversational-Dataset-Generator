# 025 — Certificado e chave do canal cifrado

> Operacional. 2026-10-31.

Os dois arquivos PEM que a [decisão 0046](../decisoes/0046-material-do-canal-cifrado.md) manda o
núcleo ler na partida: `Tls:CertificateFile` (o certificado) e `Tls:KeyFile` (a chave privada, sem
senha). A configuração leva os caminhos, nunca o material. Vem antes de
[023](023-iniciar-o-nucleo-publicado.md), e `Host:Urls` só aceita endereços `https://`.

## Onde moram

A chave fora da pasta do executável, só com o dono do processo, como a de
[024](024-arquivos-de-custodia-da-chave.md): modo 600 no arquivo e 700 na pasta, no Linux. O
certificado é público, e o núcleo não exige dele nem a permissão nem o lugar. Sem os dois arquivos,
com PEM ilegível, com chave que não forma par com o certificado ou com certificado vencido ou ainda
não válido, a partida recusa e a recusa nomeia o arquivo e o que viu, nunca a chave.

## Criar numa demonstração

O certificado autoassinado serve à instalação local. Os valores são os do exemplo de
[021](021-subir-o-nucleo-publicado.md), a trocar pelos do seu ambiente (`-days` é a validade):

```
( umask 077; mkdir -p /etc/cdg/tls; cd /etc/cdg/tls \
  && openssl req -x509 -newkey ec -pkeyopt ec_paramgen_curve:prime256v1 -nodes \
    -subj /CN=localhost -days 30 -keyout chave.pem -out certificado.pem 2> /dev/null )
```

Numa instalação real, o certificado vem da autoridade que a instalação usa, e a chave, de quem o
emitiu. O núcleo confere a validade na partida, não a cadeia de confiança, e não renova nada: um
certificado que vence derruba a próxima partida.

## Conferir

O canal é cifrado também no loopback, então o roteiro de [022](022-conferir-o-nucleo-publicado.md)
usa `openssl s_client`, que não confere a cadeia de um autoassinado. Um pedido em claro à porta
não recebe conteúdo, e toda resposta traz `Strict-Transport-Security` (suposição da decisão 0046).
