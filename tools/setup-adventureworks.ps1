# Setup AdventureWorks Database for TipsyBaboon TestSite
# Downloads and installs AdventureWorksLT (lightweight version) for SQL Server

param(
    [string]$ServerInstance = "localhost",
    [string]$DatabaseName = "AdventureWorksLT"
)

$ErrorActionPreference = "Stop"

Write-Host "Setting up AdventureWorks database..." -ForegroundColor Cyan

# Check if database already exists
$checkDbQuery = @"
SELECT COUNT(*) as DbExists 
FROM sys.databases 
WHERE name = '$DatabaseName'
"@

try {
    $result = Invoke-Sqlcmd -ServerInstance $ServerInstance -Query $checkDbQuery -TrustServerCertificate
    if ($result.DbExists -gt 0) {
        Write-Host "Database '$DatabaseName' already exists on $ServerInstance" -ForegroundColor Yellow
        $response = Read-Host "Drop and recreate? (y/N)"
        if ($response -ne 'y' -and $response -ne 'Y') {
            Write-Host "Setup cancelled." -ForegroundColor Gray
            exit 0
        }
        Invoke-Sqlcmd -ServerInstance $ServerInstance -Query "DROP DATABASE [$DatabaseName]" -TrustServerCertificate
        Write-Host "Dropped existing database." -ForegroundColor Green
    }
} catch {
    Write-Host "Error checking for existing database: $_" -ForegroundColor Red
    exit 1
}

# Download AdventureWorksLT script
$scriptUrl = "https://github.com/Microsoft/sql-server-samples/releases/download/adventureworks/AdventureWorksLT2022.bak"
$tempPath = Join-Path $env:TEMP "AdventureWorksLT2022.bak"

Write-Host "Downloading AdventureWorksLT backup from GitHub..." -ForegroundColor Cyan
try {
    Invoke-WebRequest -Uri $scriptUrl -OutFile $tempPath -UseBasicParsing
    Write-Host "Download complete: $tempPath" -ForegroundColor Green
} catch {
    Write-Host "Failed to download backup file: $_" -ForegroundColor Red
    Write-Host "Alternative: Download manually from https://github.com/Microsoft/sql-server-samples/releases" -ForegroundColor Yellow
    exit 1
}

# Get SQL Server default data path
$dataPathQuery = "SELECT SERVERPROPERTY('InstanceDefaultDataPath') AS DefaultDataPath"
try {
    $dataPathResult = Invoke-Sqlcmd -ServerInstance $ServerInstance -Query $dataPathQuery -TrustServerCertificate
    $dataPath = $dataPathResult.DefaultDataPath
    if ([string]::IsNullOrEmpty($dataPath)) {
        $dataPath = "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\"
        Write-Host "Using default path: $dataPath" -ForegroundColor Yellow
    }
} catch {
    Write-Host "Could not determine SQL Server data path, using default." -ForegroundColor Yellow
    $dataPath = "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\"
}

# Restore database
$mdfPath = Join-Path $dataPath "$DatabaseName.mdf"
$ldfPath = Join-Path $dataPath "${DatabaseName}_log.ldf"

$restoreQuery = @"
RESTORE DATABASE [$DatabaseName]
FROM DISK = N'$tempPath'
WITH 
    MOVE 'AdventureWorksLT2022_Data' TO N'$mdfPath',
    MOVE 'AdventureWorksLT2022_Log' TO N'$ldfPath',
    REPLACE,
    STATS = 10
"@

Write-Host "Restoring database to $ServerInstance..." -ForegroundColor Cyan
try {
    Invoke-Sqlcmd -ServerInstance $ServerInstance -Query $restoreQuery -TrustServerCertificate -QueryTimeout 300
    Write-Host "Database restored successfully!" -ForegroundColor Green
} catch {
    Write-Host "Failed to restore database: $_" -ForegroundColor Red
    Write-Host "You may need to run this script as Administrator if SQL Server does not have write access to the data directory." -ForegroundColor Yellow
    exit 1
}

# Verify tables
$verifyQuery = @"
SELECT 
    s.name AS SchemaName,
    t.name AS TableName
FROM [$DatabaseName].sys.tables t
INNER JOIN [$DatabaseName].sys.schemas s ON t.schema_id = s.schema_id
ORDER BY s.name, t.name
"@

Write-Host "`nVerifying database tables..." -ForegroundColor Cyan
try {
    $tables = Invoke-Sqlcmd -ServerInstance $ServerInstance -Query $verifyQuery -TrustServerCertificate
    Write-Host "Found $($tables.Count) tables:" -ForegroundColor Green
    $tables | ForEach-Object { Write-Host "  $($_.SchemaName).$($_.TableName)" -ForegroundColor Gray }
} catch {
    Write-Host "Could not verify tables: $_" -ForegroundColor Yellow
}

# Cleanup
try {
    Remove-Item $tempPath -Force
    Write-Host "`nSetup complete! Temporary files cleaned up." -ForegroundColor Green
} catch {
    Write-Host "`nSetup complete! (Could not delete temp file: $tempPath)" -ForegroundColor Yellow
}

Write-Host "`nConnection string: Server=$ServerInstance;Database=$DatabaseName;Trusted_Connection=True;TrustServerCertificate=True;" -ForegroundColor Cyan
