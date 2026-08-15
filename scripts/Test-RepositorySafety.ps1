[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$excludedDirectories = @('bin', 'obj', 'artifacts', '.git', '.vs', 'TestResults')
$textExtensions = @(
    '.cs', '.xaml', '.csproj', '.sln', '.md', '.ps1', '.yml', '.yaml',
    '.json', '.props', '.targets', '.gitignore', '.gitattributes'
)

$files = Get-ChildItem -LiteralPath $repositoryRoot -Recurse -File -Force | Where-Object {
    $relative = $_.FullName.Substring($repositoryRoot.Length).TrimStart('\', '/')
    $segments = $relative -split '[\\/]'
    -not ($segments | Where-Object { $excludedDirectories -contains $_ }) -and
    ($textExtensions -contains $_.Extension -or $_.Name -in @('.gitignore', '.gitattributes'))
}

$findings = [System.Collections.Generic.List[string]]::new()
foreach ($file in $files) {
    $relative = $file.FullName.Substring($repositoryRoot.Length).TrimStart('\', '/')
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
        $lineNumber++

        if ($line -match '(?i)[A-Z]:\\Users\\[^\\\s]+' -or $line -match '(?i)/(?:Users|home)/[^/\s]+') {
            $findings.Add("$relative`:$lineNumber contains an absolute user profile path")
        }
        if ($line -match '(?i)-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----') {
            $findings.Add("$relative`:$lineNumber contains a private-key marker")
        }
        if ($line -match '(?i)\b(?:sk|sess)-[A-Za-z0-9_-]{16,}\b') {
            $findings.Add("$relative`:$lineNumber contains a token-like value")
        }

        foreach ($emailMatch in [regex]::Matches($line, '(?i)\b[A-Z0-9._%+-]+@([A-Z0-9.-]+\.[A-Z]{2,})\b')) {
            $emailHost = $emailMatch.Groups[1].Value.ToLowerInvariant()
            if ($emailHost -notin @('example.com', 'example.org', 'example.net')) {
                $findings.Add("$relative`:$lineNumber contains a non-example email address")
            }
        }
    }
}

if ($findings.Count -gt 0) {
    $findings | ForEach-Object { Write-Host $_ }
    throw "Repository safety scan found $($findings.Count) potential secret or personal-data issue(s)."
}

Write-Host "Repository safety scan passed across $($files.Count) text files."
