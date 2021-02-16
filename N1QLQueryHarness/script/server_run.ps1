Param(
    [Parameter(Mandatory=$true)]
    [string]$sha
)

Push-Location $PSScriptRoot\..\

& 'C:\Program Files\dotnet\dotnet.exe' build -c Release
Push-Location bin\Release\net5.0
if(Test-Path -Path "data" -PathType Container) {
    cd "data"
    & 'C:\Program Files\Git\bin\git.exe' pull origin master
    cd ".."
} else {
    & 'C:\Program Files\Git\bin\git.exe' clone --filter=blob:none --sparse https://github.com/couchbase/query data
    cd "data"
    & 'C:\Program Files\Git\bin\git.exe' sparse-checkout init --cone
    & 'C:\Program Files\Git\bin\git.exe' sparse-checkout add test/filestore/test_cases
    cd ".."
}

Pop-Location
Write-Host "Downloading LiteCore (SHA $sha)..."
& 'C:\Program Files\dotnet\dotnet.exe' run -c Release -- prepare --sha $sha
if($LASTEXITCODE -ne 0) {
    Write-Error "Prepare stage failed..."
    exit $LASTEXITCODE
}

Write-Host "Converting server query data..."
& 'C:\Program Files\dotnet\dotnet.exe' run -c Release -- migrate
if($LASTEXITCODE -ne 0) {
    Write-Error "Migrate stage failed..."
    exit $LASTEXITCODE
}

Write-Host "Running queries..."
& 'C:\Program Files\dotnet\dotnet.exe' run -c Release -- run -o -j results.json
if($LASTEXITCODE -lt 0) {
    Write-Error "Run stage failed..."
    exit $LASTEXITCODE
} elseif($LASTEXITCODE -gt 0) {
    Write-Error "`r`n$LASTEXITCODE queries did not pass!"
    exit $LASTEXITCODE
}