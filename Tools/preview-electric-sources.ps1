$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sources = @(
    'Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Textures/LightningTrail.tif',
    'Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Textures/EnergyEffect.tif',
    'Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects/Textures/EarthCrackEmissive.tif',
    'Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects/Textures/FireBallEmission.tif',
    'Assets/PortfolioFX/ReferenceShield/HitNoise.png',
    'Assets/PortfolioFX/ReferenceArc/T_ArcBranchAtlas.png'
)
$sheet = [Drawing.Bitmap]::new(1440,510)
$g = [Drawing.Graphics]::FromImage($sheet)
$g.Clear([Drawing.Color]::FromArgb(25,25,25))
$font = [Drawing.Font]::new('Arial',9)
try {
    for ($i=0; $i -lt $sources.Count; $i++) {
        $b = [Drawing.Bitmap]::new((Join-Path $PWD $sources[$i]))
        $a = [Drawing.Bitmap]::new($b.Width,$b.Height)
        try {
            for ($y=0; $y -lt $b.Height; $y++) { for ($x=0; $x -lt $b.Width; $x++) {
                $v=$b.GetPixel($x,$y).A
                $a.SetPixel($x,$y,[Drawing.Color]::FromArgb(255,$v,$v,$v))
            } }
            $g.DrawImage($b,[Drawing.Rectangle]::new($i*240,0,235,235))
            $g.DrawImage($a,[Drawing.Rectangle]::new($i*240,260,235,235))
            $g.DrawString([IO.Path]::GetFileName($sources[$i]),$font,[Drawing.Brushes]::White,$i*240,240)
        } finally { $b.Dispose(); $a.Dispose() }
    }
    [IO.Directory]::CreateDirectory((Join-Path $PWD 'Logs/ElectricUpgrade')) | Out-Null
    $sheet.Save((Join-Path $PWD 'Logs/ElectricUpgrade/sources.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally { $font.Dispose(); $g.Dispose(); $sheet.Dispose() }
