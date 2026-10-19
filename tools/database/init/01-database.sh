#!/bin/sh
# Creates the database, its owner and the application role. The passwords come from the environment
# and reach the server through psql's \getenv, never through a command line another process can read.
set -e
: "${CDG_OWNER_PASSWORD:?CDG_OWNER_PASSWORD ausente: defina a senha do papel owner}"
: "${CDG_APP_PASSWORD:?CDG_APP_PASSWORD ausente: defina a senha do papel app}"

psql -v ON_ERROR_STOP=1 --no-psqlrc --username "${POSTGRES_USER:-postgres}" --dbname postgres <<'SQL'
\getenv owner_password CDG_OWNER_PASSWORD
\getenv app_password CDG_APP_PASSWORD

CREATE ROLE owner LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS
    PASSWORD :'owner_password';
CREATE ROLE app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS
    PASSWORD :'app_password';

CREATE DATABASE cdg OWNER owner;
REVOKE ALL ON DATABASE cdg FROM PUBLIC;
GRANT CONNECT ON DATABASE cdg TO app;

\connect cdg
CREATE EXTENSION pgaudit;
ALTER SCHEMA public OWNER TO owner;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO app;
SQL
