using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RebuildYou.Web.Data;
using RebuildYou.Web.Models;
using Testcontainers.MsSql;

namespace RebuildYou.Web.IntegrationTests;

[CollectionDefinition("application integration", DisableParallelization = true)]
public sealed class ApplicationIntegrationCollection : ICollectionFixture<ApplicationIntegrationFixture>;

public sealed class ApplicationIntegrationFixture : IAsyncLifetime
{
    public const string AdminEmail = "integration.admin@rebuildyou.test";
    public const string AdminPassword = "Integration_Admin_2026!";

    readonly string contentRoot = Path.Combine(Path.GetTempPath(), "RebuildYou.IntegrationTests", Guid.NewGuid().ToString("N"));
    readonly string databaseName = "RebuildYouTests_" + Guid.NewGuid().ToString("N");
    readonly MsSqlContainer sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").WithPassword(AdminPassword).Build();

    public IntegrationWebApplicationFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(contentRoot);
        await sqlServer.StartAsync();

        var masterConnectionString = new SqlConnectionStringBuilder(sqlServer.GetConnectionString())
        {
            InitialCatalog = "master"
        };
        await using (var connection = new SqlConnection(masterConnectionString.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
        }

        var testConnectionString = new SqlConnectionStringBuilder(sqlServer.GetConnectionString())
        {
            InitialCatalog = databaseName
        }.ConnectionString;

        Factory = new IntegrationWebApplicationFactory(testConnectionString, contentRoot);
        using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();
        await sqlServer.DisposeAsync();
        if (Directory.Exists(contentRoot))
            Directory.Delete(contentRoot, recursive: true);
    }

    public async Task<ApplicationUser> CreateUserAsync(string? email = null)
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userEmail = email ?? $"user-{Guid.NewGuid():N}@rebuildyou.test";
        var user = new ApplicationUser
        {
            UserName = userEmail,
            Email = userEmail,
            EmailConfirmed = true,
            FirstName = "Integration",
            Surname = "User",
            AcceptedPrivacy = true
        };
        var created = await users.CreateAsync(user, "Integration_User_2026!");
        if (!created.Succeeded)
            throw new InvalidOperationException(string.Join("; ", created.Errors.Select(error => error.Description)));
        var addedToRole = await users.AddToRoleAsync(user, "User");
        if (!addedToRole.Succeeded)
            throw new InvalidOperationException(string.Join("; ", addedToRole.Errors.Select(error => error.Description)));
        return user;
    }
}

public sealed class IntegrationWebApplicationFactory(string connectionString, string contentRoot) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(contentRoot);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["AdminSeed:Email"] = ApplicationIntegrationFixture.AdminEmail,
                ["AdminSeed:Password"] = ApplicationIntegrationFixture.AdminPassword,
                ["DataProtection:KeyDirectory"] = Path.Combine(contentRoot, "data-protection-keys")
            });
        });
    }
}

