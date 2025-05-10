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
$allFiles += "$TargetDir\TextureSwapper.dll", "$TargetDir\TextureSwapper.pdb"

# -- -- --

# Define the source URL and destination path
$sourceUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-full.7z"
$destinationPath = "$ProjectDir\temp\" # Change this if you want a different destination
$releaseVersion = "ffmpeg-7.1.1-full_build"
$archiveName = "$releaseVersion.7z"
$archiveFullPath = Join-Path $destinationPath $archiveName
$binPath = Join-Path $destinationPath "$releaseVersion\bin" # Path inside the archive
$ffmpegExePath = Join-Path $destinationPath "$releaseVersion\bin\ffmpeg.exe"
$ffprobeExePath = Join-Path $destinationPath "$releaseVersion\bin\ffprobe.exe"

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
    $commonLocations = @(
        "C:\Program Files\7-Zip\7z.exe",
        "C:\Program Files (x86)\7-Zip\7z.exe"
    )
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
        & $zipPath x $archiveFullPath -o"$destinationPath" "ffmpeg-7.1.1-full_build\bin\ffmpeg.exe" "ffmpeg-7.1.1-full_build\bin\ffprobe.exe" -y | Out-Null
        Write-Host "Extraction complete."
    } catch {
        Write-Error "Failed to extract files: $($_.Exception.Message)"
        exit # Stop if extraction fails.
    }
}

Write-Host "FFmpeg download process complete."

$allFiles += $ffmpegExePath, $ffprobeExePath

# -- -- -- 

## FFMPegCore Stuff
#$allFiles += "$PublishDir\FFMpegCore.dll", "$PublishDir\Instances.dll", "$PublishDir\Microsoft.Bcl.AsyncInterfaces.dll"
#$allFiles += "$PublishDir\System.Buffers.dll", "$PublishDir\System.IO.Pipelines.dll", "$PublishDir\System.Memory.dll"
#$allFiles += "$PublishDir\System.Numerics.Vectors.dll", "$PublishDir\System.Runtime.CompilerServices.Unsafe.dll"
#$allFiles += "$PublishDir\System.Text.Encodings.Web.dll", "$PublishDir\System.Text.Json.dll", "$PublishDir\System.Threading.Tasks.Extensions.dll"
#
## Magick Stuff
#$allFiles += "$PublishDir\Magick.NET-Q8-x64.dll", "$PublishDir\Magick.NET.Core.dll"
#
## NAudio
#$allFiles += "$PublishDir\NAudio.dll", "$PublishDir\NAudio.Asio.dll", "$PublishDir\NAudio.Core.dll", "$PublishDir\NAudio.Midi.dll"
#$allFiles += "$PublishDir\NAudio.Wasapi.dll", "$PublishDir\NAudio.WinForms.dll", "$PublishDir\NAudio.WinMM.dll"

# Create the compressed archive
$DestinationPath = Join-Path $PublishOutputDir "$AssemblyName-$Version.zip"
Compress-Archive -Path $allFiles -DestinationPath $DestinationPath -Force

Write-Host "Successfully created archive: $DestinationPath"

# Remove the temporary JSON file after archiving (if it was created)
if ($modifiedJsonIncluded -and (Test-Path $tempJsonFilePath)) {
    Remove-Item $tempJsonFilePath -Force

    Write-Host "Removed temp JSON manifest: $tempJsonFilePath"
}