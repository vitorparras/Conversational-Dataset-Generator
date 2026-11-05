# 033 — A primeira pessoa numa máquina operada de fora

> Operacional. 2026-11-05.

Quando a máquina é um contêiner operado de fora ([021](021-subir-o-nucleo-publicado.md)), o `read`
de [028](028-criar-primeira-pessoa.md) não tem terminal. A senha da pessoa vai para um arquivo de
modo 600, como as de 021, e o comando a lê dele. Depois de 021 e antes de
[023](023-iniciar-o-nucleo-publicado.md); a conta é a de [032](032-conta-do-nucleo.md).

A regra da senha: não vazia, sem aspas nem barra invertida (que quebrariam o JSON da entrada em
[022](022-conferir-o-nucleo-publicado.md)) e sem quebra de linha no fim, que o `printf` abaixo não
acrescenta. Na demonstração serve a do exemplo, `senha-de-demonstracao-1`. `CONTA` e `CONTEINER` são
marcadores:

```
printf '%s' 'senha-de-demonstracao-1' | docker exec -i -u CONTA CONTEINER bash -c 'umask 077; cat > /tmp/senha-pessoa'
```

O comando é um bloco de máquina: a senha da aplicação vem de `/tmp/senha-app` (021), e o nome
(`Ana`) é o que a entrada pede em `name`. O código de saída é o de 028, e 0 diz o id da pessoa:

```
cd /opt/cdg/nucleo
CDG_Database__Password="$(cat /tmp/senha-app)" ./Core.Host create-person --name Ana \
  --function "Administração técnica" --Installation:Kind=demonstracao --Passwords:WorkFactor=100000 \
  --Database:Host=db:5432 --Database:Name=cdg --Database:Username=app < /tmp/senha-pessoa
echo "saída=$?"
```

A 022 lê `/tmp/senha-pessoa` para entrar, e a 023 o apaga no fim. O `--Installation:Kind` e o fator
são os de 023: sem o `Kind`, a instalação vale `real`, e só então o fator de demonstração sai
com 78.
