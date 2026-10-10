$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
public static class ElectricMasks {
    public static void Prepare(string input, string output) {
        using (var src = new Bitmap(input))
        using (var dst = new Bitmap(src.Width, src.Height)) {
            for (int y=0;y<src.Height;y++) for(int x=0;x<src.Width;x++) {
                double value=0;
                for(int dy=-2;dy<=2;dy++) for(int dx=-2;dx<=2;dx++) {
                    int xx=Math.Max(0,Math.Min(src.Width-1,x+dx));
                    int yy=Math.Max(0,Math.Min(src.Height-1,y+dy));
                    double weight=dx*dx+dy*dy<=1?1:0.28;
                    value=Math.Max(value,src.GetPixel(xx,yy).R*weight);
                }
                int c=(int)Math.Min(255,value*1.45);
                dst.SetPixel(x,y,Color.FromArgb(255,c,c,c));
            }
            dst.Save(output,ImageFormat.Png);
        }
    }
}
'@
$dest = Join-Path $PWD 'Assets/PortfolioFX/ReferenceArc/T_BodyLightning.png'
if (Test-Path -LiteralPath $dest) { throw 'Preserve existing derived texture.' }
[ElectricMasks]::Prepare((Join-Path $PWD 'Assets/PortfolioFX/ReferenceArc/T_ArcBranchAtlas.png'),$dest)
$meta = [IO.File]::ReadAllText((Join-Path $PWD 'Assets/PortfolioFX/ReferenceArc/T_ArcFlow.png.meta'))
$meta = $meta -replace '(?m)^guid: \w+', ('guid: '+[guid]::NewGuid().ToString('N'))
$meta = $meta -replace 'wrapV: 1','wrapV: 0'
[IO.File]::WriteAllText(($dest+'.meta'),$meta,[Text.UTF8Encoding]::new($false))
Write-Output $dest
