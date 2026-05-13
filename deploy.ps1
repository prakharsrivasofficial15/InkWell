<#
.SYNOPSIS
    Deploys all InkWell backend microservices to Azure App Service.
.DESCRIPTION
    Creates App Service Plan, Web Apps, configures managed identity and Key Vault access,
    then publishes and deploys each service.
.NOTES
    Prerequisites: Azure CLI logged in, .NET 8 SDK installed.
    Run from the InkWell solution root directory.
#>

param(
    [string]$ResourceGroup = "inkwell-rg",
    [string]$Location = "centralindia",
    [string]$PlanName = "inkwell-asp",
    [string]$PlanSku = "B1",
    [string]$KeyVaultName = "inkwell-kv2",
    [string]$FrontendUrl = "http://localhost:4200",
    [switch]$SkipInfra,
    [switch]$SkipDeploy
)

$ErrorActionPreference = "Stop"

# Service definitions
$services = @(
    @{ Name = "inkwell-auth-api";         Project = "InkWellAuth.API"         },
    @{ Name = "inkwell-post-api";         Project = "InkWellPost.API"         },
    @{ Name = "inkwell-category-api";     Project = "InkWellCategory.API"     },
    @{ Name = "inkwell-comment-api";      Project = "InkWellComment.API"      },
    @{ Name = "inkwell-media-api";        Project = "InkWellMedia.API"        },
    @{ Name = "inkwell-newsletter-api";   Project = "InkWellNewsletter.API"   },
    @{ Name = "inkwell-notification-api"; Project = "InkWellNotification.API" }
)

$publishRoot = Join-Path $PSScriptRoot "publish"

# Helper: Run az command, suppressing stderr warnings
function Invoke-Az {
    param([string]$Arguments)
    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = "cmd.exe"
    $pinfo.Arguments = "/c az $Arguments"
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError = $true
    $pinfo.UseShellExecute = $false
    $pinfo.CreateNoWindow = $true
    $p = New-Object System.Diagnostics.Process
    $p.StartInfo = $pinfo
    $p.Start() | Out-Null
    $stdout = $p.StandardOutput.ReadToEnd()
    $stderr = $p.StandardError.ReadToEnd()
    $p.WaitForExit()
    if ($p.ExitCode -ne 0) {
        $errorMsg = if ($stderr) { $stderr } else { $stdout }
        Write-Host "  ERROR: $errorMsg" -ForegroundColor Red
        throw "az command failed with exit code $($p.ExitCode)"
    }
    return $stdout
}

