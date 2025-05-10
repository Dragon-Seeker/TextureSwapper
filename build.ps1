# Set the project path
$ProjectPath = "TextureSwapper.csproj"

# Build the project targeting .NET Standard 2.1
dotnet build $ProjectPath -p:TargetFramework=netstandard2.1

# Check for build success
if ($LASTEXITCODE -eq 0) {
    Write-Host "Build completed successfully."
} else {
    Write-Host "Build failed."
}