# Build-and-test runner for UNTRUSTED Java candidate code. Same hardening contract as
# Dockerfile.sandbox (read this one's header comment first) - run ONLY with:
#   docker run --rm --network none --read-only \
#     --tmpfs /work:rw,size=1024m,exec,uid=10001,gid=10001,mode=1777 \
#     --tmpfs /tmp:rw,size=64m,uid=10001,gid=10001,mode=1777 \
#     --cap-drop ALL --security-opt no-new-privileges --pids-limit 256 \
#     --memory 1g --cpus 1 --user 10001:10001 --env HOME=/work \
#     -v <readonly-input>:/input:ro herproof-sandbox-java
FROM eclipse-temurin:21-jdk
RUN useradd -u 10001 -m runner
WORKDIR /work

# JUnit Platform Console Standalone is the one dependency this sandbox needs - baked into the image
# at build time, when network is actually available, exactly like the offline NuGet cache in
# Dockerfile.sandbox. Network is disabled at runtime, so this jar is the only way tests ever run.
ARG JUNIT_CONSOLE_VERSION=1.11.3
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /opt/junit \
    && curl -fsSL -o /opt/junit/junit-platform-console-standalone.jar \
       "https://repo1.maven.org/maven2/org/junit/platform/junit-platform-console-standalone/${JUNIT_CONSOLE_VERSION}/junit-platform-console-standalone-${JUNIT_CONSOLE_VERSION}.jar"

COPY --chmod=0555 docker/sandbox-entrypoint-java.sh /usr/local/bin/sandbox-entrypoint.sh
USER 10001:10001
ENV TMPDIR=/work/tmp HOME=/work
ENTRYPOINT ["/usr/local/bin/sandbox-entrypoint.sh"]
