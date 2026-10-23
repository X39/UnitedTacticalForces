FROM node:24.11.1-bookworm AS node
FROM mcr.microsoft.com/dotnet/sdk:10.0.201 AS release-tests
COPY --from=node /usr/local/ /usr/local/
WORKDIR /src
COPY . .
# Match Microsoft.Playwright in Ui.Tests, independently of React's browser version.
RUN npx --yes playwright@1.61.0 install --with-deps chromium
RUN sh .mozaic/check-dotnet-release.sh

FROM scratch AS proof
COPY --from=release-tests /proof/ /proof/
