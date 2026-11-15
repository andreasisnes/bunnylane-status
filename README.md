# bunnylane-status

Bunny Lane status terminal: a public, lofi, terminal-looking web page that answers a fixed set of commands with live homelab metrics. Visitors type at a real prompt; the backend owns every command and never passes visitor text to anything.

Design and plans live in the homelab repo: `docs/design/2026-11-15-status-terminal-design.md`.

## Commands

| Command | Output |
| --- | --- |
| `help` | the command table |
| `status` | `[OK]`/`[DEGRADED]` per area: nodes, storage, workloads, edge |
| `nodes` | node pool as `cp-1..`, `w-1..`: role, cpu, memory, load, uptime |
| `net [1h\|24h\|7d\|30d]` | WAN in/out now, sparkline, chart, per-node table |
| `disks` | storage per worker, volume count, replica health |
| `nas` | media share used/total and growth per day |
| `gpu [1h\|24h]` | gpu busy, vram, temperature, chart |
| `vps [month]` | this month's traffic against the 6 TB cap; `month` lists the last six |
| `top [month\|all]` | leaderboard: watch time and VPS data per display name |
| `now` | streams playing, total bitrate, gpu in use |
| `watch <cmd>` | re-run every 2 s over SSE until `Ctrl+C` or `stop` |
| `clear`, `history`, `theme [night\|dawn]` | local to the browser |

Anything else returns `command not found`.

## Layout

- `backend/` .NET 10 minimal API: allow-list parser, Prometheus and Jellyfin clients, SSE streams, leaderboard collectors (Jellyfin session sampler, Traefik access-log follower), SQLite via EF Core. Serves the built frontend from `wwwroot`.
- `frontend/` Vite + React + TypeScript block terminal with uPlot charts.
- `Dockerfile` one multi-stage image: `ghcr.io/andreasisnes/bunnylane-status`.

## Configuration

Bound from the `Status` section; environment variables use the `STATUS__` prefix.

| Setting | Default | Meaning |
| --- | --- | --- |
| `STATUS__PrometheusUrl` | `http://kps-prometheus.monitoring.svc:9090` | Prometheus base URL |
| `STATUS__JellyfinUrl` | `http://jellyfin.jellyfin.svc:8096` | Jellyfin base URL |
| `STATUS__JellyfinApiKey` | empty | API key (Dashboard → API Keys); without it `now`/`top` show `[UNKNOWN]` |
| `STATUS__VpsCapBytes` | `6000000000000` | monthly cap, both directions summed |
| `STATUS__TimeZone` | `Europe/Oslo` | month boundaries and leaderboard days |
| `STATUS__HiddenUsers__0` | none | display names to leave out of `top` |
| `STATUS__TraefikNamespace` | `traefik` | where the access-log follower looks for Traefik pods |
| `STATUS__SqlitePath` | `/data/status.db` (image) | leaderboard database |

The follower needs a ServiceAccount with `get`/`list` on pods and `get` on `pods/log` in the Traefik namespace. Without cluster credentials it logs a warning and VPS attribution stays off.

## Endpoints

- `POST /api/command` `{ "line": "status" }` → `CommandResult`; 10 per second per client.
- `GET /api/watch?line=net` and `GET /api/ticker` → `text/event-stream`; 4 concurrent streams per client.
- `GET /healthz` always 200 while the process is up; `GET /readyz` 200 once Prometheus answered within 60 s.

## Develop

```bash
dotnet test backend
cd frontend && npm ci && npm test && npm run lint
cd frontend && npx playwright test          # needs a browser; or run in mcr.microsoft.com/playwright:v1.65.1-noble
docker build -t bunnylane-status:dev . && docker run --rm -p 8080:8080 -e STATUS__PrometheusUrl=http://host.docker.internal:9090 bunnylane-status:dev
```

For local UI work run `dotnet run --project backend/BunnyLane.Status` and `npm run dev` in `frontend`; Vite proxies `/api` to port 5000.

## Release and deployment

Conventional Commits; the release notes are generated from them (`cliff.toml`).

1. **CI** (`ci.yml`) runs on every pull request. On a push to `main` it also publishes the image `ghcr.io/andreasisnes/bunnylane-status:<sha>` (and `:latest`) and the manifests in `syncroot/` as the OCI artifact `oci://ghcr.io/andreasisnes/bunnylane-status/bunnylane-status-syncroot:<sha>`, with `syncroot/prod` pinned to that image. Nothing reaches production yet.
2. **Release** (`release.yml`) runs after a successful CI run on `main`. It deletes any existing draft release and creates a new draft `bunnylane-status YYYY.MM.DD-<sha>` with the changelog since the last published release. There is always at most one draft, for the latest commit.
3. **CD** (`cd.yml`) runs when a release is published: it tags the image and the manifests artifact with the release tag and with `prod`.

The homelab repo holds the namespace, the sops-encrypted `status-jellyfin` secret, an `OCIRepository` following `prod` and a Flux `Kustomization` with `path: ./prod`. **Publishing the draft release is the deploy**; Flux picks the new `prod` tag up within 10 minutes.

The manifests expect namespace `status`, a `longhorn` storage class, the `letsencrypt-prod` ClusterIssuer, Traefik with its CRDs, and Traefik pods labelled `app.kubernetes.io/name=traefik` in namespace `traefik`. The GHCR package for the syncroot must be public (or the homelab `OCIRepository` needs a pull secret); set its visibility once after the first push.
