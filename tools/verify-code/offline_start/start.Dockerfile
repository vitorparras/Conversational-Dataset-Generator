# The runtime the self-contained host needs, plus strace and openssl (for the throwaway certificate); base pinned by tag and digest. The image is
# built with a network and run without one, so nothing is installed while the host starts.
# strace and openssl carry no version here (#728): Ubuntu drops a version from the archive when a
# security update replaces it, and a pinned one broke the build of every pull request. The base
# digest fixes the system; the versions the build got are read from the image and shown by the
# step (steps/offline_containers.py, installed_packages), so a run still says what it ran with.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble@sha256:62028293921f5bd6db3d9f9119cd29cd926dfd3c2336da23b09f425e40c3c832

USER root

RUN set -eux; \
    apt-get -o Acquire::Retries=3 update; \
    apt-get -o Acquire::Retries=3 install -y --no-install-recommends strace openssl; \
    rm -rf /var/lib/apt/lists/*; \
    mkdir /app /work /trace; \
    chown app:app /work /trace

COPY --chmod=755 start.sh /usr/local/bin/start.sh

USER app

ENTRYPOINT ["/usr/local/bin/start.sh"]
