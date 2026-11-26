param(
    [string]$BaseUrl = "https://localhost:8443"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($env:ADMIN_SEED_EMAIL) -or [string]::IsNullOrWhiteSpace($env:ADMIN_SEED_PASSWORD)) {
    throw "Set ADMIN_SEED_EMAIL and ADMIN_SEED_PASSWORD to the staging administrator credentials before running this test."
}

$baseUri = [Uri]$BaseUrl
if ($baseUri.Scheme -ne "https") {
    throw "The staging smoke test requires an HTTPS URL."
}
$allowLocalCertificate = $baseUri.Host -in @("localhost", "127.0.0.1", "::1")
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$handler.CookieContainer = [System.Net.CookieContainer]::new()
if ($allowLocalCertificate) {
    $handler.ServerCertificateCustomValidationCallback = [System.Net.Http.HttpClientHandler]::DangerousAcceptAnyServerCertificateValidator
}
$client = [System.Net.Http.HttpClient]::new($handler)
$client.BaseAddress = $baseUri

function Get-AntiforgeryToken([string]$path) {
    $response = $client.GetAsync($path).GetAwaiter().GetResult()
    $null = $response.EnsureSuccessStatusCode()
    $html = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $match = [Regex]::Match($html, '<input[^>]*name="__RequestVerificationToken"[^>]*value="([^"]+)"', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) {
        throw "Could not find an antiforgery token on $path."
    }
    return [Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
}

function New-FormContent([string]$token, [hashtable]$values) {
    $values["__RequestVerificationToken"] = $token
    $encoded = foreach ($key in $values.Keys) {
        [Uri]::EscapeDataString([string]$key) + "=" + [Uri]::EscapeDataString([string]$values[$key])
    }
    return [System.Net.Http.StringContent]::new(($encoded -join "&"), [Text.Encoding]::UTF8, "application/x-www-form-urlencoded")
}

function Assert-Redirect($response, [string]$scenario) {
    if ($response.StatusCode -ne [Net.HttpStatusCode]::Redirect) {
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        throw "$scenario failed: expected a redirect, got $([int]$response.StatusCode). $body"
    }
}

try {
    $homeResponse = $null
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $homeResponse = $client.GetAsync("/").GetAwaiter().GetResult()
            if ($homeResponse.IsSuccessStatusCode) {
                break
            }
        }
        catch [System.Net.Http.HttpRequestException] {
            Start-Sleep -Seconds 3
        }
        Start-Sleep -Seconds 3
    }
    if ($null -eq $homeResponse) {
        throw "The HTTPS endpoint did not become available."
    }
    $null = $homeResponse.EnsureSuccessStatusCode()
    if ($homeResponse.RequestMessage.RequestUri.Scheme -ne "https") {
        throw "The Release container was not reached over HTTPS."
    }

    $token = Get-AntiforgeryToken "/Account/Login"
    $login = $client.PostAsync("/Account/Login", (New-FormContent $token @{
        Email = $env:ADMIN_SEED_EMAIL
        Password = $env:ADMIN_SEED_PASSWORD
        RememberMe = "false"
    })).GetAwaiter().GetResult()
    Assert-Redirect $login "Administrator login"

    $authCookie = $login.Headers.GetValues("Set-Cookie") | Where-Object { $_ -match "\.AspNetCore\.Identity\.Application=" } | Select-Object -First 1
    if (-not $authCookie -or $authCookie -notmatch "(?i);\s*secure(?:;|$)") {
        throw "The Production authentication cookie was not marked Secure."
    }

    $adminPage = $client.GetAsync("/Admin/Index").GetAwaiter().GetResult()
    $null = $adminPage.EnsureSuccessStatusCode()

    $token = Get-AntiforgeryToken "/Admin/OpportunityForm"
    $title = "Staging smoke " + [Guid]::NewGuid().ToString("N")
    $create = $client.PostAsync("/Admin/OpportunityForm", (New-FormContent $token @{
        Id = "0"
        Title = $title
        EmployerName = "Staging Smoke Test"
        CareerCategoryId = "1"
        Location = "Johannesburg"
        Type = "Permanent"
        Deadline = [DateTime]::Today.AddDays(30).ToString("yyyy-MM-dd")
        Status = "Draft"
        ShortDescription = "Automated Release-container staging verification."
        IsDemo = "false"
    })).GetAwaiter().GetResult()
    Assert-Redirect $create "Opportunity creation"

    $opportunities = $client.GetAsync("/Admin/Opportunities").GetAwaiter().GetResult()
    $null = $opportunities.EnsureSuccessStatusCode()
    $opportunityHtml = $opportunities.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $titlePosition = $opportunityHtml.IndexOf($title, [StringComparison]::Ordinal)
    if ($titlePosition -lt 0) {
        throw "The created staging opportunity was not found."
    }
    $rowStart = $opportunityHtml.LastIndexOf("<tr", $titlePosition, [StringComparison]::Ordinal)
    $rowEnd = $opportunityHtml.IndexOf("</tr>", $titlePosition, [StringComparison]::Ordinal)
    $row = if ($rowStart -ge 0 -and $rowEnd -gt $rowStart) { $opportunityHtml.Substring($rowStart, $rowEnd - $rowStart) } else { "" }
    $idMatch = [Regex]::Match($row, 'name="id"\s+value="(\d+)"')
    if (-not $idMatch.Success) {
        throw "Could not locate the created opportunity ID in the admin listing."
    }
    $opportunityId = $idMatch.Groups[1].Value

    $token = Get-AntiforgeryToken "/Admin/Opportunities"
    $publish = $client.PostAsync("/Admin/SetOpportunityStatus", (New-FormContent $token @{
        id = $opportunityId
        status = "Published"
    })).GetAwaiter().GetResult()
    Assert-Redirect $publish "Opportunity publishing"

    $details = $client.GetAsync("/Opportunities/Details/$opportunityId").GetAwaiter().GetResult()
    $null = $details.EnsureSuccessStatusCode()
    if ($details.RequestMessage.RequestUri.Scheme -ne "https" -or
        -not $details.Content.ReadAsStringAsync().GetAwaiter().GetResult().Contains($title)) {
        throw "The published opportunity was not publicly reachable over HTTPS."
    }

    $randomPhone = Get-Random -Minimum 10000000 -Maximum 99999999
    $userEmail = "staging-$([Guid]::NewGuid().ToString('N'))@rebuildyou.test"
    $userPassword = "Staging_User_2026!"
    $token = Get-AntiforgeryToken "/Account/Register"
    $register = $client.PostAsync("/Account/Register", (New-FormContent $token @{
        FirstName = "Staging"
        Surname = "Test"
        Email = $userEmail
        Mobile = "+2782$randomPhone"
        Password = $userPassword
        AcceptPrivacy = "true"
    })).GetAwaiter().GetResult()
    Assert-Redirect $register "User registration/login"

    $denied = $client.GetAsync("/Admin/Index").GetAwaiter().GetResult()
    if ($denied.StatusCode -ne [Net.HttpStatusCode]::Redirect -or
        $denied.Headers.Location.ToString() -notmatch "/Account/AccessDenied") {
        throw "A regular user was not denied access to the admin area."
    }

    $token = Get-AntiforgeryToken "/Dashboard/Cv"
    $pdf = [Text.Encoding]::ASCII.GetBytes("%PDF-1.4`nstaging-smoke-cv`n%%EOF")
    $multipart = [System.Net.Http.MultipartFormDataContent]::new()
    $multipart.Add([System.Net.Http.StringContent]::new($token), "__RequestVerificationToken")
    $fileContent = [System.Net.Http.ByteArrayContent]::new($pdf)
    $fileContent.Headers.ContentType = [Net.Http.Headers.MediaTypeHeaderValue]::new("application/pdf")
    $multipart.Add($fileContent, "file", "staging-smoke.pdf")
    $upload = $client.PostAsync("/Dashboard/UploadCv", $multipart).GetAwaiter().GetResult()
    Assert-Redirect $upload "CV upload"

    $cvPage = $client.GetAsync("/Dashboard/Cv").GetAwaiter().GetResult()
    $null = $cvPage.EnsureSuccessStatusCode()
    $cvHtml = $cvPage.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $cvMatch = [Regex]::Match($cvHtml, 'DownloadCv(?:/|\?id=)(\d+)')
    if (-not $cvMatch.Success) {
        throw "The uploaded CV was not listed for its owner."
    }
    $download = $client.GetAsync("/Dashboard/DownloadCv?id=$($cvMatch.Groups[1].Value)").GetAwaiter().GetResult()
    $null = $download.EnsureSuccessStatusCode()
    $downloadedPdf = $download.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
    if ([Convert]::ToBase64String($downloadedPdf) -ne [Convert]::ToBase64String($pdf)) {
        throw "The downloaded CV did not match the uploaded file."
    }

    Write-Output "PASS: Release container served HTTPS, secure cookies, admin/user authorization, opportunity publishing, and private CV upload/download."
}
finally {
    $client.Dispose()
    $handler.Dispose()
}
