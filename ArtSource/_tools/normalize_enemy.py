"""Normalize boss and monster frames onto one canvas per character.
mode 'head': anchor = head centre of first frame of each clip (keeps in-clip float/bob)
mode 'facefeet': per-frame, y = bbox bottom, x = head centre
"""
import json, os, sys
import numpy as np
from PIL import Image

SRC, OUT = sys.argv[1], sys.argv[2]
fb = json.load(open('/home/claude/rev/face_boss.json')); fm = json.load(open('/home/claude/rev/face_mon.json'))

CHARS = {
 'Boss': dict(target=1.0, mode='head', face=fb, clips={
    'Boss_Float':  (['Boss/Walk/Walk_0%d' % i for i in (1, 2, 3, 4, 2)], [0.17] * 5, 1.0, True),
    'Boss_Cast':   (['Boss/Attack_0/Attack_0%d' % i for i in (1, 2, 3, 4)], [0.13, 0.09, 0.12, 0.2], 0.72, False),
    'Boss_Death':  (['Boss/Death/boss_death_%02d' % i for i in range(1, 16)], [0.08] * 15, 0.90, False)}),
 'Reaper': dict(target=0.8, mode='facefeet', face=fm, clips={
    'Reaper_Walk': (['Monster/Monster_0', 'Monster/Moster_1', 'Monster/Monster_2', 'Monster/Monster_3'], [0.16] * 4, 1.0, True)}),
}

for ch, cfg in CHARS.items():
    items = {}
    for clip, (files, durs, s, loop) in cfg['clips'].items():
        k = cfg['target'] / s; fr = []
        first = cfg['face'].get(files[0] + '.png')
        for f in files:
            im = Image.open(f'{SRC}/{f}.png').convert('RGBA')
            a = np.array(im)[..., 3] > 20
            info = cfg['face'].get(f + '.png')
            if cfg['mode'] == 'head':
                ax, ay = first['cx'], first['cy']
            else:
                ys, _ = np.where(a); ax, ay = info['cx'], ys.max() + 1
            fr.append((f, im, k, ax * k, ay * k))
        items[clip] = (fr, durs, loop)
    L = R = U = D = 0
    for fr, _, _ in items.values():
        for _, im, k, ax, ay in fr:
            bb = im.getbbox()
            if not bb: continue
            L = max(L, ax - bb[0] * k); R = max(R, bb[2] * k - ax); U = max(U, ay - bb[1] * k); D = max(D, bb[3] * k - ay)
    half = int(np.ceil(max(L, R) / 16) * 16) + 8
    CW = 2 * half; below = int(np.ceil(D / 8) * 8) + 8; CH = int(np.ceil((U + below) / 16) * 16) + 8
    AX, AY = CW // 2, CH - below
    meta = dict(cell=[CW, CH], pivotPx=[AX, below], pivot=[AX / CW, below / CH], pixelsPerUnit=256, clips={})
    os.makedirs(f'{OUT}/{ch}/sheets', exist_ok=True)
    for clip, (fr, durs, loop) in items.items():
        sheet = Image.new('RGBA', (CW * len(fr), CH))
        for i, (f, im, k, ax, ay) in enumerate(fr):
            r = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
            sheet.alpha_composite(r, (int(round(i * CW + AX - ax)), int(round(AY - ay))))
        sheet.save(f'{OUT}/{ch}/sheets/{clip}.png', optimize=True)
        meta['clips'][clip] = dict(frames=len(fr), durations=durs, loop=loop, source=[f for f, *_ in fr])
    json.dump(meta, open(f'{OUT}/{ch}/{ch.lower()}_anim.json', 'w'), indent=1)
    print(ch, 'canvas', CW, CH, 'pivot', meta['pivot'])
