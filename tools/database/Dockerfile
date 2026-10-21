# PostgreSQL with the pgaudit extension preloaded; base pinned by tag and digest.
FROM postgres:17.11-bookworm@sha256:3645570cccdfa447589da9f57dd740faa29b30938e861289a5574b6ca6b03826

USER root

RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends "postgresql-17-pgaudit=17.1-2.pgdg12+1"; \
    rm -rf /var/lib/apt/lists/*

# Every cluster initdb creates from the sample loads pgaudit, whatever command the container gets.
RUN set -eux; \
    sed -ri "s!^#?(shared_preload_libraries)\s*=\s*'[^']*'!\1 = 'pgaudit'!" \
        /usr/share/postgresql/postgresql.conf.sample; \
    grep -F "shared_preload_libraries = 'pgaudit'" /usr/share/postgresql/postgresql.conf.sample

COPY --chown=postgres:postgres init/ /docker-entrypoint-initdb.d/

USER postgres

# TCP only answers once initialization is over: the temporary server listens on the socket alone.
HEALTHCHECK --interval=5s --timeout=5s --start-period=60s --retries=12 \
    CMD ["pg_isready", "--host=127.0.0.1", "--port=5432", "--quiet"]
