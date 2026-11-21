# B-05 — one-command startup in Docker:  docker compose up --build   (from SuperCom-Platform/)
# Build context: the folder that contains BOTH SuperCom-Platform/ and the Part 3 suite SuperCom/.

# 1) React client → static files
FROM node:20-bookworm AS client
WORKDIR /src/client
COPY SuperCom-Platform/client/package*.json ./
RUN npm ci --no-audit --no-fund
COPY SuperCom-Platform/client/ ./
RUN npx vite build --outDir /out/wwwroot --emptyOutDir

# 2) ASP.NET Core API → published binaries
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS server
WORKDIR /src
COPY SuperCom-Platform/server/ ./
RUN dotnet publish src/TestPlatform.Api -c Release -o /out/app

# 3) Runtime = Playwright image (Node + matching browsers) + ASP.NET Core 8 runtime,
#    because the API launches the Part 3 suite as a child process.
FROM mcr.microsoft.com/playwright:v1.61.0-noble
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates libicu74 \
 && rm -rf /var/lib/apt/lists/* \
 && curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && bash /tmp/dotnet-install.sh --channel 8.0 --runtime aspnetcore --install-dir /usr/share/dotnet \
 && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
 && rm /tmp/dotnet-install.sh

# The Part 3 suite (dependencies installed for Linux inside the image)
WORKDIR /suite
COPY SuperCom/package*.json ./
RUN npm ci --no-audit --no-fund
COPY SuperCom/ ./

# The platform: API + built client in its wwwroot
WORKDIR /app
COPY --from=server /out/app ./
COPY --from=client /out/wwwroot ./wwwroot

# Listen on all interfaces inside the container; the reporter (same container) reaches it on localhost.
ENV ASPNETCORE_URLS=http://0.0.0.0:5080 \
    Urls=http://0.0.0.0:5080 \
    Platform__PublicUrl=http://localhost:5080 \
    Platform__DataDirectory=/data \
    Suite__WorkingDirectory=/suite
EXPOSE 5080
VOLUME /data
CMD ["dotnet", "TestPlatform.Api.dll"]
