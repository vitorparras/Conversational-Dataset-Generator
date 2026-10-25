from dataclasses import dataclass


@dataclass
class Settings:
    access_token: str = ""


def configure(settings: Settings) -> dict[str, str]:
    # ruleid: secret-literal-python
    password = "planted-value-pw"
    # ruleid: secret-literal-python
    api_key: str = "planted-value-key"
    # ruleid: secret-literal-python
    settings.access_token = "planted-value-token"
    # ruleid: secret-literal-python
    dsn = "host=db;user=app;password=planted"
    # ruleid: secret-literal-python
    token = "ghp_" + "planted-value-joined"
    # ok: secret-literal-python
    secret = b"ZQX-MARKER-7731-startup-secret"
    # ok: secret-literal-python
    default_token = "none"
    # ok: secret-literal-python
    secret_header = b"x-service-secret"
    # ok: secret-literal-python
    missing_secret_message = "o segredo não chegou pela entrada padrão"
    print(password, api_key, dsn, token, secret, default_token, secret_header)
    # ruleid: secret-literal-python
    return {"client_secret": "planted-value-dict", "message": missing_secret_message}


def copy(dsn: str) -> str:
    # ok: secret-literal-python
    other = dsn
    return other


# ruleid: secret-literal-python
def connect(signing_key: str = "planted-value-signing") -> str:
    # ruleid: secret-literal-python
    encryption_key = "planted-value-encryption"
    # ok: secret-literal-python
    cache_key = "settings-cache"
    # ok: secret-literal-python
    replacement_token = "PESSOA_1"
    # ok: secret-literal-python
    secret_key = "LanguageService:Secret"
    return signing_key + encryption_key + cache_key + replacement_token + secret_key
