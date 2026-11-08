"""Trim normalized frames and shelf-pack them into atlas pages, keeping the feet anchor as a per-sprite pivot.
Output per character: <name>_atlas_<n>.png + <name>_atlas.json
json: {ppu, pages:[file..], clips:{clip:{loop, durations, frames:[{page,x,y,w,h,px,py}]}}}
 x,y,w,h in Unity sprite rect coordinates (origin bottom-left); px,py pivot normalized within rect.
"""
import json, os, sys
from PIL import Image

def frames_from_sheet(sheet_path, n, cw, ch):
    sh = Image.open(sheet_path).convert('RGBA')
    return [sh.crop((i * cw, 0, (i + 1) * cw, ch)) for i in range(n)]

def pack(name, meta, sheet_dir, out_dir, ppu, page_size=4096, pad=3):
    cw, ch = meta['cell']; ax = meta['pivotPx'][0]; ay_from_bottom = meta['pivotPx'][1]
    items = []  # (clip, idx, img, pivot_x_in_trim, pivot_y_from_bottom_in_trim)
    for clip, c in meta['clips'].items():
        for i, cell in enumerate(frames_from_sheet(os.path.join(sheet_dir, clip + '.png'), c['frames'], cw, ch)):
            bb = cell.getbbox() or (0, 0, 2, 2)
            l, t, r, b = bb[0] - 1, bb[1] - 1, bb[2] + 1, bb[3] + 1
            l, t = max(l, 0), max(t, 0); r, b = min(r, cw), min(b, ch)
            img = cell.crop((l, t, r, b))
            px = (ax - l) / img.width
            py = ((ch - ay_from_bottom) - t)  # anchor y from top within trim
            py = (img.height - py) / img.height
            items.append((clip, i, img, px, py))
    order = sorted(range(len(items)), key=lambda k: -items[k][2].height)
    pages = []; placements = {}
    def new_page():
        pages.append(dict(img=Image.new('RGBA', (page_size, page_size)), x=pad, y=pad, shelf=0)); return pages[-1]
    page = new_page()
    for k in order:
        img = items[k][2]
        if page['x'] + img.width + pad > page_size:
            page['y'] += page['shelf'] + pad; page['x'] = pad; page['shelf'] = 0
        if page['y'] + img.height + pad > page_size:
            page = new_page()
        page['img'].alpha_composite(img, (page['x'], page['y']))
        placements[k] = (len(pages) - 1, page['x'], page['y'])
        page['x'] += img.width + pad; page['shelf'] = max(page['shelf'], img.height)
    files = []
    for pi, p in enumerate(pages):
        used_h = p['y'] + p['shelf'] + pad
        hh = 1
        while hh < used_h: hh *= 2
        im = p['img'].crop((0, 0, page_size, min(page_size, hh)))
        fn = f'{name}_atlas_{pi}.png'; im.save(os.path.join(out_dir, fn), optimize=True); files.append((fn, im.height))
    out = dict(ppu=ppu, pages=[f for f, _ in files], clips={})
    for k, (clip, i, img, px, py) in enumerate(items):
        pi, x, y = placements[k]; ph = files[pi][1]
        c = out['clips'].setdefault(clip, dict(loop=meta['clips'][clip]['loop'], durations=meta['clips'][clip]['durations'], frames=[]))
        c['frames'].append(dict(page=pi, x=x, y=ph - y - img.height, w=img.width, h=img.height, px=round(px, 5), py=round(py, 5)))
    json.dump(out, open(os.path.join(out_dir, f'{name}_atlas.json'), 'w'), indent=1)
    print(name, 'pages', [(f, h) for f, h in files], 'frames', len(items))

if __name__ == '__main__':
    root, dst = sys.argv[1], sys.argv[2]
    os.makedirs(dst, exist_ok=True)
    pack('Player', json.load(open(f'{root}/Player/player_anim.json')), f'{root}/Player/sheets', dst, 200)
    pack('Boss', json.load(open(f'{root}/Boss/boss_anim.json')), f'{root}/Boss/sheets', dst, 130, 4096)
    pack('Reaper', json.load(open(f'{root}/Reaper/reaper_anim.json')), f'{root}/Reaper/sheets', dst, 180, 2048)
