FROM mcr.microsoft.com/dotnet/sdk@sha256:3dae2f7699441af56216ff64d5c9b6dfce7cd7dc7f4f71d353d29662b10a384f AS sdk302
FROM docker.io/library/node@sha256:fe306151aeafaf7c94e03dd3aff2435c91621f71a58bba3a09579c89d3ba730e AS node
FROM docker.io/library/rust@sha256:8bf7188ae46ad73b801c8d405eedc13ce63a7852b562885743a56890723549a8 AS rust
FROM mcr.microsoft.com/dotnet/sdk@sha256:c804b88aaca8d6b76f65998427adec3101dcbbf2e52e705e2509eb81263aab0c
COPY --from=sdk302 /usr/share/dotnet/ /opt/utf-dotnet-302/
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules/npm/ /usr/local/lib/node_modules/npm/
COPY --from=rust /usr/local/rustup/ /usr/local/rustup/
COPY --from=rust /usr/local/cargo/ /usr/local/cargo/
ENV RUSTUP_HOME=/usr/local/rustup CARGO_HOME=/usr/local/cargo RUSTUP_AUTO_INSTALL=0 \
    PATH=/usr/local/cargo/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin \
    UTF_DOTNET_201=/usr/share/dotnet/dotnet UTF_DOTNET_302=/opt/utf-dotnet-302/dotnet \
    UTF_DOTNET_PLAYWRIGHT_BROWSERS_PATH=/opt/utf-playwright-browsers/dotnet \
    UTF_REACT_PLAYWRIGHT_BROWSERS_PATH=/opt/utf-playwright-browsers/react
COPY capture-apt.sh sdk-dependencies.sh select-dotnet.sh /tmp/utf-preparation/
RUN timeout --signal=TERM --kill-after=30 3600 sh /tmp/utf-preparation/sdk-dependencies.sh && rm -rf /tmp/utf-preparation
COPY archives/ /opt/utf-runtime-archives/
COPY inputs.json /usr/share/utf-preparation/inputs.json
COPY smoke-browser.cjs /opt/utf-preparation/smoke-browser.cjs
RUN cd /opt/utf-runtime-archives && sha256sum --strict --check SHA256SUMS && chmod -R a+rX,a-w /opt/utf-runtime-archives
WORKDIR /workspace
USER 65532:65532
ENTRYPOINT []
CMD []
