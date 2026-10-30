# Where the service folder is assembled, with a network: Python, git and the tools the build calls.
# Base pinned by tag and digest. The folder it makes runs in another container, without a network.
FROM python:3.12-slim-bookworm@sha256:e697c81897302a4c4333bade000d98038fbb21eb064f8ee1fd42d02ebcd9d337

RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends git; \
    rm -rf /var/lib/apt/lists/*; \
    useradd --create-home --uid 10001 build; \
    mkdir /input /out; \
    chown build:build /out

ENV PATH=/home/build/.local/bin:$PATH

USER build
