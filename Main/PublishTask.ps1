param(
    [string]$PublishOutputDir,
    [string]$ProjectDir,
    [string]$TargetDir,
    [string]$AssemblyName,
    [string]$Version
)

$AssetsDir = $ProjectDir + "assets"

Write-Host "--- Parameters and Values ---"
foreach ($param in $PSBoundParameters.Keys) {
    Write-Host "$param=$($PSBoundParameters[$param])"
}

$allFiles = @()

# Define the name of your JSON file
$jsonFileName = "manifest.json" # Replace with the actual name of your JSON file
$jsonFilePath = Join-Path $AssetsDir $jsonFileName
$tempJsonFilePath = Join-Path $PublishOutputDir "$jsonFileName"
$modifiedJsonIncluded = $false

# Check if the JSON file exists and process it
if (Test-Path $jsonFilePath) {
    try {
        # Read the content of the JSON file
        $jsonContent = Get-Content $jsonFilePath -Raw

        # Replace the placeholder "$(version)" with the $Version parameter
        $modifiedJsonContent = $jsonContent -replace '\$\(version\)', $Version

        # Write the modified content to a temporary JSON file
        $modifiedJsonContent | Out-File $tempJsonFilePath -Encoding UTF8

        # Add the temporary JSON file to the list of files to be zipped
        $allFiles += $tempJsonFilePath
        $modifiedJsonIncluded = $true
    } catch {
        Write-Error "An error occurred while processing the JSON file: $_"
    }
} else {
    Write-Warning "JSON file '$jsonFileName' not found in '$AssetsDir'."
}

# Create the publish output directory if it doesn't exist
New-Item -ItemType Directory -Path $PublishOutputDir -Force

# Get all files from the assets directory recursively
$assetsFiles = Get-ChildItem -Path $AssetsDir -Recurse -File | Where-Object {$_.Name -ne "manifest.json"} | Select-Object -ExpandProperty FullName

# Adds the asset files
$allFiles += $assetsFiles

# Add the TextureSwapper.dll and TextureSwapper.pdb from PublishOutputDir
$allFiles += "$TargetDir\$AssemblyName.dll", "$TargetDir\$AssemblyName.pdb"

# -- -- -- 

#$depdenciesFolder = "$TargetDir\publish"
$depdenciesFolder = "$TargetDir"

## Endec Stuff
$allFiles += "$depdenciesFolder\Endec.dll", "$depdenciesFolder\Endec.Json.dll"

$allFiles += "$depdenciesFolder\StandardSocketsHttpHandler.dll"

# Create the compressed archive
$DestinationPath = Join-Path $PublishOutputDir "$AssemblyName-$Version.zip"
Compress-Archive -Path $allFiles -DestinationPath $DestinationPath -Force

Write-Host "Successfully created archive: $DestinationPath"

# Remove the temporary JSON file after archiving (if it was created)
if ($modifiedJsonIncluded -and (Test-Path $tempJsonFilePath)) {
    Remove-Item $tempJsonFilePath -Force

    Write-Host "Removed temp JSON manifest: $tempJsonFilePath"
}