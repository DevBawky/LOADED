param(
    [string]$OutputDirectory = "$PSScriptRoot\..\Assets\Resources\BulletFrames"
)

Add-Type -AssemblyName System.Drawing

$size = 512
$center = [float]($size / 2)
$white = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$transparent = [System.Drawing.Color]::FromArgb(0, 0, 0, 0)

function New-MaskCanvas {
    $bitmap = [System.Drawing.Bitmap]::new(
        $size,
        $size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear($transparent)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.CompositingQuality =
        [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.PixelOffsetMode =
        [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    return @($bitmap, $graphics)
}

function New-RoundPen([float]$width) {
    $pen = [System.Drawing.Pen]::new($white, $width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

function Get-Point([double]$angleDegrees, [double]$radius) {
    $radians = $angleDegrees * [Math]::PI / 180.0
    return [System.Drawing.PointF]::new(
        [float]($center + [Math]::Cos($radians) * $radius),
        [float]($center + [Math]::Sin($radians) * $radius))
}

function Draw-Ring($graphics, [float]$radius, [float]$width) {
    $pen = New-RoundPen $width
    try {
        $graphics.DrawEllipse(
            $pen,
            $center - $radius,
            $center - $radius,
            $radius * 2,
            $radius * 2)
    }
    finally {
        $pen.Dispose()
    }
}

function Draw-Arc(
    $graphics,
    [float]$radius,
    [float]$start,
    [float]$sweep,
    [float]$width) {
    $pen = New-RoundPen $width
    try {
        $graphics.DrawArc(
            $pen,
            $center - $radius,
            $center - $radius,
            $radius * 2,
            $radius * 2,
            $start,
            $sweep)
    }
    finally {
        $pen.Dispose()
    }
}

function Draw-Line(
    $graphics,
    [System.Drawing.PointF]$start,
    [System.Drawing.PointF]$end,
    [float]$width) {
    $pen = New-RoundPen $width
    try {
        $graphics.DrawLine($pen, $start, $end)
    }
    finally {
        $pen.Dispose()
    }
}

function Fill-Polygon($graphics, [System.Drawing.PointF[]]$points) {
    $brush = [System.Drawing.SolidBrush]::new($white)
    try {
        $graphics.FillPolygon($brush, $points)
    }
    finally {
        $brush.Dispose()
    }
}

function Fill-Circle(
    $graphics,
    [System.Drawing.PointF]$point,
    [float]$radius) {
    $brush = [System.Drawing.SolidBrush]::new($white)
    try {
        $graphics.FillEllipse(
            $brush,
            $point.X - $radius,
            $point.Y - $radius,
            $radius * 2,
            $radius * 2)
    }
    finally {
        $brush.Dispose()
    }
}

function Fill-RadialRectangle(
    $graphics,
    [double]$angle,
    [double]$innerRadius,
    [double]$outerRadius,
    [double]$halfWidth) {
    $radians = $angle * [Math]::PI / 180.0
    $directionX = [Math]::Cos($radians)
    $directionY = [Math]::Sin($radians)
    $normalX = -$directionY
    $normalY = $directionX
    $points = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(
            [float]($center + $directionX * $innerRadius + $normalX * $halfWidth),
            [float]($center + $directionY * $innerRadius + $normalY * $halfWidth)),
        [System.Drawing.PointF]::new(
            [float]($center + $directionX * $outerRadius + $normalX * $halfWidth),
            [float]($center + $directionY * $outerRadius + $normalY * $halfWidth)),
        [System.Drawing.PointF]::new(
            [float]($center + $directionX * $outerRadius - $normalX * $halfWidth),
            [float]($center + $directionY * $outerRadius - $normalY * $halfWidth)),
        [System.Drawing.PointF]::new(
            [float]($center + $directionX * $innerRadius - $normalX * $halfWidth),
            [float]($center + $directionY * $innerRadius - $normalY * $halfWidth)))
    Fill-Polygon $graphics $points
}

function Fill-TangentArrow(
    $graphics,
    [double]$angle,
    [double]$radius,
    [double]$radialHalfWidth,
    [double]$tangentLength,
    [bool]$clockwise) {
    $radians = $angle * [Math]::PI / 180.0
    $directionX = [Math]::Cos($radians)
    $directionY = [Math]::Sin($radians)
    $tangentX = -$directionY
    $tangentY = $directionX
    if ($clockwise) {
        $tangentX = -$tangentX
        $tangentY = -$tangentY
    }
    $baseX = $center + $directionX * $radius - $tangentX * $tangentLength * 0.45
    $baseY = $center + $directionY * $radius - $tangentY * $tangentLength * 0.45
    $points = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(
            [float]($baseX + $directionX * $radialHalfWidth),
            [float]($baseY + $directionY * $radialHalfWidth)),
        [System.Drawing.PointF]::new(
            [float]($baseX - $directionX * $radialHalfWidth),
            [float]($baseY - $directionY * $radialHalfWidth)),
        [System.Drawing.PointF]::new(
            [float]($center + $directionX * $radius + $tangentX * $tangentLength),
            [float]($center + $directionY * $radius + $tangentY * $tangentLength)))
    Fill-Polygon $graphics $points
}

function Fill-Leaf(
    $graphics,
    [System.Drawing.PointF]$base,
    [System.Drawing.PointF]$tip,
    [float]$width) {
    $dx = $tip.X - $base.X
    $dy = $tip.Y - $base.Y
    $length = [Math]::Max(0.001, [Math]::Sqrt($dx * $dx + $dy * $dy))
    $normalX = -$dy / $length
    $normalY = $dx / $length
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    try {
        $path.AddBezier(
            $base,
            [System.Drawing.PointF]::new(
                [float]($base.X + $dx * 0.38 + $normalX * $width),
                [float]($base.Y + $dy * 0.38 + $normalY * $width)),
            [System.Drawing.PointF]::new(
                [float]($base.X + $dx * 0.75 + $normalX * $width * 0.55),
                [float]($base.Y + $dy * 0.75 + $normalY * $width * 0.55)),
            $tip)
        $path.AddBezier(
            $tip,
            [System.Drawing.PointF]::new(
                [float]($base.X + $dx * 0.75 - $normalX * $width * 0.55),
                [float]($base.Y + $dy * 0.75 - $normalY * $width * 0.55)),
            [System.Drawing.PointF]::new(
                [float]($base.X + $dx * 0.38 - $normalX * $width),
                [float]($base.Y + $dy * 0.38 - $normalY * $width)),
            $base)
        $path.CloseFigure()
        $brush = [System.Drawing.SolidBrush]::new($white)
        try {
            $graphics.FillPath($brush, $path)
        }
        finally {
            $brush.Dispose()
        }
    }
    finally {
        $path.Dispose()
    }
}

function Draw-RotatedEllipse(
    $graphics,
    [System.Drawing.PointF]$position,
    [float]$width,
    [float]$height,
    [float]$angle,
    [float]$strokeWidth) {
    $state = $graphics.Save()
    $pen = New-RoundPen $strokeWidth
    try {
        $graphics.TranslateTransform($position.X, $position.Y)
        $graphics.RotateTransform($angle)
        $graphics.DrawEllipse(
            $pen,
            -$width * 0.5,
            -$height * 0.5,
            $width,
            $height)
    }
    finally {
        $pen.Dispose()
        $graphics.Restore($state)
    }
}

function Save-Mask([string]$name, [scriptblock]$draw) {
    $canvas = New-MaskCanvas
    $bitmap = $canvas[0]
    $graphics = $canvas[1]
    try {
        & $draw $graphics
        $path = Join-Path $OutputDirectory "$name.png"
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Output $path
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

[System.IO.Directory]::CreateDirectory(
    [System.IO.Path]::GetFullPath($OutputDirectory)) | Out-Null

Save-Mask "Normal" {
    param($g)
    Draw-Ring $g 216 13
    Draw-Ring $g 188 7
}

Save-Mask "Ghost" {
    param($g)
    foreach ($start in @(8, 128, 248)) {
        Draw-Arc $g 224 $start 82 13
        Draw-Arc $g 194 $start 82 7
    }
}

Save-Mask "Sniper" {
    param($g)
    Draw-Ring $g 188 7
    foreach ($start in @(20, 110, 200, 290)) {
        Draw-Arc $g 216 $start 50 11
    }
    Draw-Line $g ([System.Drawing.PointF]::new(180, 24)) ([System.Drawing.PointF]::new(332, 24)) 10
    Draw-Line $g ([System.Drawing.PointF]::new(190, 24)) ([System.Drawing.PointF]::new(190, 58)) 10
    Draw-Line $g ([System.Drawing.PointF]::new(322, 24)) ([System.Drawing.PointF]::new(322, 58)) 10
    Draw-Line $g ([System.Drawing.PointF]::new(180, 488)) ([System.Drawing.PointF]::new(332, 488)) 10
    Draw-Line $g ([System.Drawing.PointF]::new(190, 454)) ([System.Drawing.PointF]::new(190, 488)) 10
    Draw-Line $g ([System.Drawing.PointF]::new(322, 454)) ([System.Drawing.PointF]::new(322, 488)) 10
    Draw-Line $g ([System.Drawing.PointF]::new(24, 180)) ([System.Drawing.PointF]::new(24, 332)) 10
    Draw-Line $g ([System.Drawing.PointF]::new(24, 190)) ([System.Drawing.PointF]::new(58, 190)) 10
    Draw-Line $g ([System.Drawing.PointF]::new(24, 322)) ([System.Drawing.PointF]::new(58, 322)) 10
    Draw-Line $g ([System.Drawing.PointF]::new(488, 180)) ([System.Drawing.PointF]::new(488, 332)) 10
    Draw-Line $g ([System.Drawing.PointF]::new(454, 190)) ([System.Drawing.PointF]::new(488, 190)) 10
    Draw-Line $g ([System.Drawing.PointF]::new(454, 322)) ([System.Drawing.PointF]::new(488, 322)) 10
}

Save-Mask "Storm" {
    param($g)
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $points = [System.Collections.Generic.List[System.Drawing.PointF]]::new()
    for ($degree = 0; $degree -le 360; $degree += 2) {
        $radians = $degree * [Math]::PI / 180.0
        $radius = 202 + 22 * [Math]::Sin($radians * 5)
        $points.Add([System.Drawing.PointF]::new(
            [float]($center + [Math]::Cos($radians) * $radius),
            [float]($center + [Math]::Sin($radians) * $radius)))
    }
    try {
        $path.AddLines($points.ToArray())
        $path.CloseFigure()
        $pen = New-RoundPen 16
        try { $g.DrawPath($pen, $path) } finally { $pen.Dispose() }
    }
    finally {
        $path.Dispose()
    }
}

Save-Mask "Shotgun" {
    param($g)
    Draw-Ring $g 194 12
    foreach ($angle in 0..7 | ForEach-Object { $_ * 45 }) {
        Fill-Circle $g (Get-Point $angle 238) 11
        Fill-Circle $g (Get-Point ($angle - 4.8) 218) 11
        Fill-Circle $g (Get-Point ($angle + 4.8) 218) 11
    }
}

Save-Mask "Piercing" {
    param($g)
    foreach ($radiusWidth in @(@(216, 12), @(188, 7))) {
        Draw-Arc $g $radiusWidth[0] 18 144 $radiusWidth[1]
        Draw-Arc $g $radiusWidth[0] 198 144 $radiusWidth[1]
    }
    foreach ($side in @(-1, 1)) {
        $innerBaseX = $center + $side * 157
        $innerTipX = $center + $side * 132
        $outerBaseX = $center + $side * 236
        $outerTipX = $center + $side * 203
        Fill-Polygon $g ([System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new([float]$innerBaseX, 239),
            [System.Drawing.PointF]::new([float]$innerBaseX, 273),
            [System.Drawing.PointF]::new([float]$innerTipX, 256)))
        Fill-Polygon $g ([System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new([float]$outerBaseX, 237),
            [System.Drawing.PointF]::new([float]$outerBaseX, 275),
            [System.Drawing.PointF]::new([float]$outerTipX, 256)))
    }
}

Save-Mask "Debuff" {
    param($g)
    Draw-Ring $g 216 12
    Draw-Ring $g 188 7
    foreach ($angle in 0..5 | ForEach-Object { $_ * 60 }) {
        $baseA = Get-Point ($angle - 7) 190
        $baseB = Get-Point ($angle + 7) 190
        $tip = Get-Point $angle 143
        Fill-Polygon $g ([System.Drawing.PointF[]]@($baseA, $baseB, $tip))
    }
}

Save-Mask "Kinetic" {
    param($g)
    foreach ($angle in @(0, 90, 180, 270)) {
        Draw-Arc $g 216 ($angle + 18) 56 12
        Draw-Arc $g 188 ($angle + 18) 56 7
        Fill-TangentArrow $g ($angle + 10) 202 35 48 $true
    }
}

Save-Mask "Combo" {
    param($g)
    foreach ($start in @(61, 151, 241, 331)) {
        Draw-Arc $g 216 $start 58 12
        Draw-Arc $g 188 $start 58 7
    }
    foreach ($angle in @(45, 135, 225, 315)) {
        Draw-RotatedEllipse $g (Get-Point $angle 202) 58 30 ($angle + 90) 10
    }
}

Save-Mask "Economy" {
    param($g)
    Draw-Ring $g 216 12
    Draw-Ring $g 188 7
    foreach ($angle in 0..11 | ForEach-Object { $_ * 30 }) {
        Fill-RadialRectangle $g $angle 180 238 9
    }
}

Save-Mask "Growth" {
    param($g)
    foreach ($start in @(-56, 124)) {
        Draw-Arc $g 210 $start 112 13
        Draw-Arc $g 184 $start 112 7
    }
    Draw-Line $g ([System.Drawing.PointF]::new(256, 92)) ([System.Drawing.PointF]::new(256, 70)) 9
    Fill-Leaf $g ([System.Drawing.PointF]::new(256, 82)) ([System.Drawing.PointF]::new(256, 20)) 18
    Fill-Leaf $g ([System.Drawing.PointF]::new(246, 86)) ([System.Drawing.PointF]::new(188, 44)) 16
    Fill-Leaf $g ([System.Drawing.PointF]::new(266, 86)) ([System.Drawing.PointF]::new(324, 44)) 16
    Fill-Leaf $g ([System.Drawing.PointF]::new(250, 422)) ([System.Drawing.PointF]::new(202, 490)) 10
    Fill-Leaf $g ([System.Drawing.PointF]::new(256, 420)) ([System.Drawing.PointF]::new(256, 500)) 10
    Fill-Leaf $g ([System.Drawing.PointF]::new(262, 422)) ([System.Drawing.PointF]::new(310, 490)) 10
}

Save-Mask "Blood" {
    param($g)
    foreach ($start in @(62, 152, 242, 332)) {
        Draw-Arc $g 210 $start 56 15
        Draw-Arc $g 184 $start 56 7
    }
    foreach ($angle in @(45, 135, 225, 315)) {
        $leftShoulder = Get-Point ($angle - 9) 232
        $rightShoulder = Get-Point ($angle + 9) 232
        $inner = Get-Point $angle 164
        Fill-Polygon $g ([System.Drawing.PointF[]]@(
            $leftShoulder,
            $inner,
            $rightShoulder))
    }
}
