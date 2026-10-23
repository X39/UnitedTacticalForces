# The native action binds this alias to the reviewed Node 24 Bookworm digest.
FROM node:24.11.1-bookworm AS browser-tests
WORKDIR /src
COPY sources/ReactApp/ ./sources/ReactApp/
COPY sources/WebApp/Properties/Language.resx sources/WebApp/Properties/Language.de.resx ./sources/WebApp/Properties/
COPY sources/Common/Ui/routes.json ./sources/Common/Ui/routes.json
WORKDIR /src/sources/ReactApp
RUN npm ci
# A full release retains the existing published-release browser matrix.
RUN npx playwright install --with-deps chromium firefox webkit
RUN CI=true UTF_FULL_BROWSER_MATRIX=true npm run test:e2e -- --workers=1 \
    && mkdir /proof \
    && printf 'React Chromium, Firefox and WebKit journeys passed\n' > /proof/result

# Export only a small proof, keeping browser installation out of the output archive.
FROM scratch AS proof
COPY --from=browser-tests /proof/ /proof/
