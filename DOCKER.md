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

The database and uploaded files are stored in Docker volumes. To permanently delete the database and uploaded files as well, run `docker compose down --volumes` (this cannot be undone).

## Troubleshooting

- If `docker compose` reports that the password variable is missing, set `$env:MSSQL_SA_PASSWORD` in the same PowerShell window before running Compose.
- If the web container exits, inspect `docker compose logs web`; if SQL Server is still starting, inspect `docker compose logs db` and then run `docker compose up -d` again after it becomes healthy.
- If port 8080 is already in use, change the left side of the `ports` mapping in `docker-compose.yml`, for example `"8081:8080"`, rebuild/start, and browse to `http://localhost:8081`.
- The Docker setup runs the app in Production mode. It does not expose the development-only demo vacancies or the development seed account.

For local development without Docker, install the .NET 10 SDK and configure a reachable SQL Server connection string; the local launch profiles use ports 5287 and 7287, not 8080.
