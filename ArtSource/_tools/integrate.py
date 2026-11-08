"""Integrate generated in-between frames into the normalized sheets.

gen dir contains: <job>_s<seed>_rgba.png (background removed, generator resolution), jobs.json, picks.json {job: seed}
For each job: resize back to the crop box, align to neighbour frames (face template match for x/y offset + scale),
insert into the clip after frame `after`, write new sheets + meta (durations rescaled to keep clip length).
Usage: integrate.py <ArtSource dir (contains Player/, Reaper/)> <gen dir> <out ArtSource dir>
"""
import json, os, sys, shutil
import numpy as np
import cv2
from PIL import Image

ART, GEN, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
jobs = json.load(open(f"{GEN}/jobs.json"))
picks = json.load(open(f"{GEN}/picks.json"))

def load_meta(who):
    return json.load(open(f"{ART}/{who}/{who.lower()}_anim.json"))

def cells(who, clip, meta, exp=(0, 0)):
    cw, ch = meta["cell"]
    n = meta["clips"][clip]["frames"]
    sh = Image.open(f"{ART}/{who}/sheets/{clip}.png").convert("RGBA")
    out = []
    for i in range(n):
        c = sh.crop((i * cw, 0, (i + 1) * cw, ch))
        if exp != (0, 0):
            e = Image.new("RGBA", (cw + 2 * exp[0], ch + exp[1])); e.paste(c, (exp[0], exp[1])); c = e
        out.append(c)
    return out

def gray(im):
    a = np.array(im.convert("RGBA")).astype(np.float32)
    al = a[..., 3:4] / 255.
    rgb = a[..., :3] * al + 150 * (1 - al)
    return cv2.cvtColor(rgb.astype(np.uint8), cv2.COLOR_RGB2GRAY)

def head_box(im):
    """Top 30% of the visible character = head region."""
    l, t, r, b = im.getbbox()
    h = b - t
    return (l, t, r, t + int(h * 0.30))

def align(new_cell, ref_cells):
    """Find scale/offset that best matches the new frame's head to the reference heads; returns transformed cell."""
    ref = ref_cells[0]
    hb = head_box(ref)
    T = gray(ref.crop(hb))
    G = gray(new_cell)
    best = (-1, 1.0, 0, 0)
    for s in np.arange(0.6, 1.7, 0.02):
        img = cv2.resize(G, None, fx=s, fy=s, interpolation=cv2.INTER_AREA)
        if img.shape[0] < T.shape[0] or img.shape[1] < T.shape[1]:
            continue
        r = cv2.matchTemplate(img, T, cv2.TM_CCOEFF_NORMED)
        _, mv, _, ml = cv2.minMaxLoc(r)
        if mv > best[0]:
            best = (mv, s, ml[0], ml[1])
    score, s, mx, my = best
    if score < 0.7 or abs(s - 1.0) > 0.12:
        s, mx, my = 1.0, None, None
    # where the head should be: interpolated between references (mean of their match positions = their own box)
    W, H = new_cell.size
    scaled = new_cell.resize((int(W * s), int(H * s)), Image.LANCZOS)
    out = Image.new("RGBA", (W, H))
    # place so head top-left lands on reference head top-left, but keep feet on the ground line
    if mx is None:
        dx, dy = 0, 0
    else:
        dx = hb[0] - mx
        dy = hb[1] - my
    out.alpha_composite(scaled, (int(dx), int(dy))) if dx >= 0 and dy >= 0 else out.paste(scaled, (int(dx), int(dy)), scaled)
    # snap feet to the reference ground line (bottom of bbox), lifts or drops at most a few px
    ref_bottom = max(c.getbbox()[3] for c in ref_cells)
    nb = out.getbbox()[3]
    shift = ref_bottom - nb
    if abs(shift) > 0:
        o2 = Image.new("RGBA", (W, H))
        o2.paste(out, (0, shift), out)
        out = o2
    return out, round(score, 3), round(s, 2), shift

