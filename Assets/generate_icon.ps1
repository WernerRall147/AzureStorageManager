# This script creates a simple icon for the Azure Storage Manager application
# Requires System.Drawing assembly

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

# Icon dimensions
$width = 256
$height = 256

# Create a new bitmap
$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

# Set background to Azure blue
$azureBlue = [System.Drawing.Color]::FromArgb(255, 0, 120, 212)
$graphics.Clear($azureBlue)

# Create a brush for the storage icon
$white = [System.Drawing.Brushes]::White
$lightBlue = [System.Drawing.Color]::FromArgb(255, 110, 200, 255)
$lightBlueBrush = New-Object System.Drawing.SolidBrush($lightBlue)

# Draw a storage icon (simplified representation)
$storageRect = New-Object System.Drawing.Rectangle(48, 48, 160, 120)
$graphics.FillRectangle($white, $storageRect)

# Draw horizontal lines to represent data
$lineY1 = 80
$lineY2 = 110
$lineY3 = 140
$lineX1 = 60
$lineX2 = 196
$lineHeight = 10

$graphics.FillRectangle($lightBlueBrush, $lineX1, $lineY1, $lineX2 - $lineX1, $lineHeight)
$graphics.FillRectangle($lightBlueBrush, $lineX1, $lineY2, $lineX2 - $lineX1, $lineHeight)
$graphics.FillRectangle($lightBlueBrush, $lineX1, $lineY3, $lineX2 - $lineX1, $lineHeight)

# Draw a check mark to represent verification
$checkPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 50, 200, 50), 10)
[System.Drawing.Point[]]$checkPoints = @(
    [System.Drawing.Point]::new(100, 180),
    [System.Drawing.Point]::new(130, 210),
    [System.Drawing.Point]::new(190, 150)
)
$graphics.DrawLines($checkPen, $checkPoints)

# Clean up graphics resources
$graphics.Dispose()

# Save the bitmap as a PNG
$iconPath = Join-Path (Split-Path $MyInvocation.MyCommand.Path) "app_icon.png"
$bitmap.Save($iconPath, [System.Drawing.Imaging.ImageFormat]::Png)

# Convert to ICO using .NET
$ico = New-Object System.Drawing.Icon([System.IO.MemoryStream]::new([System.IO.File]::ReadAllBytes($iconPath)), 256, 256)
$fs = New-Object System.IO.FileStream((Join-Path (Split-Path $MyInvocation.MyCommand.Path) "app_icon.ico"), [System.IO.FileMode]::Create)
$ico.Save($fs)
$fs.Close()
$ico.Dispose()

Write-Host "Icon created successfully at: $(Join-Path (Split-Path $MyInvocation.MyCommand.Path) 'app_icon.ico')"
