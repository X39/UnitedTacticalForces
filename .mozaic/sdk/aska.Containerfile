FROM mcr.microsoft.com/dotnet/runtime@sha256:ca17638b4cbab2063771e8943dbe887ea6c0ae00c5fede3498d5938758c85dfe
COPY capture-apt.sh runtime-dependencies.sh /tmp/utf-preparation/
RUN timeout --signal=TERM --kill-after=30 2400 sh /tmp/utf-preparation/runtime-dependencies.sh aska && rm -rf /tmp/utf-preparation
WORKDIR /app
USER root
ENTRYPOINT []
CMD []
