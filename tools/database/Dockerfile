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

# The server's own record of what the application does: reads and writes, as csvlog in a directory outside the data directory, which
# the server writes and its group reads, and never a parameter of a statement, nor an error with its detail, which names the row. Rotation never deletes.
RUN set -eux; \
    install -d -o postgres -g postgres -m 0750 /var/log/postgresql; \
    printf '%s\n' \
        "logging_collector = on" \
        "log_destination = 'csvlog'" \
        "log_directory = '/var/log/postgresql'" \
        "log_file_mode = 0640" \
        "log_truncate_on_rotation = off" \
        "pgaudit.log = 'read, write'" \
        "pgaudit.log_parameter = off" \
        "log_parameter_max_length = 0" \
        "log_parameter_max_length_on_error = 0" \
        "log_min_messages = log" \
        >> /usr/share/postgresql/postgresql.conf.sample

COPY --chown=postgres:postgres init/ /docker-entrypoint-initdb.d/

USER postgres

# TCP only answers once initialization is over: the temporary server listens on the socket alone.
HEALTHCHECK --interval=5s --timeout=5s --start-period=60s --retries=12 \
    CMD ["pg_isready", "--host=127.0.0.1", "--port=5432", "--quiet"]
