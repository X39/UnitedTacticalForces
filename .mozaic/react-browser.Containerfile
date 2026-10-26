# The native action binds this alias to the reviewed Node 24 Bookworm digest.
FROM node:24.11.1-bookworm AS browser-tests
WORKDIR /src
COPY sources/ReactApp/ ./sources/ReactApp/
COPY sources/WebApp/Properties/Language.resx sources/WebApp/Properties/Language.de.resx ./sources/WebApp/Properties/
COPY sources/Common/Ui/routes.json ./sources/Common/Ui/routes.json
WORKDIR /src/sources/ReactApp
RUN npm ci
# Recovery CI retains the existing Chromium application journeys.
RUN npx playwright install --with-deps chromium
RUN CI=true npm run test:e2e -- --workers=1 \
    && mkdir /proof \
    && printf 'React Chromium journeys passed\n' > /proof/result

# Export only a small proof, keeping browser installation out of the output archive.
FROM scratch AS proof
COPY --from=browser-tests /proof/ /proof/
