# Run RebuildYOU with Docker Desktop

The project is an ASP.NET Core 10 MVC application; GitHub itself does not run it. `ERR_CONNECTION_REFUSED` at `http://localhost:8080` means no process is accepting connections on port 8080. The Dockerfile and Compose configuration in this repository start the web app and its SQL Server database together.

## Requirements

- Windows with Docker Desktop installed and running (Linux containers enabled).
- Docker Compose v2 (`docker compose`).

## Start it from PowerShell

Open PowerShell in the folder containing `docker-compose.yml` and run:

```powershell
$env:MSSQL_SA_PASSWORD = Read-Host "Enter a strong SQL Server password (8+ characters, uppercase, lowercase, number, symbol)"
$env:ADMIN_SEED_EMAIL = Read-Host "Enter the first administrator email"
$env:ADMIN_SEED_PASSWORD = Read-Host "Enter a strong administrator password"
docker compose up --build -d
```

Wait for the database and app to start, then open <http://localhost:8080>. The app container waits for SQL Server to become healthy before starting. On first startup, the app creates the database schema and seeds the administrator from the environment variables above. Keep these passwords private; do not commit them to the repository.

Check startup status and logs if the page does not load:

```powershell
docker compose ps
docker compose logs --tail 100 web
docker compose logs --tail 100 db
```

Stop the app and database while preserving their data:

```powershell
docker compose down
```

The database, uploaded files, and Data Protection keys are stored in Docker volumes. The Compose app uses Development mode because this local-only setup serves HTTP; production must use HTTPS. To permanently delete the database and uploaded files as well, run `docker compose down --volumes` (this cannot be undone).

## Troubleshooting

- If `docker compose` reports that the password variable is missing, set `$env:MSSQL_SA_PASSWORD` in the same PowerShell window before running Compose.
- If the web container exits, inspect `docker compose logs web`; if SQL Server is still starting, inspect `docker compose logs db` and then run `docker compose up -d` again after it becomes healthy.
- If port 8080 is already in use, change the left side of the `ports` mapping in `docker-compose.yml`, for example `"8081:8080"`, rebuild/start, and browse to `http://localhost:8081`.
- This Compose setup is for local development only and exposes HTTP on localhost. Do not expose it directly to the internet. For a production deployment, run in Production mode behind HTTPS and configure the trusted proxy IPs as described in the README.

For local development without Docker, install the .NET 10 SDK and configure a reachable SQL Server connection string; the local launch profiles use ports 5287 and 7287, not 8080.

## Release-container HTTPS smoke test

The separate `docker-compose.staging.yml` stack runs the Release Docker image in Production mode behind Caddy TLS termination at `https://localhost:8443`. It has its own SQL Server and persistent data volumes. In PowerShell, set `MSSQL_SA_PASSWORD`, `ADMIN_SEED_EMAIL`, and `ADMIN_SEED_PASSWORD`, then run:

```powershell
docker compose -f docker-compose.staging.yml up --build -d
.\scripts\Test-Staging.ps1
```

The smoke test checks HTTPS, the Secure authentication cookie, administrator login and authorization, opportunity publishing, and a user's CV upload/download. Caddy uses a local internal certificate; the script skips certificate trust validation only for localhost. This verifies the Release container and TLS path locally, not a public staging deployment. For external staging, use a trusted certificate and set `ReverseProxy__KnownProxies__0` to the actual immediate proxy address. Stop the local stack with `docker compose -f docker-compose.staging.yml down`; do not use `--volumes` unless its SQL data and uploaded files should be permanently removed.
