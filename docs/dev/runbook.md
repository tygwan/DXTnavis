# DX Platform Runbook (Dev)

## Components & Versions
- DXBase: .NET Standard 2.0 / .NET 8.0
- DXrevit: .NET 8.0-windows, Revit 2025 API
- DXnavis: .NET Framework 4.8 (planned)
- PostgreSQL: 15+
- Extensions: pgvector 0.5+
- FastAPI server:
  - fastapi 0.110.x
  - uvicorn 0.30.x
  - asyncpg 0.29.x
  - pydantic 2.7.x, pydantic-settings 2.2.x
  - prometheus-client 0.20.x

## Prereqs
- PostgreSQL 15+ running locally, database `dx_platform` created.
- `database/init_database.sql` executed (includes pgvector, schemas, analytics views).
- .NET SDK 8.0, Revit/Autodesk installations per `docs/dev/setup-guide.md`.

## Running the FastAPI Server
1) Prepare environment
- `cd fastapi_server`
- `python -m venv venv`
- Windows: `venv\\Scripts\\activate` (Mac/Linux: `source venv/bin/activate`)
- `pip install -r requirements.txt`
- Create `.env` from `.env.example` and set:
  - `DATABASE_URL=postgresql://dx_api_role:...@localhost:5432/dx_platform`
  - `ALLOWED_ORIGINS=http://localhost`
  - `ALLOWED_HOSTS=localhost,127.0.0.1`

2) Launch
- `uvicorn fastapi_server.main:app --host 0.0.0.0 --port 5000 --reload`

3) Smoke test
- Health: `GET /health` → `{ status: ok|degraded, db: ok|error }`
- Metrics: `GET /metrics` (Prometheus format)
- Ingest: `POST /api/v1/ingest` (DXrevit payload)
- Analytics:
  - `GET /api/v1/models/versions?projectName=...&limit=100&offset=0`
  - `GET /api/v1/models/{version}/summary`
  - `GET /api/v1/models/compare?v1=...&v2=...&changeType=ADDED&limit=1000`
  - `GET /api/v1/timeliner/{version}/mapping`

## Security & CORS
- CORS: allow-origins from `.env` (`ALLOWED_ORIGINS`).
- Trusted hosts: set `ALLOWED_HOSTS` to exact hosts in production.
- Security headers (middleware):
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: no-referrer`
  - `Permissions-Policy: geolocation=(), microphone=(), camera=()`
  - (Optional) `Strict-Transport-Security` behind HTTPS
  - `Content-Security-Policy: default-src 'none'` (adjust if serving UI)

## Observability
- Built-in `/metrics` for Prometheus scraping
  - `http_requests_total{method,path,status}`
  - `http_request_duration_seconds{method,path}`
- Extend: add DB op timers, ingest counters by project/version if needed.

## Data Flow
- DXrevit → `/api/v1/ingest` → PostgreSQL (metadata/objects/relationships)
- Analytics views: `analytics_version_summary`, `analytics_4d_link_data`, `analytics_rag_usage`
- BI: connect with `dx_readonly_role` and DirectQuery these views.

## Common Issues
- DB connect fails: Verify `DATABASE_URL`, ensure PostgreSQL listening on 5432.
- pgvector missing: run `database/extensions/pgvector.sql` with superuser.
- Large ingest timeout: tune `TimeoutSeconds` in DX settings; consider batch size.

## Production Notes
- Run with Gunicorn+Uvicorn workers: 2–4 workers to start.
- Set `ALLOWED_ORIGINS`, `ALLOWED_HOSTS`, enable HSTS behind TLS.
- Backups: schedule `pg_dump` as in `database/README.md`.
