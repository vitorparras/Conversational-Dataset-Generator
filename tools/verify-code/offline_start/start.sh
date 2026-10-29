#!/bin/sh
# Starts the host copied to /app under strace, waits for the line that says it serves, stops it the
# way a service manager does and exits with its code; 124 when the line never came in time.
# Everything the checks read goes to /trace: the host's output, the trace and the interfaces seen.
set -u

READY_SECONDS=${READY_SECONDS:-60}
STOP_SECONDS=${STOP_SECONDS:-20}
LISTENING='Now listening on: http://127.0.0.1:'

ls /sys/class/net > /trace/interfaces.txt
cp /app/Core.Host /work/Core.Host
chmod +x /work/Core.Host
cp /app/appsettings.json /work/appsettings.json
cd /work || exit 125

host_pid() {
    for entry in /proc/[0-9]*; do
        if [ "$(cat "$entry/comm" 2>/dev/null)" = Core.Host ]; then
            echo "${entry#/proc/}"
            return
        fi
    done
}

strace -f -qq --decode-pids=comm -e trace=network,openat -o /trace/strace.txt \
    ./Core.Host --Host:Urls=http://127.0.0.1:5000 --Recognizer:Kind=fake > /trace/host.log 2>&1 &
tracer=$!

waited=0
while [ "$waited" -lt $((READY_SECONDS * 5)) ]; do
    grep -qF "$LISTENING" /trace/host.log && break
    kill -0 "$tracer" 2> /dev/null || break
    sleep 0.2
    waited=$((waited + 1))
done

grep -qF "$LISTENING" /trace/host.log || timed_out=124
pid=$(host_pid)
[ -n "$pid" ] && kill -TERM "$pid"

waited=0
while kill -0 "$tracer" 2> /dev/null && [ "$waited" -lt $((STOP_SECONDS * 5)) ]; do
    sleep 0.2
    waited=$((waited + 1))
done
kill -0 "$tracer" 2> /dev/null && kill -KILL "$tracer" "${pid:-0}" 2> /dev/null

wait "$tracer"
code=$?
exit "${timed_out:-$code}"
