param(
    [string]$OutputDirectory = (Join-Path (Join-Path $PSScriptRoot '..') 'src\EasyLog.App\Assets')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

function New-RoundedRectanglePath {
    param(
        [System.Drawing.RectangleF]$Rectangle,
        [float]$Radius
    )
    $diameter = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($Rectangle.X, $Rectangle.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rectangle.Right - $diameter, $Rectangle.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rectangle.Right - $diameter, $Rectangle.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rectangle.X, $Rectangle.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Draw-AppIcon {
    param([int]$Size)

    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $s = $Size / 256.0

        # ============================================================
        # BACKGROUND — Vibrant purple gradient rounded square
        # ============================================================
        $margin = [math]::Max(1, 6 * $s)
        $outerRect = [System.Drawing.RectangleF]::new($margin, $margin, $Size - 2 * $margin, $Size - 2 * $margin)
        $cornerRadius = [math]::Max(2, 44 * $s)
        $outerPath = New-RoundedRectanglePath -Rectangle $outerRect -Radius $cornerRadius
        try {
            $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
                [System.Drawing.PointF]::new(0, 0),
                [System.Drawing.PointF]::new($Size, $Size),
                [System.Drawing.Color]::FromArgb(255, 88, 28, 135),
                [System.Drawing.Color]::FromArgb(255, 45, 10, 90))
            try { $graphics.FillPath($bgBrush, $outerPath) } finally { $bgBrush.Dispose() }
        } finally { $outerPath.Dispose() }

        # ============================================================
        # LOG EXHAUST TRAILS (streaming behind rocket)
        # ============================================================
        $trailColors = @(
            [System.Drawing.Color]::FromArgb(160, 52, 211, 153),
            [System.Drawing.Color]::FromArgb(120, 110, 231, 183),
            [System.Drawing.Color]::FromArgb(100, 167, 243, 208),
            [System.Drawing.Color]::FromArgb(80, 52, 211, 153),
            [System.Drawing.Color]::FromArgb(60, 110, 231, 183)
        )
        $trailWidths = @(72, 56, 84, 44, 64)
        $trailPenW = [math]::Max(1, 4 * $s)
        for ($i = 0; $i -lt 5; $i++) {
            $tPen = New-Object System.Drawing.Pen($trailColors[$i], $trailPenW)
            $tPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
            $tPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
            try {
                $ty = (110 + $i * 18) * $s
                $txEnd = (60 + $trailWidths[$i]) * $s
                $graphics.DrawLine($tPen, (20 * $s), $ty, $txEnd, $ty)
            } finally { $tPen.Dispose() }
        }

        # ============================================================
        # ROCKET BODY (tilted, flying upper-right)
        # ============================================================
        # Save state and rotate for tilted rocket
        $saved = $graphics.Transform.Clone()
        $graphics.TranslateTransform((148 * $s), (108 * $s))
        $graphics.RotateTransform(-40)

        # Main body (elongated rounded capsule)
        $rBodyBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 240, 240, 250))
        try {
            $rbW = 52 * $s
            $rbH = 100 * $s
            $rbRect = [System.Drawing.RectangleF]::new(-$rbW / 2, -$rbH / 2, $rbW, $rbH)
            $rbPath = New-RoundedRectanglePath -Rectangle $rbRect -Radius ([math]::Max(2, 24 * $s))
            try { $graphics.FillPath($rBodyBrush, $rbPath) } finally { $rbPath.Dispose() }
        } finally { $rBodyBrush.Dispose() }

        # Nose cone (triangle at top)
        $noseBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 253, 100, 80))
        try {
            $noseH = 30 * $s
            $noseW = 36 * $s
            $nosePoints = [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new(0, (-52 * $s - $noseH)),
                [System.Drawing.PointF]::new(-$noseW / 2, -48 * $s),
                [System.Drawing.PointF]::new($noseW / 2, -48 * $s)
            )
            $graphics.FillPolygon($noseBrush, $nosePoints)
        } finally { $noseBrush.Dispose() }

        # Window (porthole on rocket body)
        $windowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 100, 200, 255))
        try {
            $winSz = 22 * $s
            $graphics.FillEllipse($windowBrush, -$winSz / 2, (-16 * $s), $winSz, $winSz)
            # Window glint
            $wGlint = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(150, 255, 255, 255))
            try {
                $gSz = 8 * $s
                $graphics.FillEllipse($wGlint, (-6 * $s), (-12 * $s), $gSz, $gSz)
            } finally { $wGlint.Dispose() }
        } finally { $windowBrush.Dispose() }

        # Window rim
        $rimPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 180, 180, 200), [math]::Max(1, 2.5 * $s))
        try {
            $graphics.DrawEllipse($rimPen, -$winSz / 2, (-16 * $s), $winSz, $winSz)
        } finally { $rimPen.Dispose() }

        # Fins (left and right)
        $finBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 253, 100, 80))
        try {
            # Left fin
            $lfPoints = [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new(-24 * $s, 30 * $s),
                [System.Drawing.PointF]::new(-42 * $s, 54 * $s),
                [System.Drawing.PointF]::new(-20 * $s, 48 * $s)
            )
            $graphics.FillPolygon($finBrush, $lfPoints)
            # Right fin
            $rfPoints = [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new(24 * $s, 30 * $s),
                [System.Drawing.PointF]::new(42 * $s, 54 * $s),
                [System.Drawing.PointF]::new(20 * $s, 48 * $s)
            )
            $graphics.FillPolygon($finBrush, $rfPoints)
        } finally { $finBrush.Dispose() }

        # Exhaust glow at bottom
        $exBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 253, 184, 51))
        try {
            $exW = 28 * $s
            $exH = 20 * $s
            $graphics.FillEllipse($exBrush, -$exW / 2, (44 * $s), $exW, $exH)
        } finally { $exBrush.Dispose() }
        $exBrush2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(150, 253, 120, 50))
        try {
            $ex2W = 18 * $s
            $ex2H = 26 * $s
            $graphics.FillEllipse($exBrush2, -$ex2W / 2, (48 * $s), $ex2W, $ex2H)
        } finally { $exBrush2.Dispose() }

        # Restore rotation
        $graphics.Transform = $saved
        $saved.Dispose()

        # ============================================================
        # SPEED LINES (dynamic motion feel — top left)
        # ============================================================
        $speedPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(70, 255, 255, 255), [math]::Max(1, 2.5 * $s))
        $speedPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $speedPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        try {
            $graphics.DrawLine($speedPen, (24 * $s), (40 * $s), (56 * $s), (56 * $s))
            $graphics.DrawLine($speedPen, (18 * $s), (60 * $s), (50 * $s), (72 * $s))
            $graphics.DrawLine($speedPen, (28 * $s), (80 * $s), (54 * $s), (88 * $s))
        } finally { $speedPen.Dispose() }

        # ============================================================
        # STARS (tiny sparkles for fun)
        # ============================================================
        $starBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 255, 255, 255))
        try {
            $starSz = [math]::Max(1, 4 * $s)
            $graphics.FillEllipse($starBrush, (32 * $s), (28 * $s), $starSz, $starSz)
            $graphics.FillEllipse($starBrush, (70 * $s), (48 * $s), ([math]::Max(1, 3 * $s)), ([math]::Max(1, 3 * $s)))
            $graphics.FillEllipse($starBrush, (46 * $s), (96 * $s), ([math]::Max(1, 3 * $s)), ([math]::Max(1, 3 * $s)))
            $graphics.FillEllipse($starBrush, (200 * $s), (30 * $s), $starSz, $starSz)
            $graphics.FillEllipse($starBrush, (220 * $s), (200 * $s), ([math]::Max(1, 3 * $s)), ([math]::Max(1, 3 * $s)))
        } finally { $starBrush.Dispose() }

    } finally {
        $graphics.Dispose()
    }

    return $bitmap
}

