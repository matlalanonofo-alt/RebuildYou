# RebuildYOU Training Centre: Website and Employment Management System

ASP.NET Core 10 MVC, Entity Framework Core 10, SQL Server, ASP.NET Core Identity, Bootstrap 5.

## What is included
- Public site: Home, About, Training Programmes (list and detail), Career Categories (each shows only its own opportunities), Opportunities (search, filters, paging), Contact form (saved to the database, honeypot and rate limiting), Privacy notice.
- Accounts: Register (all spec fields saved to SQL Server), Login/Logout, lockout after 5 failed attempts, email-based password recovery, Profile, Change password. Passwords are hashed by Identity.
- User dashboard: profile completion, CV upload/download/delete (PDF/DOCX, 5 MB, magic-byte check, random file names, stored in App_Data outside wwwroot, ownership enforced), save opportunities, apply (duplicates blocked, expired/closed rejected, external-URL opportunities link out), My Applications with status, withdraw.
- Admin dashboard (role "Admin"): live statistics from the database, user search and suspend/reactivate, opportunity create/edit/publish/close/archive, applications (status, interview date, internal notes, CV viewing logged in an audit log), CSV export (no personal data), training programme management (publish/unpublish/archive), categories, contact enquiries.

## v6 update: manager admin area (IMPORTANT: reset the database once)
This version adds new tables and columns. Delete the old database first: View > SQL Server Object Explorer > (localdb)\\MSSQLLocalDB > Databases > RebuildYouDb > right-click > Delete (tick "Close existing connections"). Then press F5; a fresh database is created.
- Create the first administrator using User Secrets or protected environment variables as described below. Do not commit administrator credentials.
- Admin pages: Overview, Users (search, last login, login count), User detail (profile, CVs, applications, courses of interest, login history), Login activity (every register/login/failed login with time and IP), Opportunities, Applications, Programmes, Programme interest, Categories, Enquiries.
- To give the manager her own admin login: she registers normally on the site, then an admin opens Admin > Users and presses "Make admin" next to her name.
- One mobile number or email = one profile (enforced in the database too).

## v4 usability changes
Short 1-minute registration (name, mobile, email, password) with the rest added later in the profile; one profile per person (duplicate email or mobile is blocked with a "log in instead" prompt); CV upload fills empty profile fields (area, education, career interests) on a best-effort basis; "Register my interest" on programmes with an admin list (Admin > Programme interest); closed vacancies hidden by default; a "Next step" card on the dashboard; "Get Work-Ready" and "For Employers" pages; show-password buttons; 48px tap targets; WhatsApp link (confirm the number is WhatsApp-enabled).

## Configure password-recovery email
Password recovery emails use Gmail SMTP by default (`smtp.gmail.com`, port `587`, SSL enabled). For Gmail or Google Workspace, enable 2-Step Verification and create an App Password; use that App Password, not your normal account password. Set your sender/username and App Password in User Secrets from the project directory:

```powershell
dotnet user-secrets set "Email:Username" "your-address@gmail.com"
dotnet user-secrets set "Email:FromEmail" "your-address@gmail.com"
dotnet user-secrets set "Email:Password" "your-google-app-password"
```

Replace the example values locally; never commit or share the App Password. In production, set `Email__Username`, `Email__FromEmail`, and `Email__Password` as protected environment/app settings. If any required SMTP setting is missing, the forgot-password page explains that recovery is unavailable.

## Not included yet (next phase)
Email verification, email/in-app notifications, supporting-document uploads, announcements/FAQ/testimonial CMS, Excel export, extra reports, automated tests, real photographs. See the notes in the chat for details.