# ==========================================================================
# PHASE 1: Infrastructure
# ==========================================================================
if (-not $SkipInfra) {
    Write-Host ""
    Write-Host "===========================================================" -ForegroundColor Cyan
    Write-Host "  PHASE 1: Creating Infrastructure" -ForegroundColor Cyan
    Write-Host "===========================================================" -ForegroundColor Cyan

    # 1a: App Service Plan
    Write-Host ""
    Write-Host "[1/4] Creating App Service Plan: $PlanName ($PlanSku, Windows)..." -ForegroundColor Yellow
    Invoke-Az "appservice plan create --name $PlanName --resource-group $ResourceGroup --location $Location --sku $PlanSku -o none"
    Write-Host "  Done - App Service Plan created." -ForegroundColor Green

    # 1b: Create Web Apps
    Write-Host ""
    Write-Host "[2/4] Creating Web Apps..." -ForegroundColor Yellow
    foreach ($svc in $services) {
        $appName = $svc.Name
        Write-Host "  Creating: $appName..." -NoNewline
        Invoke-Az "webapp create --name $appName --resource-group $ResourceGroup --plan $PlanName --runtime dotnet:8 -o none"
        Write-Host " Done" -ForegroundColor Green
    }

    # 1c: Enable Managed Identity
    Write-Host ""
    Write-Host "[3/4] Enabling System-Assigned Managed Identity..." -ForegroundColor Yellow
    foreach ($svc in $services) {
        $appName = $svc.Name
        Write-Host "  Enabling MI for: $appName..." -NoNewline
        Invoke-Az "webapp identity assign --name $appName --resource-group $ResourceGroup -o none"
        Write-Host " Done" -ForegroundColor Green
    }

    # 1d: Grant Key Vault Access (RBAC mode)
    Write-Host ""
    Write-Host "[4/4] Granting Key Vault access to each Web App (RBAC)..." -ForegroundColor Yellow

    # Get Key Vault resource ID
    $kvResourceId = (Invoke-Az "keyvault show --name $KeyVaultName --query id -o tsv").Trim()

    foreach ($svc in $services) {
        $appName = $svc.Name
        Write-Host "  Granting KV access for: $appName..." -NoNewline

        # Get the principal ID of the managed identity
        $principalId = (Invoke-Az "webapp identity show --name $appName --resource-group $ResourceGroup --query principalId -o tsv").Trim()

        if ([string]::IsNullOrEmpty($principalId)) {
            Write-Host " WARNING: Could not get principal ID, skipping..." -ForegroundColor Yellow
            continue
        }

        # Assign 'Key Vault Secrets User' role (RBAC)
        try {
            Invoke-Az "role assignment create --assignee-object-id $principalId --assignee-principal-type ServicePrincipal --role 'Key Vault Secrets User' --scope $kvResourceId -o none"
            Write-Host " Done" -ForegroundColor Green
        } catch {
            # Role assignment may already exist
            Write-Host " Done (may already exist)" -ForegroundColor Green
        }
    }

    # Configure App Settings
    Write-Host ""
    Write-Host "[Bonus] Configuring App Settings..." -ForegroundColor Yellow
    foreach ($svc in $services) {
        $appName = $svc.Name
        Write-Host "  Configuring: $appName..." -NoNewline

        $settingsStr = "KeyVaultUri=https://$KeyVaultName.vault.azure.net/ JwtSettings__Issuer=InkWell JwtSettings__Audience=InkWellUsers JwtSettings__ExpiryMinutes=60 JwtSettings__RefreshTokenExpiryDays=7 ASPNETCORE_ENVIRONMENT=Production AllowedCorsOrigins=$FrontendUrl"

        Invoke-Az "webapp config appsettings set --name $appName --resource-group $ResourceGroup --settings $settingsStr -o none"
        Write-Host " Done" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "PHASE 1 COMPLETE - Infrastructure setup done!" -ForegroundColor Green
}

# ==========================================================================
# PHASE 2: Build and Deploy
# ==========================================================================
if (-not $SkipDeploy) {
    Write-Host ""
    Write-Host "===========================================================" -ForegroundColor Cyan
    Write-Host "  PHASE 2: Build and Deploy Services" -ForegroundColor Cyan
    Write-Host "===========================================================" -ForegroundColor Cyan

    # Clean publish directory
    if (Test-Path $publishRoot) {
        Remove-Item $publishRoot -Recurse -Force
    }

    foreach ($svc in $services) {
        $appName    = $svc.Name
        $projectDir = $svc.Project
        $projectPath = Join-Path $PSScriptRoot $projectDir
        $outputDir   = Join-Path $publishRoot $appName
        $zipPath     = Join-Path $publishRoot "$appName.zip"

        Write-Host ""
        Write-Host "--------------------------------------------" -ForegroundColor DarkGray
        Write-Host "  Deploying: $appName ($projectDir)" -ForegroundColor Yellow
        Write-Host "--------------------------------------------" -ForegroundColor DarkGray

        # Build
        Write-Host "  [1/3] Publishing $projectDir..." -ForegroundColor White
        dotnet publish $projectPath -c Release -o $outputDir --nologo -v quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Error "dotnet publish failed for $projectDir"
            continue
        }
        Write-Host "    Build successful" -ForegroundColor Green

        # Create zip
        Write-Host "  [2/3] Creating deployment package..." -ForegroundColor White
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        Compress-Archive -Path (Join-Path $outputDir "*") -DestinationPath $zipPath -Force
        $zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
        Write-Host "    Package created ($zipSize MB)" -ForegroundColor Green

        # Deploy
        Write-Host "  [3/3] Deploying to Azure..." -ForegroundColor White
        Invoke-Az "webapp deploy --name $appName --resource-group $ResourceGroup --src-path `"$zipPath`" --type zip -o none"
        Write-Host "    Deployed successfully!" -ForegroundColor Green
        Write-Host "    URL: https://$appName.azurewebsites.net" -ForegroundColor Cyan
    }

    # Cleanup
    Write-Host ""
    Write-Host "  Cleaning up publish artifacts..." -ForegroundColor DarkGray
    if (Test-Path $publishRoot) {
        Remove-Item $publishRoot -Recurse -Force
    }

    Write-Host ""
    Write-Host "===========================================================" -ForegroundColor Green
    Write-Host "  ALL SERVICES DEPLOYED SUCCESSFULLY!" -ForegroundColor Green
    Write-Host "===========================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Service URLs:" -ForegroundColor White
    foreach ($svc in $services) {
        Write-Host "    - https://$($svc.Name).azurewebsites.net/swagger" -ForegroundColor Cyan
    }
    Write-Host ""
}
