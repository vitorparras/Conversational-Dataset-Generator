# The runtime the self-contained host needs; base pinned by tag and digest.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble@sha256:62028293921f5bd6db3d9f9119cd29cd926dfd3c2336da23b09f425e40c3c832

USER root

RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends strace=6.8-0ubuntu2; \
    rm -rf /var/lib/apt/lists/*; \
    mkdir /app /work /trace; \
    chown app:app /work /trace

COPY --chmod=755 start.sh /usr/local/bin/start.sh

USER app

ENTRYPOINT ["/usr/local/bin/start.sh"]
