[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$infraProject = "src/GymChatAI.Infrastructure/GymChatAI.Infrastructure.csproj"
$apiProject = "src/GymChatAI.Api/GymChatAI.Api.csproj"
$migrationsFolder = "src/GymChatAI.Infrastructure/Migrations"

function Get-DescriptiveMigrationName {
    param($tempMigrationClassName)

    $file = Get-ChildItem -Path $migrationsFolder -Filter "*_$tempMigrationClassName.cs" | Select-Object -First 1
    if (-not $file) { return "Update_$(Get-Date -Format 'yyyyMMddHHmmss')" }

    $content = Get-Content -Path $file.FullName -Raw

    $newTables = [regex]::Matches($content, 'CreateTable\(\s*name:\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
    $droppedTables = [regex]::Matches($content, 'DropTable\(\s*name:\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
    $addedColumns = [regex]::Matches($content, 'AddColumn<[^>]+>\(\s*name:\s*"([^"]+)",\s*table:\s*"([^"]+)"') |
        ForEach-Object { "$($_.Groups[2].Value)_$($_.Groups[1].Value)" }
    $droppedColumns = [regex]::Matches($content, 'DropColumn\(\s*name:\s*"([^"]+)",\s*table:\s*"([^"]+)"') |
        ForEach-Object { "$($_.Groups[2].Value)_$($_.Groups[1].Value)" }
    $alteredColumns = [regex]::Matches($content, 'AlterColumn<[^>]+>\(\s*name:\s*"([^"]+)",\s*table:\s*"([^"]+)"') |
        ForEach-Object { "$($_.Groups[2].Value)_$($_.Groups[1].Value)" }

    $parts = @()
    if ($newTables)      { $parts += "Add_" + ($newTables -join "_") }
    if ($droppedTables)  { $parts += "Remove_" + ($droppedTables -join "_") }
    if ($addedColumns)   { $parts += "AddCol_" + ($addedColumns -join "_") }
    if ($droppedColumns) { $parts += "RemoveCol_" + ($droppedColumns -join "_") }
    if ($alteredColumns) { $parts += "AlterCol_" + ($alteredColumns -join "_") }

    if (-not $parts) { return "Update_$(Get-Date -Format 'yyyyMMddHHmmss')" }

    $name = ($parts -join "_") -replace '[^a-zA-Z0-9_]', ''
    if ($name.Length -gt 100) { $name = $name.Substring(0, 100) }
    return $name
}

Write-Host "A verificar se há alterações de modelo por migrar..." -ForegroundColor Cyan

$output = dotnet ef migrations has-pending-model-changes --project $infraProject --startup-project $apiProject 2>&1
$exitCode = $LASTEXITCODE
Write-Host $output

if ($exitCode -ne 0) {
    Write-Host ""
    Write-Host "⚠️  Há alterações no modelo - a analisar para sugerir um nome..." -ForegroundColor Yellow

    $tempName = "TempPendingCheck_$(Get-Date -Format 'yyyyMMddHHmmss')"
    dotnet ef migrations add $tempName --project $infraProject --startup-project $apiProject | Out-Null

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Falha ao gerar a migração temporária." -ForegroundColor Red
        exit 1
    }

    $suggestedName = Get-DescriptiveMigrationName -tempMigrationClassName $tempName
    dotnet ef migrations remove --project $infraProject --startup-project $apiProject --force | Out-Null

    Write-Host ""
    Write-Host "Nome sugerido: " -NoNewline -ForegroundColor Cyan
    Write-Host $suggestedName -ForegroundColor White
    $finalName = Read-Host "Prime Enter para aceitar, ou escreve um nome diferente"

    if ([string]::IsNullOrWhiteSpace($finalName)) {
        $finalName = $suggestedName
    }

    Write-Host "A gerar migração '$finalName'..." -ForegroundColor Cyan
    dotnet ef migrations add $finalName --project $infraProject --startup-project $apiProject

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Falha ao gerar a migração." -ForegroundColor Red
        exit 1
    }

    Write-Host "Migração '$finalName' gerada com sucesso." -ForegroundColor Green
}

Write-Host ""
Write-Host "A arrancar a app..." -ForegroundColor Cyan
dotnet run --project $apiProject