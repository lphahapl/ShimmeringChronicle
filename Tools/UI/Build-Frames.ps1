Add-Type -AssemblyName System.Drawing
$outDir = Join-Path $PSScriptRoot '../../Assets/Art/UI/Astral'
function Make-Frame($name, $top, $bottom, $border) {
 $bmp = [System.Drawing.Bitmap]::new(128,128)
 $g = [System.Drawing.Graphics]::FromImage($bmp)
 $g.SmoothingMode = 'AntiAlias'
 $rect = [System.Drawing.Rectangle]::new(2,2,123,123)
 $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($rect,[System.Drawing.ColorTranslator]::FromHtml($top),[System.Drawing.ColorTranslator]::FromHtml($bottom),90.0)
 $g.FillRectangle($brush,$rect)
 $pen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml($border),1)
 $g.DrawRectangle($pen,2,2,123,123)
 $pen.Color = [System.Drawing.Color]::FromArgb(40,230,218,185)
 $g.DrawRectangle($pen,6,6,115,115)
 $pen.Color = [System.Drawing.ColorTranslator]::FromHtml($border)
 $pen.Width=2
 foreach($x in @(2,125)){foreach($y in @(2,125)){
  $dx= if($x -eq 2){12}else{-12}; $dy= if($y -eq 2){12}else{-12}
  $g.DrawLine($pen,$x,$y,($x+$dx),$y); $g.DrawLine($pen,$x,$y,$x,($y+$dy))
 }}
 $bmp.Save((Join-Path $outDir ($name+'.png')),[System.Drawing.Imaging.ImageFormat]::Png)
 $pen.Dispose();$brush.Dispose();$g.Dispose();$bmp.Dispose()
}
Make-Frame 'panel' '#283344' '#1D2635' '#9F9279'
Make-Frame 'slot' '#46546B' '#303C51' '#C0B295'
Make-Frame 'hp_track' '#293D40' '#202E33' '#A1A68B'
$bmp=[System.Drawing.Bitmap]::new(32,8)
$g=[System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::White)
$bmp.Save((Join-Path $outDir 'bar_fill.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()
