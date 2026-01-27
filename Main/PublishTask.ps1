param(
    [string]$PublishOutputDir,
    [string]$ProjectDir,
    [string]$BuildOutputDir,
    [string]$AssemblyName,
    [string]$Version
)

function CopyTo { param ([string]$rootDir, [string]$outputDir, [string]$filePath)
    # Calculate relative path by removing the BaseDir from the Full Path and make path in zip dir
    $resolvedRootDir = [Regex]::Escape((Resolve-Path $rootDir))
    $relativePath = ($filePath -replace $resolvedRootDir, "").TrimStart("\")
    
    # Define and create the target subfolder in Staging
    $targetPath = Join-Path $outputDir $relativePath
    $targetDir = Split-Path $targetPath

    # Write-Host "$targetPath"
    
    if (!(Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }
    
    # Copy the file to its new home in the staging tree
    Copy-Item -Path $filePath -Destination $targetPath
}

#Write-Host "--- Parameters and Values ---"
#foreach ($param in $PSBoundParameters.Keys) { Write-Host "$param=$($PSBoundParameters[$param])" }

# -- -- -- Setup

$AssetsDir = $ProjectDir + "assets"

# Create the publish output directory if it doesn't exist
New-Item -ItemType Directory -Path $PublishOutputDir -Force

$TempZipDir = "$PublishOutputDir\ZipStaging"

if (Test-Path $TempZipDir) { Remove-Item $TempZipDir -Recurse -Force }
New-Item -ItemType Directory -Path $TempZipDir

$programFiles = @() #  All DLL and related files
$assetsFiles = @()  #  All asset files related to mod info, icon, images or other data related files

# -- -- -- Create formatted manifest file

# Define the name of your JSON file
$fileName = "manifest.json"
$rawJsonManifest = Join-Path $AssetsDir $fileName
$formattedJsonManifest = Join-Path $TempZipDir $fileName

if (Test-Path $rawJsonManifest) {
    try {
        # Read the content of the JSON file
        $jsonContent = Get-Content $rawJsonManifest -Raw

        # Replace the placeholder "$(version)" with the $Version parameter
        $modifiedJsonContent = $jsonContent -replace '\$\(version\)', $Version

        # Write the modified content to a temporary JSON file
        $modifiedJsonContent | Out-File $formattedJsonManifest -Encoding UTF8
    } catch {
        Write-Error "An error occurred while processing the JSON file: $_"
    }
} else {
    Write-Warning "JSON file '$jsonFileName' not found in '$AssetsDir'."
}

# -- -- -- Collect asset and program files

# Add all files from the assets directory recursively
$assetsFiles += Get-ChildItem -Path $AssetsDir -Recurse -File | Where-Object {$_.Name -ne "manifest.json"} | Select-Object -ExpandProperty FullName

# Add the TextureSwapper.dll and TextureSwapper.pdb from PublishOutputDir
$programFiles += "$BuildOutputDir\$AssemblyName.dll", "$BuildOutputDir\$AssemblyName.pdb"

## Endec Stuff
$programFiles += "$BuildOutputDir\Endec.dll", "$BuildOutputDir\Endec.Json.dll"

$programFiles += "$BuildOutputDir\StandardSocketsHttpHandler.dll"

# -- -- -- Copy collected files to temp dir 

# Copy assets files to the temp staging directory
foreach ($filePath in $assetsFiles) { CopyTo -rootDir $AssetsDir -outputDir $TempZipDir -filePath $filePath }

# Copy program files to plugins folder 
foreach ($programFile in $programFiles) { Copy-Item -Path $programFile -Destination "$TempZipDir\plugins"  }

# -- -- --

$OutputZip = Join-Path $PublishOutputDir "$AssemblyName-$Version.zip"

Compress-Archive -Path "$TempZipDir\*" -DestinationPath $OutputZip -Force

Remove-Item $TempZipDir -Recurse -Force # Delete contents of temp zip dir

Write-Host "Successfully created archive: $OutputZip"