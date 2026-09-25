# Generates the 16 host voice clips via ElevenLabs and writes them to
# Assets/_Smartest/Audio/Voice/<key>.mp3
#
# Usage (do not write the key into this file):
#   $env:ELEVENLABS_API_KEY = "..."
#   $env:ELEVENLABS_VOICE_ID = "..."
#   powershell -File Tools\GenerateHostVoice.ps1

$ErrorActionPreference = "Stop"

$apiKey = $env:ELEVENLABS_API_KEY
$voiceId = $env:ELEVENLABS_VOICE_ID
$model = if ($env:ELEVENLABS_MODEL) { $env:ELEVENLABS_MODEL } else { "eleven_multilingual_v2" }

if ([string]::IsNullOrWhiteSpace($apiKey)) { throw "Set ELEVENLABS_API_KEY" }
if ([string]::IsNullOrWhiteSpace($voiceId)) { throw "Set ELEVENLABS_VOICE_ID" }

$root = Split-Path -Parent $PSScriptRoot
$linesPath = Join-Path $PSScriptRoot "voice-lines.json"
$outDir = Join-Path $root "Assets\_Smartest\Audio\Voice"
$tmpDir = Join-Path $env:TEMP "smartest-voice"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null

$lines = Get-Content -Raw -Path $linesPath | ConvertFrom-Json
$uri = "https://api.elevenlabs.io/v1/text-to-speech/$voiceId`?output_format=mp3_44100_128"
$ok = 0
$fail = 0

foreach ($line in $lines) {
    $outFile = Join-Path $outDir ($line.key + ".mp3")
    $bodyFile = Join-Path $tmpDir ($line.key + ".json")
    $errFile = Join-Path $tmpDir ($line.key + ".err")

    $payload = [ordered]@{
        text = [string]$line.text
        model_id = $model
        voice_settings = [ordered]@{
            stability = 0.52
            similarity_boost = 0.8
            style = 0.2
            use_speaker_boost = $true
        }
    }
    $json = $payload | ConvertTo-Json -Compress -Depth 5
    [System.IO.File]::WriteAllText($bodyFile, $json, [System.Text.UTF8Encoding]::new($false))

    Write-Host "Generating $($line.key) ..."
    $code = 0
    & curl.exe -sS -o $outFile -w "%{http_code}" -X POST $uri `
        -H "xi-api-key: $apiKey" `
        -H "Content-Type: application/json" `
        -H "Accept: audio/mpeg" `
        --data-binary "@$bodyFile" | Tee-Object -Variable status | Out-Null
    $code = [int]$status

    $isMp3 = $false
    if (Test-Path $outFile) {
        $fs = [System.IO.File]::OpenRead($outFile)
        try {
            $b0 = $fs.ReadByte(); $b1 = $fs.ReadByte()
            $isMp3 = ($b0 -eq 0x49 -and $b1 -eq 0x44) -or ($b0 -eq 0xFF -and ($b1 -band 0xE0) -eq 0xE0) # ID3 or MPEG frame
        } finally { $fs.Close() }
    }

    if ($code -ge 200 -and $code -lt 300 -and $isMp3) {
        Write-Host "  wrote $outFile ($((Get-Item $outFile).Length) bytes)"
        $ok++
    } else {
        $fail++
        $err = if (Test-Path $outFile) { Get-Content -Raw -Path $outFile } else { "(no body)" }
        Write-Host "  FAILED HTTP $code : $err"
        if (Test-Path $outFile) { Remove-Item -Force $outFile }
        [System.IO.File]::WriteAllText($errFile, $err)
    }

    Start-Sleep -Milliseconds 350
}

Write-Host ""
Write-Host "Done. $ok generated, $fail failed. Path: $outDir"
if ($fail -gt 0) { exit 1 }
