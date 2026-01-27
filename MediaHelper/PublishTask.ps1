param(
    [string]$PublishOutputDir,
    [string]$ProjectDir,
    [string]$LibsDir,
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

$programFiles = @()
$assetsFiles = @()

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

# -- -- --

$releaseVersion = "ffmpeg-7.1.1-essentials_build"

# Define the source URL and destination path
$sourceUrl = "http://www.gyan.dev/ffmpeg/builds/packages/$releaseVersion.7z"
$destinationPath = "$ProjectDir\temp\" # Change this if you want a different destination

$archiveFullPath = Join-Path $destinationPath "$releaseVersion.7z"

$binPath = Join-Path $destinationPath "$releaseVersion\bin" # Path inside the archive

$ffmpegExePath = Join-Path $binPath "ffmpeg.exe"
$ffprobeExePath = Join-Path $binPath "ffprobe.exe"

# Create the destination directory if it doesn't exist
if (-not (Test-Path -Path $destinationPath -PathType 'Container')) {
    Write-Verbose "Creating destination directory: $destinationPath" -Verbose
    New-Item -Path $destinationPath -ItemType 'Directory' -Force | Out-Null
}

# Check if the archive exists.
if (-not (Test-Path -Path $archiveFullPath -PathType 'Leaf')) {
    # Download the 7z archive
    Write-Host "Downloading FFmpeg from: $sourceUrl to $archiveFullPath"
    try {
        Invoke-WebRequest -Uri $sourceUrl -OutFile $archiveFullPath -UseBasicParsing
        Write-Host "Download complete."
    } catch {
        Write-Error "Failed to download FFmpeg: $($_.Exception.Message)"
        exit  # Stop execution if download fails
    }
} else {
    Write-Host "Archive file already exists: $archiveFullPath"
}

# Check if 7-Zip is installed
$zipPath = Get-Command "7z" -ErrorAction SilentlyContinue
if (!$zipPath) {
    # 7-Zip not found in PATH, check common locations
    $commonLocations = @("C:\Program Files\7-Zip\7z.exe", "C:\Program Files (x86)\7-Zip\7z.exe")
    foreach ($location in $commonLocations) {
        if (Test-Path -Path $location -PathType 'Leaf') {
            $zipPath = $location
            Write-Host "7-Zip found at: $location"
            break # Exit the loop if found
        }
    }
    if (!$zipPath) {
        Write-Error "7-Zip is not installed or not found in common locations. Please install 7-Zip and ensure it's in your PATH."
        exit  # Stop if 7-Zip is not found
    }
}

# Check for existing ffmpeg.exe and ffprobe.exe
if ((Test-Path -Path $ffmpegExePath -PathType 'Leaf') -and (Test-Path -Path $ffprobeExePath -PathType 'Leaf')) {
    Write-Host "ffmpeg.exe and ffprobe.exe already exist in $binPath. Skipping extraction."
} else {
    # Extract the files if they don't exist
    Write-Host "Extracting ffmpeg.exe and ffprobe.exe from the archive."
    try {
        # Use 7-Zip to extract only the necessary files
        & $zipPath x $archiveFullPath -o"$destinationPath" "$releaseVersion\bin\ffmpeg.exe" "$releaseVersion\bin\ffprobe.exe" -y | Out-Null
        Write-Host "Extraction complete."
    } catch {
        Write-Error "Failed to extract files: $($_.Exception.Message)"
        exit # Stop if extraction fails.
    }
}

Write-Host "FFmpeg download process complete."

# -- -- --

# Get all files from the assets directory recursively
$assetsFiles = Get-ChildItem -Path $AssetsDir -Recurse -File | Where-Object {$_.Name -ne "manifest.json"} | Select-Object -ExpandProperty FullName

# Add the TextureSwapper.dll and TextureSwapper.pdb from PublishOutputDir
$programFiles += "$BuildOutputDir\$AssemblyName.dll", "$BuildOutputDir\$AssemblyName.pdb"


$programFiles += $ffmpegExePath, $ffprobeExePath

## FFMPegCore Stuff
$programFiles += "$BuildOutputDir\FFMpegCore.dll", "$BuildOutputDir\Instances.dll", "$BuildOutputDir\Microsoft.Bcl.AsyncInterfaces.dll"
$programFiles += "$BuildOutputDir\System.Buffers.dll", "$BuildOutputDir\System.IO.Pipelines.dll", "$BuildOutputDir\System.Memory.dll"
$programFiles += "$BuildOutputDir\System.Numerics.Vectors.dll", "$BuildOutputDir\System.Runtime.CompilerServices.Unsafe.dll"
$programFiles += "$BuildOutputDir\System.Text.Encodings.Web.dll", "$BuildOutputDir\System.Text.Json.dll", "$BuildOutputDir\System.Threading.Tasks.Extensions.dll"

# Magick Stuff
$programFiles += "$BuildOutputDir\Magick.NET-Q8-x64.dll", "$BuildOutputDir\Magick.NET.Core.dll", "$BuildOutputDir\Magick.Native-Q8-x64.dll"

# NAudio
$programFiles += "$BuildOutputDir\NAudio.dll", "$BuildOutputDir\NAudio.Asio.dll", "$BuildOutputDir\NAudio.Core.dll", "$BuildOutputDir\NAudio.Midi.dll"
$programFiles += "$BuildOutputDir\NAudio.Wasapi.dll", "$BuildOutputDir\NAudio.WinForms.dll", "$BuildOutputDir\NAudio.WinMM.dll"

# -- -- -- Copy collected files to temp dir 

# Copy assets files to the temp staging directory
foreach ($assetPath in $assetsFiles) { CopyTo -rootDir $AssetsDir -outputDir $TempZipDir -filePath $assetPath }

# Copy program files to plugins folder 
foreach ($programFile in $programFiles) { Copy-Item -Path $programFile -Destination "$TempZipDir\plugins"  }

# -- -- --

$OutputZip = Join-Path $PublishOutputDir "$AssemblyName-$Version.zip"

Compress-Archive -Path "$TempZipDir\*" -DestinationPath $OutputZip -Force

Remove-Item $TempZipDir -Recurse -Force # Delete contents of temp zip dir

Write-Host "Successfully created archive: $OutputZip"