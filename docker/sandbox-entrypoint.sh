#!/bin/sh
# Copies read-only input to the workdir, restores offline, builds, tests, prints a JSON report.
# Network is disabled at container level. Restore must use a pre-populated offline cache or a prior restore step.
set -u
cp -r /input/. /work/src 2>/dev/null
cd /work/src || { echo '{"restore_ok":false,"build_ok":false,"error":"no input"}'; exit 0; }
timeout 120 dotnet restore >/work/restore.log 2>&1; R=$?
B=1; T=1
if [ $R -eq 0 ]; then
  timeout 180 dotnet build --no-restore -c Release >/work/build.log 2>&1; B=$?
fi
if [ $B -eq 0 ]; then
  timeout 240 dotnet test --no-build -c Release --logger "trx" >/work/test.log 2>&1; T=$?
fi
tail_log() { tail -c 4000 "$1" 2>/dev/null | sed 's/\\/\\\\/g; s/"/\\"/g' | tr '\n' ' '; }
printf '{"restore_ok":%s,"build_ok":%s,"tests_ok":%s,"log_excerpt":"%s"}\n' \
  "$([ $R -eq 0 ] && echo true || echo false)" \
  "$([ $B -eq 0 ] && echo true || echo false)" \
  "$([ $T -eq 0 ] && echo true || echo false)" \
  "$(tail_log /work/test.log)"
