# RebuildYOU Training Centre: Website and Employment Management System

ASP.NET Core 8 MVC, Entity Framework Core, SQL Server, ASP.NET Core Identity, Bootstrap 5.

## What is included
- Public site: Home, About, Training Programmes (list and detail), Career Categories (each shows only its own opportunities), Opportunities (search, filters, paging), Contact form (saved to the database, honeypot and rate limiting), Privacy notice.
- Accounts: Register (all spec fields saved to SQL Server), Login/Logout, lockout after 5 failed attempts, email-based password recovery, Profile, Change password. Passwords are hashed by Identity.
- User dashboard: profile completion, CV upload/download/delete (PDF/DOCX, 5 MB, magic-byte check, random file names, stored in App_Data outside wwwroot, ownership enforced), save opportunities, apply (duplicates blocked, expired/closed rejected, external-URL opportunities link out), My Applications with status, withdraw.
- Admin dashboard (role "Admin"): live statistics from the database, user search and suspend/reactivate, opportunity create/edit/publish/close/archive, applications (status, interview date, internal notes, CV viewing logged in an audit log), CSV export (no personal data), training programme management (publish/unpublish/archive), categories, contact enquiries.

## v6 update: manager admin area (IMPORTANT: reset the database once)
This version adds new tables and columns. Delete the old database first: View > SQL Server Object Explorer > (localdb)\\MSSQLLocalDB > Databases > RebuildYouDb > right-click > Delete (tick "Close existing connections"). Then press F5; a fresh database is created.
- In Development, an administrator is created for you: **manager@rebuildyou.local / Manager@2026!** (file `appsettings.Development.json`). Change it before going live.
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
1. Open `RebuildYou.sln` (File > Open > Project/Solution). Install the ".NET 8 SDK" and "ASP.NET and web development" workload if prompted.
2. Connection string: edit `ConnectionStrings:DefaultConnection` in `appsettings.json`. The default uses SQL Server LocalDB. For a full SQL Server use e.g. `Server=YOURSERVER;Database=RebuildYouDb;User Id=...;Password=...;TrustServerCertificate=True`. Never commit real passwords; use User Secrets (right-click project > Manage User Secrets) or environment variables. Configure SMTP as described in [Configure password-recovery email](#configure-password-recovery-email).
3. First administrator: in User Secrets add
   ```json
   { "AdminSeed": { "Email": "you@yourdomain.co.za", "Password": "A-Strong-Password-1" } }
   ```
   The admin is created on first start. Remove the secret afterwards.
4. Create the database. Quick start: just press F5; the app creates the tables automatically. Recommended (proper migrations): open Package Manager Console and run
   ```
   Add-Migration InitialCreate
   Update-Database
   ```
   When a migration exists the app applies migrations on startup instead.
5. Press F5. Log in with the admin account and open Admin Dashboard from the profile menu.

## Demo data
In Development only, four fictional opportunities titled "[DEMO] ..." are added. Delete them in Admin > Opportunities before go-live. Training programmes are seeded with descriptions only; fees, durations and accreditation are blank until RebuildYOU confirms them.

## Before go-live
- Confirm the public email address (the profile PDF shows lereases@gmail.com, the brief shows rebuildyoutc@gmail.com). Edit `Site:Email` in `appsettings.json`.
- Add licensed photographs (Unsplash/Pexels/Wikimedia) to `wwwroot/img` and reference them with alt text.
- Have the Privacy Notice reviewed for POPIA.
- Use HTTPS, set `ASPNETCORE_ENVIRONMENT=Production`, back up `App_Data/cvs` together with the database.

## Publish
Right-click project > Publish > choose Azure App Service, IIS or Folder. Set the connection string and AdminSeed values as environment variables/app settings on the host. Make sure the host account can write to `App_Data/cvs`.

## Run with Docker
Docker Desktop with Linux containers and Docker Compose is required. From the `RebuildYou` directory, set a strong SQL Server password in the current PowerShell session, then start the app and database:

```powershell
$env:MSSQL_SA_PASSWORD = Read-Host "Enter a strong SQL Server password"
docker compose up --build -d
```

Open [http://localhost:8080](http://localhost:8080) in a browser. The SQL database and uploaded CVs are stored in Docker volumes. To create an initial administrator, also set `ADMIN_SEED_EMAIL` and `ADMIN_SEED_PASSWORD` in the same session before running Compose; without these, users can register but no administrator is seeded. Keep these values out of source control. Use `docker compose logs -f web` to view app logs and `docker compose down` to stop the services. The named volumes are retained when the services are stopped.
