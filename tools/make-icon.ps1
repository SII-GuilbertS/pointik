# Génère l'icône originale de Pointik (anneau jaune + point de clic bleu sur fond sombre arrondi)
# au format ICO multi-tailles (entrées PNG).
param([string]$Out)
Add-Type -AssemblyName System.Drawing
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = @()
foreach ($n in $sizes) {
  $bmp = New-Object System.Drawing.Bitmap $n, $n
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
  $r = [Math]::Max(2, $n * 0.22)
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $w = $n - 1
  $path.AddArc(0, 0, 2*$r, 2*$r, 180, 90); $path.AddArc($w-2*$r, 0, 2*$r, 2*$r, 270, 90)
  $path.AddArc($w-2*$r, $w-2*$r, 2*$r, 2*$r, 0, 90); $path.AddArc(0, $w-2*$r, 2*$r, 2*$r, 90, 90); $path.CloseFigure()
  $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Point 0,0), (New-Object System.Drawing.Point 0,$n), [System.Drawing.Color]::FromArgb(255,40,46,58), [System.Drawing.Color]::FromArgb(255,22,25,32))
  $g.FillPath($bg, $path)
  $c = $n / 2.0
  $ringR = $n * 0.30; $th = [Math]::Max(1.6, $n * 0.095)
  $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255,255,196,31)), $th
  $g.DrawEllipse($pen, [single]($c-$ringR), [single]($c-$ringR), [single](2*$ringR), [single](2*$ringR))
  $dot = [Math]::Max(1.5, $n * 0.09)
  $blue = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,45,127,249))
  $g.FillEllipse($blue, [single]($c-$dot), [single]($c-$dot), [single](2*$dot), [single](2*$dot))
  $g.Dispose()
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  $pngs += ,($ms.ToArray())
}
$fs = [System.IO.File]::Create($Out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $n = $sizes[$i]; $len = $pngs[$i].Length
  $b = if ($n -ge 256) { 0 } else { $n }
  $bw.Write([byte]$b); $bw.Write([byte]$b); $bw.Write([byte]0); $bw.Write([byte]0)
  $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]$len); $bw.Write([UInt32]$offset)
  $offset += $len
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close(); $fs.Close()
"Icone ecrite : $Out"
