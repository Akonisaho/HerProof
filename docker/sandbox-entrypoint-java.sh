#!/bin/sh
# Same contract as sandbox-entrypoint.sh (docker/Dockerfile.sandbox): copies read-only input to the
# workdir, compiles, runs the hidden JUnit tests, prints one JSON report line. Network is disabled at
# container level - the JUnit Platform Console Standalone jar is baked into the image at build time
# (see Dockerfile.sandbox.java), so nothing here ever needs the network it doesn't have.
set -u
mkdir -p /work/src /work/tmp /work/classes
cp -r /input/. /work/src 2>/dev/null
cd /work/src || { echo '{"restore_ok":false,"build_ok":false,"tests_ok":false,"log_excerpt":"no input"}'; exit 0; }

# No package manager step for Java in this sandbox - the only dependency (JUnit) is already on the
# image's classpath, so "restore" is just confirming the compiler and the JUnit jar are actually there.
R=0
if ! command -v javac >/dev/null 2>&1 || [ ! -f /opt/junit/junit-platform-console-standalone.jar ]; then
  R=1
fi

B=1; T=1
if [ $R -eq 0 ]; then
  timeout 120 javac -cp /opt/junit/junit-platform-console-standalone.jar -d /work/classes *.java >/work/build.log 2>&1; B=$?
fi
if [ $B -eq 0 ]; then
  # The launcher's default class-name filter only matches names like FooTest/FooTests - our hidden
  # test files are named HiddenTest1.java, HiddenTest2.java etc. and would silently match zero
  # classes without this override (confirmed live: build_ok true, 0 tests found, no error at all).
  timeout 180 java -jar /opt/junit/junit-platform-console-standalone.jar \
    --disable-banner --details=summary --include-classname=".*" \
    --class-path /work/classes --scan-classpath /work/classes \
    >/work/test.log 2>&1
  T=$?
  # Parse the actual counts rather than trusting the exit code or a plain string match - "0 tests
  # successful" legitimately CONTAINS the substring "tests successful", and the launcher's own exit
  # code is also non-zero when it simply finds zero tests, so neither can mean "it passed" alone.
  # tests_ok is only ever true when at least one test ran, none failed, and none were aborted.
  FOUND=$(grep -oE '[0-9]+ tests found' /work/test.log | grep -oE '^[0-9]+')
  FAILED=$(grep -oE '[0-9]+ tests failed' /work/test.log | grep -oE '^[0-9]+')
  ABORTED=$(grep -oE '[0-9]+ tests aborted' /work/test.log | grep -oE '^[0-9]+')
  if [ -n "$FOUND" ] && [ "$FOUND" -gt 0 ] && [ "${FAILED:-0}" -eq 0 ] && [ "${ABORTED:-0}" -eq 0 ]; then
    T=0
  else
    T=1
  fi
fi

tail_log() { tail -c 4000 "$1" 2>/dev/null | sed 's/\\/\\\\/g; s/"/\\"/g' | tr '\n' ' '; }
if [ $R -ne 0 ]; then EXCERPT_LOG=/dev/null
elif [ $B -ne 0 ]; then EXCERPT_LOG=/work/build.log
else EXCERPT_LOG=/work/test.log
fi
printf '{"restore_ok":%s,"build_ok":%s,"tests_ok":%s,"log_excerpt":"%s"}\n' \
  "$([ $R -eq 0 ] && echo true || echo false)" \
  "$([ $B -eq 0 ] && echo true || echo false)" \
  "$([ $T -eq 0 ] && echo true || echo false)" \
  "$(tail_log "$EXCERPT_LOG")"
