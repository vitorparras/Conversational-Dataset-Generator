# 0046 — O material do canal cifrado chega por arquivos PEM

> Decisão vigente. 2026-10-31, decisões P1 a P3 do PO na issue #72.

## Contexto

A [0029](0029-identidade-e-sessao.md) manda cifrar toda troca com o sistema em trânsito, inclusive
em rede local. A issue #72 pede que a configuração não traga valor embutido e que a partida recuse,
nomeando o que falta. Faltava dizer em que forma o certificado e a chave privada chegam ao
processo, e o que o núcleo recusa nelas.

## Decisão

**O certificado e a chave privada vêm de dois arquivos PEM, e a configuração leva os caminhos,
nunca o material.**

- `Tls:CertificateFile` aponta o certificado e `Tls:KeyFile` aponta a chave privada, ambos em PEM.
  Os dois são obrigatórios ao servir, sem valor padrão. A chave não tem senha, e o núcleo a recusa
  se tiver: a barreira é a permissão do dono. Aceitas chaves RSA e de curva elíptica.
- O núcleo recusa, nomeando o arquivo e o que viu, nunca a chave: arquivo ausente; PEM ilegível;
  certificado e chave que não formam par; certificado vencido ou ainda não válido.
- A chave privada só pode ser aberta ao dono do processo, pela mesma regra da chave-mestra
  ([0044](0044-arquivo-de-custodia-da-chave.md)): no Linux, qualquer bit de grupo ou de outros; no
  Windows, qualquer permissão a quem não é o usuário do processo, SYSTEM ou Administradores. A
  chave também fica fora da pasta do artefato, também por ligação de pasta. O certificado é
  público e não tem essas duas exigências.
- Certificado autoassinado é aceito na instalação local. A validade é conferida na partida, não
  a cadeia de confiança.
- Não existe porta em claro, nem redirecionamento de uma porta em claro para a cifrada. O canal
  é cifrado também no loopback, sem exceção.
- O cookie de sessão é `Secure`. O `Strict-Transport-Security` entra como **suposição**, sem
  documento que o exija, e se reavalia quando a sessão existir.

## Alternativas recusadas

- **Certificado e chave no valor da configuração.** Aparece no ambiente do processo e do filho.
- **Arquivo PFX com senha.** A senha seria outro segredo para entregar, e a mesma barreira de
  permissão protegeria os dois.
- **Porta em claro que só redireciona.** O primeiro pedido, com a sessão, sairia em claro.
- **Exceção para o loopback.** A 0029 diz "inclusive em rede local", sem ressalva.

## Consequências

Quem opera precisa de um certificado antes da primeira partida; na instalação local, um
autoassinado serve. Exigir um cliente TLS (`openssl` ou `curl`) para conferir o núcleo muda a
[operação 001](../operacao/001-ambiente.md) e a premissa de "sem cliente HTTP" do roteiro 022, o
que fica para o dono confirmar. A chave privada é um segredo no disco da máquina que serve, com
a permissão do dono como única barreira local. Um certificado vencido derruba a partida: não há
renovação automática.

## O que reabre esta decisão

Se a instalação passar a ter um meio de entregar certificado e chave sem arquivo, ou uma
autoridade de certificação própria, a entrega por arquivo PEM deixa de ser a menor superfície. Se
o dono decidir que o loopback fica em claro, a 0029 muda antes.