def color_match(im, refs):
    """Match mean/std per Lab channel of opaque pixels to the reference frames."""
    a = np.array(im); m = a[..., 3] > 128
    if m.sum() < 50: return im
    lab = cv2.cvtColor(a[..., :3], cv2.COLOR_RGB2LAB).astype(np.float32)
    rl = []
    for r in refs:
        ra = np.array(r); rm = ra[..., 3] > 128
        rl.append(cv2.cvtColor(ra[..., :3], cv2.COLOR_RGB2LAB).astype(np.float32)[rm])
    rl = np.concatenate(rl)
    src = lab[m]
    for c in range(3):
        sm, ss = src[:, c].mean(), src[:, c].std() + 1e-3
        tm, ts = rl[:, c].mean(), rl[:, c].std() + 1e-3
        k = 0.85  # partial transfer keeps the generated shading
        lab[..., c] = np.where(m, (lab[..., c] - sm) * (1 + k * (ts / ss - 1)) + sm + k * (tm - sm), lab[..., c])
    a[..., :3] = cv2.cvtColor(np.clip(lab, 0, 255).astype(np.uint8), cv2.COLOR_LAB2RGB)
    return Image.fromarray(a, "RGBA")

def horn_top(im):
    a = np.array(im).astype(int)
    m = (a[..., 3] > 128) & (a[..., 0] > 185) & (a[..., 1] > 160) & (a[..., 2] < 185) & (a[..., 0] - a[..., 2] > 25)
    ys = np.where(m.any(1))[0]
    return ys.min() if len(ys) else None

def rescale_about_ground(cell, s):
    W, H = cell.size
    l, t, r, b = cell.getbbox()
    cx = (l + r) / 2
    sc = cell.resize((max(1, int(W * s)), max(1, int(H * s))), Image.LANCZOS)
    out = Image.new("RGBA", (W, H))
    out.paste(sc, (int(round(cx - cx * s)), int(round(b - b * s))), sc)
    return out

LOCO = {"Run", "Reaper_Walk", "Sword_Attack", "Bow_Attack"}

def clean_alpha(im):
    a = np.array(im)
    al = a[..., 3].astype(np.float32)
    al[al < 24] = 0
    al = np.clip((al - 24) * 255.0 / (255 - 24), 0, 255)
    a[..., 3] = al.astype(np.uint8)
    # keep only the largest connected blob + anything touching it (drops stray specks)
    m = (a[..., 3] > 0).astype(np.uint8)
    n, lab, stats, _ = cv2.connectedComponentsWithStats(m, 8)
    if n > 2:
        keep = np.zeros(n, bool)
        big = 1 + np.argmax(stats[1:, cv2.CC_STAT_AREA])
        for i in range(1, n):
            if i == big or stats[i, cv2.CC_STAT_AREA] > stats[big, cv2.CC_STAT_AREA] * 0.03:
                keep[i] = True
        a[..., 3] = np.where(keep[lab], a[..., 3], 0)
    return Image.fromarray(a, "RGBA")

