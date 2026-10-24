FROM mcr.microsoft.com/dotnet/aspnet@sha256:f879b8a9cb3f6694b8b0c9fa530eb0c39d8920db09c05a7edbd6c4c6c104ae1b
COPY capture-apt.sh runtime-dependencies.sh /tmp/utf-preparation/
RUN timeout --signal=TERM --kill-after=30 1800 sh /tmp/utf-preparation/runtime-dependencies.sh api && rm -rf /tmp/utf-preparation
WORKDIR /app
USER app
ENTRYPOINT []
CMD []
