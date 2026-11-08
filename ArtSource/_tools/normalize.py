"""Normalize Yowol player frames: same character scale, feet anchored at a fixed point, uniform canvas.
Usage: python3 -I normalize.py <src Character dir> <face.json> <out dir>
"""
import sys, os, json, re, glob
import numpy as np
from PIL import Image
from scipy import ndimage

SRC, FACE, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
face = {k.lstrip('./'): v for k, v in json.load(open(FACE)).items()}

TARGET = 400 / 515.0   # final pixel scale relative to the reference idle drawing (idle ~400px tall)

# clip name -> (folder, frame files in play order, per-frame durations sec, source-scale (float or list), anchor mode, loop)
# anchor modes: 'feet'  = per-frame canvas, feet on bbox bottom, x = shoe blobs centre
#               'clip'  = canvas already consistent within clip: anchor from first frame, applied to all
#               'face'  = per-frame, y = bbox bottom, x = face centre minus clip offset
def anim_times(path):
    t = open(path, encoding='utf-8', errors='ignore').read()
    times = [float(x) for x in re.findall(r'- time: ([\d.]+)\n\s+value: \{fileID', t)]
    return times

CLIPS = {
 'Sword_Idle':      ('Character_Idle', ['Idle_0','Idle_1','Idle_2','Idle_3'], [0.16,0.16,0.16,0.15], 1.0, 'feet', True),
 'Bow_Idle':        ('Character_Idle', ['Bow_Idle_0','Bow_Idle_1','Bow_Idle_2','Bow_Idle_3'], [0.10,0.13,0.12,0.13], 1.0, 'feet', True),
 'GreatSword_Idle': ('Character_Idle', ['LongSword1','LongSword2','LongSword3','LongSword4'], [0.15,0.15,0.15,0.13], 1.03, 'feet', True),
 'Run':             ('Character_running', ['Run_0','Run_1','Run_2','Run_3'], [0.13,0.13,0.13,0.13], 0.82, 'face', True),
 'Sword_Attack':    ('Character_Sword_Attack', ['Attack_0','Attack_1','Attack_2'], [0.083,0.083,0.12], [0.90,0.85,0.87], 'feet', False),
 'GreatSword_Attack':('Character_Lsword_Attack', ['Lsword_Attack_0','Lsword_Attack_1','Lsword_Attack_2','Lsword_Attack_3'], [0.083,0.05,0.05,0.12], 0.84, 'feet', False),
 'Bow_Attack':      ('Character_Bow_Attack', ['Bow_Attack_0','Bow_Attack_1','Bow_Attack_2','Bow_Attack_3'], [0.10,0.10,0.12,0.12], 1.0, 'feet', False),
 'Death':           ('Character_Death', ['death_%02d' % i for i in range(1, 9)], [0.083,0.067,0.083,0.083,0.083,0.067,0.05,0.4], 0.80, 'clip', False),
}
for w, folder, s in [('Sword','S_Run_Jump',0.87), ('Bow','B_Run_Jump',0.91), ('GreatSword','L_Run_Jump',0.82)]:
    CLIPS[f'{w}_Jump'] = ('Character_Jump/' + folder, ['jump_%02d' % i for i in range(1, 13)], [0.055]*12, s, 'clip', False)
for w, folder, s in [('Sword','S_Idle_Jp',0.88), ('Bow','B_Idle_Jump',0.90), ('GreatSword','L_Idle_Jump',0.84)]:
    CLIPS[f'{w}_JumpInPlace'] = ('Character_Jump/' + folder, ['jump_%02d' % i for i in range(1, 13)], [0.055]*12, s, 'clip', False)
SW = {'Sword_to_Bow':'S','Sword_to_Lsword':'S','Bow_to_Sword':'B','Bow_to_Lsword':'B','Lsword_to_sword':'L','Lsword_to_Bow':'L'}
names = {'Sword_to_Bow':'Swap_Sword_To_Bow','Sword_to_Lsword':'Swap_Sword_To_GreatSword','Bow_to_Sword':'Swap_Bow_To_Sword',
         'Bow_to_Lsword':'Swap_Bow_To_GreatSword','Lsword_to_sword':'Swap_GreatSword_To_Sword','Lsword_to_Bow':'Swap_GreatSword_To_Bow'}
for folder, nm in names.items():
    files = sorted(os.path.basename(p)[:-4] for p in glob.glob(f'{SRC}/Swap/{folder}/*.png'))
    CLIPS[nm] = ('Swap/' + folder, files, [0.1]*len(files), 0.75, 'clip', False)