report = []
for who in sorted(set(j["who"] for j in jobs)):
    meta = load_meta(who)
    cw, ch = meta["cell"]
    EXP = (160, 200) if who == "Reaper" else (0, 0)   # extra px per side (x) and on top (y)
    os.makedirs(f"{OUT}/{who}/sheets", exist_ok=True)
    for clip in [c for c in meta["clips"]]:
        src = f"{ART}/{who}/sheets/{clip}.png"
        cjobs = [j for j in jobs if j["who"] == who and j["clip"] == clip and j["name"] in picks]
        base = cells(who, clip, meta, EXP)
        if not cjobs:
            n = len(base)
            sheet = Image.new("RGBA", (base[0].width * n, base[0].height))
            for i, c in enumerate(base): sheet.alpha_composite(c, (i * base[0].width, 0))
            sheet.save(f"{OUT}/{who}/sheets/{clip}.png", optimize=True)
            continue
        frames = [(c, meta["clips"][clip]["durations"][i], meta["clips"][clip]["source"][i]) for i, c in enumerate(base)]
        inserts = {}
        extra_clips = {}
        for j in cjobs:
            seed = picks[j["name"]]
            g = Image.open(f"{GEN}/{j['name']}_s{seed}_rgba.png").convert("RGBA")
            l, t, r, b = j["box"]
            g = g.resize((r - l, b - t), Image.LANCZOS)
            g = clean_alpha(g)
            cell = Image.new("RGBA", (cw + 2 * EXP[0], ch + EXP[1]))
            cell.paste(g, (l + EXP[0], t + EXP[1]), g)
            refs = [base[i] for i in j["inputs"]]
            cell = color_match(cell, refs)
            if j["after"] < 0 and who == "Reaper":
                ref_top, gen_top = horn_top(refs[0]), horn_top(cell)
                rb, gb = refs[0].getbbox()[3], cell.getbbox()[3]
                if ref_top is not None and gen_top is not None:
                    k = (rb - ref_top) / max(1, gb - gen_top)
                    k = float(np.clip(k, 0.8, 1.6))
                    # a crouched attack pose is lower than the upright walk: allow it to stay ~12% shorter
                    cell = rescale_about_ground(cell, k * 0.92)
            elif clip in LOCO:
                rh = np.mean([r.getbbox()[3] - r.getbbox()[1] for r in refs])
                gh = cell.getbbox()[3] - cell.getbbox()[1]
                cell = rescale_about_ground(cell, float(np.clip(rh / gh, 0.85, 1.18)))
            if j["after"] < 0:
                bottom = refs[0].getbbox()[3]
                sh = bottom - cell.getbbox()[3]
                c2 = Image.new("RGBA", cell.size); c2.paste(cell, (0, sh), cell); cell = c2
                sc, s = "keypose", 1.0
            else:
                cell, sc, s, sh = align(cell, refs)
            report.append(f"{j['name']} seed={seed} match={sc} scale={s} groundshift={sh}")
            if j["after"] >= 0:
                inserts.setdefault(j["after"], []).append(cell)
            else:
                extra_clips[j["name"]] = cell
        new = []
        for i, (c, d, s) in enumerate(frames):
            add = inserts.get(i, [])
            k = 1 + len(add)
            new.append((c, d / k, s))
            for a in add:
                new.append((a, d / k, "gen"))
        n = len(new)
        CW2, CH2 = new[0][0].size
        sheet = Image.new("RGBA", (CW2 * n, CH2))
        for i, (c, _, _) in enumerate(new):
            sheet.alpha_composite(c, (i * CW2, 0))
        sheet.save(f"{OUT}/{who}/sheets/{clip}.png", optimize=True)
        meta["clips"][clip]["frames"] = n
        meta["clips"][clip]["durations"] = [round(d, 4) for _, d, _ in new]
        meta["clips"][clip]["source"] = [s for _, _, s in new]
        # Reaper attack: windup -> slash -> recover(walk0)
        if extra_clips:
            seq = [extra_clips.get("Reaper_Atk_windup"), extra_clips.get("Reaper_Atk_slash")]
            seq = [c for c in seq if c is not None]
            if seq:
                seq.append(base[0])
                sheet = Image.new("RGBA", (CW2 * len(seq), CH2))
                for i, c in enumerate(seq):
                    sheet.alpha_composite(c, (i * CW2, 0))
                sheet.save(f"{OUT}/{who}/sheets/Reaper_Attack.png", optimize=True)
                meta["clips"]["Reaper_Attack"] = dict(frames=len(seq), durations=[0.5, 0.25, 0.3][:len(seq)], loop=False,
                                                     source=["gen"] * (len(seq) - 1) + ["Monster_0"])
    if EXP != (0, 0):
        meta["cell"] = [cw + 2 * EXP[0], ch + EXP[1]]
        meta["pivotPx"] = [meta["pivotPx"][0] + EXP[0], meta["pivotPx"][1]]
        meta["pivot"] = [meta["pivotPx"][0] / meta["cell"][0], meta["pivotPx"][1] / meta["cell"][1]]
    json.dump(meta, open(f"{OUT}/{who}/{who.lower()}_anim.json", "w"), indent=1)
print("\n".join(report))