[Collection("application integration")]
public sealed class ApplicationIntegrationTests(ApplicationIntegrationFixture fixture)
{
    const string UserPassword = "Integration_User_2026!";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Startup_applies_initial_migration_and_leaves_no_pending_migrations()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Contains("20261126103855_InitialCreate", await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Login_redirects_users_to_dashboard_and_limits_admin_pages_to_admins()
    {
        var user = await fixture.CreateUserAsync();
        using var memberClient = CreateClient();

        var anonymousResponse = await memberClient.GetAsync("/Admin/Index");
        Assert.Equal(HttpStatusCode.Redirect, anonymousResponse.StatusCode);
        Assert.Contains("/Account/Login", anonymousResponse.Headers.Location!.ToString());

        using var memberLogin = await SignInAsync(memberClient, user.Email!, UserPassword);
        Assert.Equal(HttpStatusCode.Redirect, memberLogin.StatusCode);
        Assert.Contains("/Dashboard", memberLogin.Headers.Location!.ToString());
        var forbiddenResponse = await memberClient.GetAsync("/Admin/Index");
        Assert.Equal(HttpStatusCode.Redirect, forbiddenResponse.StatusCode);
        Assert.Contains("/Account/AccessDenied", forbiddenResponse.Headers.Location!.ToString());

        using var adminClient = CreateClient();
        using var adminLogin = await SignInAsync(adminClient, ApplicationIntegrationFixture.AdminEmail, ApplicationIntegrationFixture.AdminPassword);
        Assert.Equal(HttpStatusCode.Redirect, adminLogin.StatusCode);
        Assert.Contains("/Admin", adminLogin.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/Admin/Index")).StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Admin_can_publish_an_opportunity_and_it_becomes_public()
    {
        using var client = CreateClient();
        using var loginResponse = await SignInAsync(client, ApplicationIntegrationFixture.AdminEmail, ApplicationIntegrationFixture.AdminPassword);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var token = await GetAntiforgeryTokenAsync(client, "/Admin/OpportunityForm");
        var title = "Integration Opportunity " + Guid.NewGuid().ToString("N");
        using var createResponse = await client.PostAsync("/Admin/OpportunityForm", Form(
            ("__RequestVerificationToken", token),
            ("Id", "0"),
            ("Title", title),
            ("EmployerName", "Integration Employer"),
            ("CareerCategoryId", "1"),
            ("Location", "Johannesburg"),
            ("Type", "Permanent"),
            ("Deadline", DateTime.Today.AddDays(30).ToString("yyyy-MM-dd")),
            ("Status", "Draft"),
            ("ShortDescription", "Created by the application integration test."),
            ("IsDemo", "false")));
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        int opportunityId;
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            opportunityId = await db.Opportunities.Where(item => item.Title == title).Select(item => item.Id).SingleAsync();
        }

        var draftListing = await client.GetStringAsync("/Opportunities/Index");
        Assert.DoesNotContain(title, draftListing, StringComparison.Ordinal);

        using var publishResponse = await client.PostAsync("/Admin/SetOpportunityStatus", Form(
            ("__RequestVerificationToken", token),
            ("id", opportunityId.ToString()),
            ("status", "Published")));
        Assert.Equal(HttpStatusCode.Redirect, publishResponse.StatusCode);

        using var detailsResponse = await client.GetAsync($"/Opportunities/Details/{opportunityId}");
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains(title, await detailsResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task User_can_upload_and_download_their_cv_but_another_user_cannot()
    {
        var owner = await fixture.CreateUserAsync();
        var otherUser = await fixture.CreateUserAsync();
        using var ownerClient = CreateClient();
        using var ownerLogin = await SignInAsync(ownerClient, owner.Email!, UserPassword);
        Assert.Equal(HttpStatusCode.Redirect, ownerLogin.StatusCode);

        var token = await GetAntiforgeryTokenAsync(ownerClient, "/Dashboard/Cv");
        var pdf = "%PDF-1.4\nintegration-test-cv\n%%EOF"u8.ToArray();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(token), "__RequestVerificationToken");
        using var file = new ByteArrayContent(pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "integration-cv.pdf");

        using var uploadResponse = await ownerClient.PostAsync("/Dashboard/UploadCv", form);
        Assert.Equal(HttpStatusCode.Redirect, uploadResponse.StatusCode);

        CvDocument cv;
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            cv = await db.CvDocuments.SingleAsync(item => item.UserId == owner.Id);
            var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            var storedPath = Path.Combine(environment.ContentRootPath, "App_Data", "cvs", cv.StoredFileName);
            Assert.True(File.Exists(storedPath));
        }

        using var downloadResponse = await ownerClient.GetAsync($"/Dashboard/DownloadCv?id={cv.Id}");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal("application/pdf", downloadResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(pdf, await downloadResponse.Content.ReadAsByteArrayAsync());

        using var otherClient = CreateClient();
        using var otherLogin = await SignInAsync(otherClient, otherUser.Email!, UserPassword);
        Assert.Equal(HttpStatusCode.Redirect, otherLogin.StatusCode);
        using var deniedDownload = await otherClient.GetAsync($"/Dashboard/DownloadCv?id={cv.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deniedDownload.StatusCode);
    }

    HttpClient CreateClient() => fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string email, string password)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");
        return await client.PostAsync("/Account/Login", Form(
            ("__RequestVerificationToken", token),
            ("Email", email),
            ("Password", password),
            ("RememberMe", "false")));
    }

    static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Antiforgery token was not found on {path}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    static FormUrlEncodedContent Form(params (string Name, string Value)[] values) =>
        new(values.Select(value => new KeyValuePair<string, string>(value.Name, value.Value)));
}
