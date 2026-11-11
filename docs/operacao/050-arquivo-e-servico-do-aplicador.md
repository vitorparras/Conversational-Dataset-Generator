# 050 — O arquivo e o serviço do aplicador

> Operacional. 2026-11-09. Decisão [0053](../decisoes/0053-configuracao-alterada-pela-tela.md).
> A conta e o papel do banco estão em [049](049-aplicador-da-configuracao.md).

## O arquivo do aplicador

applier.json, ao lado do binário, de modo `0600` e dono `aplicador`: a conta do núcleo não o
lê. Ele guarda a senha do papel `leitura`. As chaves são fechadas: chave que falta, caminho
relativo ou que não existe recusa o início nomeando a chave.

```
{
  "Applier": {
    "SettingsFile": "/opt/cdg/appsettings.json",
    "RestartCommand": "/usr/bin/systemctl",
    "RestartArguments": "restart|cdg-nucleo.service"
  },
  "Database": { "Host": "127.0.0.1:5432", "Name": "cdg", "Username": "leitura", "Password": "…" }
}
```

O reinício roda **sem shell** e sem nada do banco nos argumentos; os argumentos se separam por
`|`. O appsettings.json é de `aplicador:nucleo` com modo `0640`: o núcleo lê e não grava
(CA-7). A pasta tem de deixar o aplicador criar e trocar arquivos nela. O arquivo novo nasce já
com o modo do antigo, para que a senha do banco nunca fique legível por outros, nem por um
instante.

## O serviço

Um serviço de execução única e um temporizador, sob a conta `aplicador`:

```
# /etc/systemd/system/cdg-aplicador.service
[Service]
Type=oneshot
User=aplicador
ExecStart=/opt/cdg/Core.Host apply-configuration

# /etc/systemd/system/cdg-aplicador.timer
[Timer]
OnBootSec=2min
OnUnitActiveSec=5min
[Install]
WantedBy=timers.target
```

`systemctl restart cdg-nucleo.service` é permitido ao `aplicador` por uma regra do `polkit` ou do
`sudoers` restrita a esse comando.