def load(p):
    return Image.open(p).convert('RGBA')

def feet_center(a):
    """a: alpha bool array. centre x of the shoe blobs in the bottom band."""
    ys, xs = np.where(a)
    bottom = ys.max(); band = max(8, int(a.shape[0] * 0.05))
    sub = a[bottom - band:bottom + 1]
    lab, n = ndimage.label(sub)
    if n == 0: return xs.mean(), bottom
    sizes = ndimage.sum(sub, lab, range(1, n + 1))
    blobs = []
    for i, sz in enumerate(sizes, 1):
        yy, xx = np.where(lab == i)
        w = xx.max() - xx.min() + 1
        if sz > 40 and w > 12: blobs.append((sz, xx.min(), xx.max()))
    blobs = sorted(blobs, reverse=True)[:2] or [(0, xs.min(), xs.max())]
    lo = min(b[1] for b in blobs); hi = max(b[2] for b in blobs)
    return (lo + hi) / 2.0, bottom

results = {}
for clip, (folder, files, durs, scale, mode, loop) in CLIPS.items():
    scales = scale if isinstance(scale, list) else [scale] * len(files)
    frames = []
    clip_anchor = None; face_off = None
    if mode == 'face':
        offs = []
        for f in files:
            im = load(f'{SRC}/{folder}/{f}.png'); a = np.array(im)[..., 3] > 20
            fx, _ = feet_center(a); offs.append(face[f'{folder}/{f}.png']['cx'] - fx)
        face_off = float(np.median(offs))
    for i, f in enumerate(files):
        im = load(f'{SRC}/{folder}/{f}.png'); a = np.array(im)[..., 3] > 20
        if mode == 'feet':
            ax, ay = feet_center(a)
        elif mode == 'face':
            _, ay = feet_center(a); ax = face[f'{folder}/{f}.png']['cx'] - face_off
        else:
            if clip_anchor is None: clip_anchor = feet_center(a)
            ax, ay = clip_anchor
        k = TARGET / scales[i]
        frames.append(dict(file=f, img=im, k=k, ax=ax * k, ay=(ay + 1) * k))
    results[clip] = dict(frames=frames, durs=durs, loop=loop)

# common canvas: anchor at (CW/2, CH - PAD)
L = R = U = D = 0
for c in results.values():
    for fr in c['frames']:
        bb = fr['img'].getbbox(); k = fr['k']
        L = max(L, fr['ax'] - bb[0] * k); R = max(R, bb[2] * k - fr['ax']); U = max(U, fr['ay'] - bb[1] * k); D = max(D, bb[3] * k - fr['ay'])
half = int(np.ceil(max(L, R) / 16) * 16) + 16
CW = 2 * half; PAD = int(np.ceil(D / 8) * 8) + 8; CH = int(np.ceil((U + PAD) / 16) * 16) + 16
AX, AY = CW // 2, CH - PAD
print('canvas', CW, CH, 'anchor', AX, AY, 'extents L R U D', round(L), round(R), round(U), round(D))

os.makedirs(f'{OUT}/frames', exist_ok=True); os.makedirs(f'{OUT}/sheets', exist_ok=True)
meta = dict(cell=[CW, CH], pivotPx=[AX, PAD], pivot=[AX / CW, PAD / CH], pixelsPerUnit=256, clips={})
for clip, c in results.items():
    n = len(c['frames'])
    sheet = Image.new('RGBA', (CW * n, CH), (0, 0, 0, 0))
    for i, fr in enumerate(c['frames']):
        im = fr['img'].resize((max(1, round(fr['img'].width * fr['k'])), max(1, round(fr['img'].height * fr['k']))), Image.LANCZOS)
        cell = Image.new('RGBA', (CW, CH), (0, 0, 0, 0))
        cell.alpha_composite(im, (int(round(AX - fr['ax'])), int(round(AY - fr['ay']))))
        os.makedirs(f'{OUT}/frames/{clip}', exist_ok=True)
        cell.save(f'{OUT}/frames/{clip}/{clip}_{i:02d}.png', optimize=True)
        sheet.alpha_composite(cell, (i * CW, 0))
    sheet.save(f'{OUT}/sheets/{clip}.png', optimize=True)
    meta['clips'][clip] = dict(frames=n, durations=c['durs'], loop=c['loop'], source=[fr['file'] for fr in c['frames']])
json.dump(meta, open(f'{OUT}/player_anim.json', 'w'), indent=1)
print('done', len(results), 'clips')