function Save-MultiSizeIco {
    param(
        [System.Drawing.Bitmap[]]$Bitmaps,
        [string]$IcoPath
    )

    $pngDatas = @()
    foreach ($bmp in $Bitmaps) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngDatas += ,($ms.ToArray())
        $ms.Dispose()
    }

    $stream = [System.IO.File]::Open($IcoPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    try {
        $writer = New-Object System.IO.BinaryWriter($stream)
        $writer.Write([Int16]0)
        $writer.Write([Int16]1)
        $writer.Write([Int16]$Bitmaps.Count)

        $dataOffset = 6 + ($Bitmaps.Count * 16)

        for ($i = 0; $i -lt $Bitmaps.Count; $i++) {
            $bmp = $Bitmaps[$i]
            $pngData = $pngDatas[$i]
            $w = if ($bmp.Width -ge 256) { 0 } else { [byte]$bmp.Width }
            $h = if ($bmp.Height -ge 256) { 0 } else { [byte]$bmp.Height }
            $writer.Write([byte]$w)
            $writer.Write([byte]$h)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([Int16]1)
            $writer.Write([Int16]32)
            $writer.Write([Int32]$pngData.Length)
            $writer.Write([Int32]$dataOffset)
            $dataOffset += $pngData.Length
        }

        foreach ($pngData in $pngDatas) { $writer.Write($pngData) }
        $writer.Flush()
    } finally { $stream.Dispose() }
}

# --- Main ---
$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null

$pngPath = Join-Path $resolvedOutputDirectory 'easylog.png'
$icoPath = Join-Path $resolvedOutputDirectory 'easylog.ico'

$sizes = @(16, 32, 48, 256)
$bitmaps = @()
foreach ($sz in $sizes) { $bitmaps += Draw-AppIcon -Size $sz }

$bitmaps[-1].Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
Save-MultiSizeIco -Bitmaps $bitmaps -IcoPath $icoPath
foreach ($bmp in $bitmaps) { $bmp.Dispose() }

Write-Host "Generated app icon: $icoPath"
Write-Host "Generated preview image: $pngPath"

