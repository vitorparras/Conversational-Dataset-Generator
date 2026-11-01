#!/bin/sh
# Brings the host up as an operator would, under strace: migrates the schema with the owner
# credential, then serves with the application's, the language service folder beside the binary.
# Waits for the line that says it serves, stops it the way a service manager does and records the
# exit codes and how long the serve run took to say it serves (from launching the host to that
# line). Everything the checks read goes to /trace; the passwords come in the environment.
set -u

# The three deadlines add up to 210 s, far below the 30 min of the job that runs this.
MIGRATE_SECONDS=${MIGRATE_SECONDS:-60}
READY_SECONDS=${READY_SECONDS:-120}
STOP_SECONDS=${STOP_SECONDS:-30}
LISTENING='Now listening on: https://127.0.0.1:'

export CDG_Database__Host=127.0.0.1:5432 CDG_Database__Name=cdg CDG_Installation__Kind=demonstracao CDG_Passwords__WorkFactor=1 CDG_Session__IdleTimeout=00:30:00 CDG_Session__AbsoluteTimeout=08:00:00

# A throwaway key and no labels, in files outside the folder the host runs from and open to its user alone.
custody=$(umask 077 && mktemp -d /tmp/custody.XXXXXX)
(umask 077 && head -c 32 /dev/urandom | base64 > "$custody/key" && : > "$custody/labels")
export CDG_MasterKey__File="$custody/key" CDG_MasterKey__LabelsFile="$custody/labels"

# A throwaway certificate for the channel, which is encrypted also on the loopback; the key stays in the same private folder.
openssl req -x509 -newkey ec -pkeyopt ec_paramgen_curve:prime256v1 -nodes -subj /CN=localhost -days 1 \
    -keyout "$custody/tls.key" -out "$custody/tls.crt" 2> /dev/null || exit 125
export CDG_Tls__CertificateFile="$custody/tls.crt" CDG_Tls__KeyFile="$custody/tls.key"

ls /sys/class/net > /trace/interfaces.txt
cp /app/Core.Host /app/appsettings.json /work/
chmod +x /work/Core.Host
cd /work || exit 125

# The trace keeps the network calls and, of the files opened, only the resolver configuration.
trace() {
    name=$1
    shift
    strace -f --seccomp-bpf -qq --decode-pids=comm -e trace=network,openat -o "/tmp/$name.full" "$@" \
        > "/trace/$name.log" 2>&1
}

keep() {
    awk '!/openat\(/ || /resolv\.conf/' "/tmp/$1.full" > "/trace/$1.strace"
}

host_pid() {
    for entry in /proc/[0-9]*; do
        if [ "$(cat "$entry/comm" 2> /dev/null)" = Core.Host ]; then
            echo "${entry#/proc/}"
            return
        fi
    done
}

now_ms() { echo $(($(date +%s%N) / 1000000)); }

# Polls every twentieth of a second until the command succeeds or the seconds run out; the pause
# is what the start time is read with, so it stays well under the limit the step judges it by.
wait_for() {
    seconds=$1
    shift
    waited=0
    while [ "$waited" -lt $((seconds * 20)) ]; do
        "$@" && return 0
        sleep 0.05
        waited=$((waited + 1))
    done
    return 1
}

serving() { grep -qF "$LISTENING" /trace/serve.log || ! kill -0 "$tracer" 2> /dev/null; }
stopped() { ! kill -0 "$tracer" 2> /dev/null; }

(
    export CDG_Database__Username=owner CDG_Database__Password="$OWNER_PASSWORD"
    trace migrate timeout "$MIGRATE_SECONDS" ./Core.Host migrate
)
migrate=$?
keep migrate

began=$(now_ms)
(
    export CDG_Database__Username=app CDG_Database__Password="$APP_PASSWORD"
    trace serve ./Core.Host
) &
tracer=$!

wait_for "$READY_SECONDS" serving
ready_ms=$(($(now_ms) - began))
grep -qF "$LISTENING" /trace/serve.log && ready=yes || ready=no
pid=$(host_pid)
[ -n "$pid" ] && kill -TERM "$pid"
wait_for "$STOP_SECONDS" stopped || kill -KILL "$pid" "$tracer" 2> /dev/null
wait "$tracer"
serve=$?
keep serve

printf 'migrate=%s\nserve=%s\nready=%s\nready_ms=%s\n' "$migrate" "$serve" "$ready" "$ready_ms" \
    > /trace/result.txt
