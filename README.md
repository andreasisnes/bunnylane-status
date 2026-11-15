# bunnylane-status

Bunny Lane status terminal: a public, lofi, terminal-looking web page that answers a fixed set of commands with live homelab metrics.

Design and plans live in the homelab repo: `docs/design/2026-11-15-status-terminal-design.md`.

## Layout

- `backend/` .NET 10 minimal API: command parser, Prometheus and Jellyfin clients, SSE streams, leaderboard collectors. Serves the built frontend from `wwwroot`.
- `frontend/` Vite + React + TypeScript block terminal.
- `Dockerfile` one multi-stage image: `ghcr.io/andreasisnes/bunnylane-status`.

## Develop

```bash
dotnet test backend
cd frontend && npm ci && npm test
docker build -t bunnylane-status:dev . && docker run --rm -p 8080:8080 bunnylane-status:dev
```
