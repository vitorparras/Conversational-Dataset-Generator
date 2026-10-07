# Política de segurança

Este projeto trata de dados que podem conter informação pessoal sensível, inclusive sobre
investigações. A superfície de risco não é só o software: é o dado.

## Relatar uma vulnerabilidade

**Não abra issue pública.** Use
[Security Advisories](https://github.com/vitorparras/Conversational-Dataset-Generator/security/advisories/new),
que permite discussão privada.

Inclua o que observou, como reproduzir, o impacto que enxerga e a versão. Se houver suspeita de
exposição de dado pessoal, diga isso na primeira linha — muda a prioridade.

Resposta esperada em até sete dias.

## O que conta como vulnerabilidade aqui

Além das classes usuais, e com prioridade mais alta:

- **Vazamento de dado pessoal** — dado identificável em registro, mensagem de erro, exportação ou
  resposta de interface
- **Falha de pseudonimização** — entidade que deveria ser substituída passando adiante, ou
  substituição reversível sem autorização
- **Quebra de cadeia de custódia** — alteração sem rastro, ou falha que impeça distinguir acidente
  de adulteração
- **Falha de isolamento entre domínios** — material de um domínio alcançável a partir de outro
- **Cifra ausente ou ineficaz** — dado sensível legível fora do ambiente, chave derivável de
  material acessível

A terceira merece destaque: defeito que destrói a capacidade de distinguir erro de fraude é tratado
como vulnerabilidade, não como falha comum.

## O que não entra neste repositório

Nem em código, nem em teste, nem em exemplo, nem em documentação: nome de pessoa real, endereço,
documento de identidade, telefone, texto de material real, chave, senha, credencial — mesmo expirada
— e caminho absoluto de máquina.

A verificação automática reprova o pull request que os contenha.

## Dados e acesso

O material não é distribuído com a ferramenta, e não há instância pública. O acesso pressupõe
solicitação formal, contrato assinado restringindo o uso a finalidade acadêmica ou de pesquisa, e
registro de quem acessou, quando e com que finalidade.

As políticas estão em [`docs/privacidade/`](docs/privacidade/).

## Se você encontrou dado sensível no repositório

Trate como incidente: **não abra issue, não comente em pull request, não cite o conteúdo** em lugar
público. Avise por Security Advisory, dizendo onde está sem reproduzir o dado, e aguarde
confirmação antes de qualquer divulgação.

Dado exposto em Git permanece no histórico mesmo após remoção do arquivo — a correção exige
reescrita de histórico e precisa ser coordenada.
