$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$dest = Join-Path $root 'Assets/PortfolioFX/ReferenceArc'
if (Test-Path -LiteralPath $dest) { throw 'ReferenceArc already exists; preserve subsequent artist edits.' }
New-Item -ItemType Directory -Path $dest | Out-Null
$items = @(
    @{ Name='T_ArcFilament.png'; Source='Assets/Trail/Textures/Trail_02.png'; Channel='Alpha'; WrapU=1 },
    @{ Name='T_ArcFlow.png'; Source='Assets/Trail/Textures/Trail_01.png'; Channel='Alpha'; WrapU=0 },
    @{ Name='T_ArcBranchAtlas.png'; Source='Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Textures/Lightning.tif'; Channel='RedAlpha'; WrapU=1 },
    @{ Name='T_ArcContact.png'; Source='Assets/PortfolioFX/ReferenceShield/ContactFlare.png'; Channel='RedAlpha'; WrapU=1 }
)
$protected = @('Assets/PortfolioFX/Shaders/SG_ArcRibbon.shadergraph','Assets/PortfolioFX/Scripts/Runtime/FXArcBeam.cs','Assets/PortfolioFX/Scenes/ElectricOverload.unity','Assets/PortfolioFX/Scenes/ArcaneChargeShot.unity','Assets/PortfolioFX/MyMaterial/M_ElectricArc.mat','Assets/PortfolioFX/Materials/ArcaneChargeShot_Arc.mat')
$hashes = @{}
foreach ($p in $protected) { $hashes[$p] = (Get-FileHash -LiteralPath (Join-Path $root $p) -Algorithm SHA256).Hash }
$sheet = [System.Drawing.Bitmap]::new(1040,300)
$graphics = [System.Drawing.Graphics]::FromImage($sheet)
$graphics.Clear([System.Drawing.Color]::FromArgb(20,20,20))
$font = [System.Drawing.Font]::new('Arial',11)
$manifest = @()
try {
    for ($k=0; $k -lt $items.Count; $k++) {
        $item=$items[$k]
        $source=Join-Path $root $item.Source
        $input=[System.Drawing.Bitmap]::new($source)
        $output=[System.Drawing.Bitmap]::new($input.Width,$input.Height)
        try {
            $nonzero=0; $peak=0
            for ($y=0; $y -lt $input.Height; $y++) {
                for ($x=0; $x -lt $input.Width; $x++) {
                    $c=$input.GetPixel($x,$y)
                    $v=if ($item.Channel -eq 'Alpha') { [int]$c.A } else { [int][math]::Round($c.R*$c.A/255.0) }
                    $output.SetPixel($x,$y,[System.Drawing.Color]::FromArgb(255,$v,$v,$v))
                    if ($v -gt 0) { $nonzero++ }
                    $peak=[math]::Max($peak,$v)
                }
            }
            if ($peak -lt 30 -or $nonzero -eq 0) { throw ('Empty or unexpectedly dark mask: '+$item.Name) }
            $target=Join-Path $dest $item.Name
            $output.Save($target,[System.Drawing.Imaging.ImageFormat]::Png)
            $meta=[IO.File]::ReadAllText((Join-Path $root 'Assets/PortfolioFX/Textures/NoiseRG.png.meta'))
            $meta=$meta -replace '(?m)^guid: \w+',('guid: '+[guid]::NewGuid().ToString('N'))
            $meta=$meta -replace 'sRGBTexture: 1','sRGBTexture: 0'
            $meta=$meta -replace 'wrapU: \d+',('wrapU: '+$item.WrapU)
            $meta=$meta -replace 'wrapV: \d+','wrapV: 1'
            [IO.File]::WriteAllText(($target+'.meta'),$meta,[Text.UTF8Encoding]::new($false))
            $graphics.DrawImage($output,[System.Drawing.Rectangle]::new($k*260+5,5,250,250))
            $graphics.DrawString($item.Name,$font,[System.Drawing.Brushes]::White,$k*260+5,266)
            $manifest+=@{ source=$item.Source; output=('Assets/PortfolioFX/ReferenceArc/'+$item.Name); conversion=$item.Channel; width=$input.Width; height=$input.Height; nonzeroPixels=$nonzero; peak=$peak }
        } finally { $input.Dispose(); $output.Dispose() }
    }
    $preview=Join-Path $root 'Arc_贴图预览.png'
    $sheet.Save($preview,[System.Drawing.Imaging.ImageFormat]::Png)
    $report=@{ protectedAssets=$hashes; textures=$manifest; note='Derived masks from existing assets, not original artwork. All outputs are linear R masks with opaque alpha. Lightning is a 2x2 atlas, not a seamless strip.' }
    [IO.File]::WriteAllText((Join-Path $dest 'manifest.json'),($report|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
    foreach ($p in $protected) {
        if ((Get-FileHash -LiteralPath (Join-Path $root $p) -Algorithm SHA256).Hash -ne $hashes[$p]) { throw ('Unexpected change: '+$p) }
    }
    Write-Output ($report | ConvertTo-Json -Depth 5)
    Write-Output $preview
} finally { $graphics.Dispose();$sheet.Dispose();$font.Dispose() }