## Run in Visual Studio 2022
1. Open `RebuildYou.sln` (File > Open > Project/Solution). Install the ".NET 10 SDK" and "ASP.NET and web development" workload if prompted.
2. Connection string: edit `ConnectionStrings:DefaultConnection` in `appsettings.json`. The default uses SQL Server LocalDB. For a full SQL Server use e.g. `Server=YOURSERVER;Database=RebuildYouDb;User Id=...;Password=...;TrustServerCertificate=True`. Never commit real passwords; use User Secrets (right-click project > Manage User Secrets) or environment variables. Configure SMTP as described in [Configure password-recovery email](#configure-password-recovery-email).
3. First administrator: in User Secrets add
   ```json
   { "AdminSeed": { "Email": "you@yourdomain.co.za", "Password": "A-Strong-Password-1" } }
   ```
   The admin is created on first start. Remove the secret afterwards.
4. Create the database schema from the checked-in EF Core migrations. For a fresh database, run:
   ```
   dotnet ef database update
   ```
   The app also applies pending migrations on startup. Back up an existing database before deploying a migration; see [Database migration, backup and rollback](#database-migration-backup-and-rollback).
5. Press F5. Log in with the admin account and open Admin Dashboard from the profile menu.

## Database migration, backup and rollback

Migrations in `Data/Migrations` are the source of truth for the SQL Server schema. Before each production deployment, take and verify a full database backup (for example, in SSMS: right-click the database > Tasks > Back Up > Full). Also back up `App_Data/cvs`; those uploaded files are stored separately from SQL Server.

Deploy the application and migrations together. Startup applies pending migrations with EF Core `MigrateAsync`; alternatively, apply them deliberately before starting the new version:

```powershell
dotnet ef database update
```

For a rollback, stop the application first. Prefer restoring the pre-deployment database backup and matching `App_Data/cvs` backup, then redeploy the previous application version. A migration can also be reverted to a named earlier migration with `dotnet ef database update <PreviousMigration>`, but down migrations may discard data. Review the migration's `Down` method and rehearse the rollback against a restored copy before using it in production. Never use `dotnet ef database update 0` against a live database.

Databases originally created with `EnsureCreated` have no EF migration history. Do not run the initial migration against such a database as-is: it will try to create tables that already exist. Back up the database and compare its schema with the generated initial migration:

```powershell
dotnet ef migrations script 0 InitialCreate --output initial-schema.sql
```

The safest upgrade is to create a fresh database with the migration, then copy and verify the existing data. Alternatively, if—and only if—the existing schema matches the initial migration exactly, baseline it by recording that migration as already applied. After taking a backup and rehearsing on a restored copy, run this SQL against that database:

```sql
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261126103855_InitialCreate'
)
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261126103855_InitialCreate', N'10.0.15');
```

If the schema differs, do not baseline it; plan a data-preserving migration from its actual schema instead. Do not delete or recreate a database containing user data as a shortcut.

## Seeded content
Career categories and training programme descriptions are seeded on first start. Fictional opportunities are not seeded. Opportunities marked as demonstration data are hidden from public listings and detail pages.

## Before go-live
- Confirm the public email address (the profile PDF shows lereases@gmail.com, the brief shows rebuildyoutc@gmail.com). Edit `Site:Email` in `appsettings.json`.
- Add licensed photographs (Unsplash/Pexels/Wikimedia) to `wwwroot/img` and reference them with alt text.
- Have the Privacy Notice reviewed for POPIA.
- Use HTTPS, set `ASPNETCORE_ENVIRONMENT=Production`, and back up `App_Data` together with the database.
- If TLS terminates at a reverse proxy, set `ReverseProxy__KnownProxies__0` to the actual IP address of the immediate proxy. Add further numbered entries only for trusted proxy addresses. Forwarded headers from other sources are ignored; never trust arbitrary client-supplied forwarding headers.
- Mount `App_Data` on persistent, writable storage. It contains uploaded CVs (`App_Data/cvs`) and Data Protection keys (`App_Data/data-protection-keys`), which must survive restarts and deployments. The key directory can be set explicitly with `DataProtection__KeyDirectory`; in a multi-instance deployment, all instances must share the same protected key store and upload storage.
- Authentication cookies are secure-only outside Development. Configure the proxy to send `X-Forwarded-Proto` so HTTPS redirection and secure cookies work correctly. Database migration or seeding failures now stop startup; inspect host logs and fix the database before routing traffic.

## Publish
Right-click project > Publish > choose Azure App Service, IIS or Folder. Set the connection string and AdminSeed values as environment variables/app settings on the host. Make sure the host account can write to the persistent `App_Data` location. If the deployment platform replaces the application directory during publishing, configure `DataProtection__KeyDirectory` to a persistent path and mount persistent upload storage at `App_Data/cvs`.

All visitors use the database configured on the web host; registrations and course interest are then visible under Admin > Users and Admin > Programme interest. To accept users from other devices, publish the site and configure `DefaultConnection` to a shared, persistent SQL Server database. A `localhost` connection string is only for the machine running the app, not each visitor's device.

## Run with Docker
Docker Desktop with Linux containers and Docker Compose is required. From the `RebuildYou` directory, set a strong SQL Server password in the current PowerShell session, then start the app and database:

```powershell
$env:MSSQL_SA_PASSWORD = Read-Host "Enter a strong SQL Server password"
docker compose up --build -d
```

Open [http://localhost:8080](http://localhost:8080) in a browser. The SQL database and uploaded CVs are stored in Docker volumes. To create an initial administrator, also set `ADMIN_SEED_EMAIL` and `ADMIN_SEED_PASSWORD` in the same session before running Compose; without these, users can register but no administrator is seeded. Keep these values out of source control. Use `docker compose logs -f web` to view app logs and `docker compose down` to stop the services. The named volumes are retained when the services are stopped.

## Integration and Release-container staging checks

The integration test project uses a disposable SQL Server container and exercises migrations, account login/admin authorization, publishing an opportunity, and private CV upload/download:

```powershell
dotnet test tests\RebuildYou.Web.IntegrationTests\RebuildYou.Web.IntegrationTests.csproj --configuration Release --filter "Category=Integration"
```

Docker Desktop must be running. To exercise the actual Release image behind a local HTTPS reverse proxy, set `MSSQL_SA_PASSWORD`, `ADMIN_SEED_EMAIL`, and `ADMIN_SEED_PASSWORD` in the same PowerShell session, then run:

```powershell
docker compose -f docker-compose.staging.yml up --build -d
.\scripts\Test-Staging.ps1
```

This local staging stack runs the app in Production mode and terminates HTTPS with Caddy. Its internal self-signed certificate is accepted by the smoke script only for localhost; it is not suitable for public staging or production. The stack uses separate persistent Docker volumes from the local development Compose setup. Stop it with `docker compose -f docker-compose.staging.yml down`; add `--volumes` only when intentionally deleting its database and uploaded files.
