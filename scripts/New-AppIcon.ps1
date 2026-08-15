[CmdletBinding()]
param(
    [string]$IcoPath,
    [string]$PreviewPath
)

# Original Roster Companion artwork. The PNG and ICO are generated entirely
# from the geometric drawing operations in this file, with no external assets.

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($IcoPath)) {
    $IcoPath = Join-Path $repositoryRoot 'src\ChatGPTRoster\Assets\ChatGPTRoster.ico'
}
if ([string]::IsNullOrWhiteSpace($PreviewPath)) {
    $PreviewPath = Join-Path $repositoryRoot 'docs\images\app-icon.png'
}

$IcoPath = [System.IO.Path]::GetFullPath($IcoPath)
$PreviewPath = [System.IO.Path]::GetFullPath($PreviewPath)
if (-not $IcoPath.StartsWith($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $PreviewPath.StartsWith($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Icon outputs must remain inside the repository.'
}

Add-Type -AssemblyName System.Drawing

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

$bitmap = [System.Drawing.Bitmap]::new(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

$navy = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 18, 33, 51))
$surface = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
$muted = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 173, 184, 199))
$green = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 18, 163, 109))

try {
    $background = New-RoundedRectanglePath -X 8 -Y 8 -Width 240 -Height 240 -Radius 48
    $graphics.FillPath($navy, $background)
    $background.Dispose()

    $panel = New-RoundedRectanglePath -X 42 -Y 43 -Width 172 -Height 170 -Radius 22
    $graphics.FillPath($surface, $panel)
    $panel.Dispose()

    foreach ($rowY in @(72, 116, 160)) {
        $graphics.FillEllipse($green, 63, $rowY, 18, 18)
        $nameBar = New-RoundedRectanglePath -X 94 -Y ($rowY + 1) -Width 78 -Height 8 -Radius 4
        $detailBar = New-RoundedRectanglePath -X 94 -Y ($rowY + 14) -Width 52 -Height 6 -Radius 3
        $graphics.FillPath($navy, $nameBar)
        $graphics.FillPath($muted, $detailBar)
        $nameBar.Dispose()
        $detailBar.Dispose()
    }

    $graphics.FillEllipse($green, 170, 165, 30, 30)
    $plusPen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 4)
    try {
        $plusPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $plusPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $graphics.DrawLine($plusPen, 177, 180, 193, 180)
        $graphics.DrawLine($plusPen, 185, 172, 185, 188)
    }
    finally {
        $plusPen.Dispose()
    }

    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($PreviewPath)) | Out-Null
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($IcoPath)) | Out-Null
    $bitmap.Save($PreviewPath, [System.Drawing.Imaging.ImageFormat]::Png)

    $memory = [System.IO.MemoryStream]::new()
    try {
        $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytes = $memory.ToArray()
        $iconBytes = [System.IO.MemoryStream]::new()
        $writer = [System.IO.BinaryWriter]::new($iconBytes)
        try {
            $writer.Write([uint16]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]1)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$pngBytes.Length)
            $writer.Write([uint32]22)
            $writer.Write($pngBytes)
            $writer.Flush()
            [System.IO.File]::WriteAllBytes($IcoPath, $iconBytes.ToArray())
        }
        finally {
            $writer.Dispose()
            $iconBytes.Dispose()
        }
    }
    finally {
        $memory.Dispose()
    }
}
finally {
    $navy.Dispose()
    $surface.Dispose()
    $muted.Dispose()
    $green.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Host "Created $IcoPath"
Write-Host "Created $PreviewPath"
