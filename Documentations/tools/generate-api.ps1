param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$UnityEditorPath
)

$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$editorDirectory = Split-Path -Parent ([System.IO.Path]::GetFullPath($UnityEditorPath))
$unityDataPath = @(
    (Join-Path $editorDirectory "Data"),
    ([System.IO.Path]::GetFullPath((Join-Path $editorDirectory "..")))
) | Where-Object {
    Test-Path -LiteralPath (Join-Path $_ "DotNetSdkRoslyn\csc.dll") -PathType Leaf
} | Select-Object -First 1

if (-not $unityDataPath) {
    throw "UnityEditorPath does not point to a Unity Editor installation with the Roslyn compiler."
}

$dotnetPath = @(
    (Join-Path $unityDataPath "NetCoreRuntime\dotnet.exe"),
    (Join-Path $unityDataPath "NetCoreRuntime\dotnet")
) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
$compilerPath = Join-Path $unityDataPath "DotNetSdkRoslyn\csc.dll"

if (-not $dotnetPath) {
    throw "The Unity Editor installation does not contain its bundled .NET runtime."
}
$scriptAssembliesPath = Join-Path $repositoryRoot "Library\ScriptAssemblies"
$documentationAssembliesPath = Join-Path $repositoryRoot "Library\DocumentationAssemblies"
$beeArtifactsPath = Join-Path $repositoryRoot "Library\Bee\artifacts"
$docfxConfigPath = Join-Path $repositoryRoot "Documentations\docfx.json"
$apiPath = Join-Path $repositoryRoot "Documentations\api"

$assemblyNames = @(
    "Ceres",
    "Ceres.Editor",
    "Ceres.ContentPipeline.Editor",
    "Ceres.Graph",
    "Ceres.Graph.Editor",
    "Ceres.Flow",
    "Ceres.Flow.Editor",
    "Ceres.Gameplay",
    "Ceres.Gameplay.Editor",
    "Unity.Ceres.CodeGen"
)

$responseDirectory = Get-ChildItem -LiteralPath $beeArtifactsPath -Directory |
    Sort-Object LastWriteTime -Descending |
    Where-Object {
        $candidate = $_.FullName
        -not ($assemblyNames | Where-Object { -not (Test-Path -LiteralPath (Join-Path $candidate "$_.rsp")) })
    } |
    Select-Object -First 1

if (-not $responseDirectory) {
    throw "Unity compiler response files were not found. Open the project and complete a clean script compilation first."
}

New-Item -ItemType Directory -Force -Path $documentationAssembliesPath | Out-Null

Push-Location $repositoryRoot
try {
    foreach ($assemblyName in $assemblyNames) {
        Write-Host "Generating $assemblyName.xml"
        $responseFile = Join-Path $responseDirectory.FullName "$assemblyName.rsp"
        $outputAssembly = Join-Path $documentationAssembliesPath "$assemblyName.dll"
        $referenceAssembly = Join-Path $documentationAssembliesPath "$assemblyName.ref.dll"
        $documentationFile = Join-Path $scriptAssembliesPath "$assemblyName.xml"

        & $dotnetPath $compilerPath "@$responseFile" "-out:$outputAssembly" "-refout:$referenceAssembly" "-doc:$documentationFile" "-nowarn:1591"
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to generate XML documentation for $assemblyName."
        }
    }

    & docfx metadata $docfxConfigPath
    if ($LASTEXITCODE -ne 0) {
        throw "DocFX metadata generation failed."
    }
}
finally {
    Pop-Location
}

$summaryCount = (Get-ChildItem -LiteralPath $apiPath -Filter "*.yml" -File |
    Select-String -Pattern '^\s*summary:' |
    Measure-Object).Count

if ($summaryCount -eq 0) {
    throw "API metadata contains no XML documentation summaries."
}

Write-Host "Generated API metadata with $summaryCount summary entries."
