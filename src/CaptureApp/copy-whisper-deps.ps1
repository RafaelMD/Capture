# Script to copy Whisper folder to output directory
param(
    [string]$TargetDir
)

# Remove trailing quote if present
$TargetDir = $TargetDir.TrimEnd('"')

$sourceWhisperDir = Join-Path $PSScriptRoot "whisper"
$targetWhisperDir = Join-Path $TargetDir "whisper"

# Check if source whisper folder exists
if (-not (Test-Path $sourceWhisperDir)) {
    Write-Host "Warning: Source whisper folder not found at $sourceWhisperDir"
    exit 0
}

# Check if target whisper folder already exists and is up to date
if (Test-Path $targetWhisperDir) {
    $sourceTime = (Get-Item $sourceWhisperDir).LastWriteTime
    $targetTime = (Get-Item $targetWhisperDir).LastWriteTime
    
    if ($targetTime -ge $sourceTime) {
        Write-Host "Whisper folder is already up to date in output directory."
        exit 0
    }
    
    Write-Host "Removing outdated whisper folder from output directory..."
    Remove-Item -Path $targetWhisperDir -Recurse -Force
}

Write-Host "Copying whisper folder to output directory..."
Copy-Item -Path $sourceWhisperDir -Destination $TargetDir -Recurse -Force
Write-Host "Whisper folder copied successfully!"
