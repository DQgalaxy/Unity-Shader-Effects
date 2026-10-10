from pathlib import Path
from PIL import Image, ImageDraw
from docx import Document

root = Path('F:/UnityProjects/10')
out = root / 'Logs/GroundSlashLesson'
out.mkdir(parents=True, exist_ok=True)
sources = [
    Path('F:/UnityProjects/OthersDemo/kenney-particle-pack-master/addons/kenney_particle_pack') / ('slash_%02d.png' % i)
    for i in range(1, 5)
]
sources += [root/'Assets/Trail/Textures/Trail_02.png']
base = root/'Assets/UnityTechnologies/ParticlePack/EffectExamples/Magic Effects/Textures'
sources += [base/n for n in ['EarthCrackAlbedo.tif','EarthCrackEmissive.tif','DustPuffSmall.png','RocksAlbedo.tga']]
sheet = Image.new('RGB', (960, 320*3), '#30383d')
draw = ImageDraw.Draw(sheet)
for i, p in enumerate(sources):
    im = Image.open(p).convert('RGBA')
    print(p.name, im.size, 'extrema:', im.getextrema())
    im.thumbnail((300,270))
    x, y = (i%3)*320, (i//3)*320
    sheet.paste(im,(x+(320-im.width)//2,y+28),im)
    draw.text((x+8,y+6),p.name,fill='white')
sheet.save(out/'assets.png')
doc = Document(root/'工程10_ShaderGraph特效原理与实操教程.docx')
for p in doc.paragraphs:
    if p.style.name.startswith('Heading'):
        print(p.style.name, p.text)
print('SECTION',doc.sections[0].page_width,doc.sections[0].page_height)
for name in ['Normal','Heading 1','Heading 2','Heading 3']:
    s=doc.styles[name]; print('STYLE',name,s.font.name,s.font.size,s.paragraph_format.space_after)
