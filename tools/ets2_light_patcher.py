#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
r"""
ETS2 light limit patcher -  raises the light list capacity of eurotrucks2.exe (480 -> N).

Target: Euro Truck Simulator 2 v1.62.3.3 (eurotrucks2.exe, 53,747,600 bytes). No dependencies
(Python 3 standard library only).

Why it is more than changing a number: the renderer's light pipeline keeps its lists in
containers with fixed inline stack storage for 480 entries; above that they return NULL and the
game crashes. The patcher therefore also enlarges those storages (new space in the stack frames of
the two functions involved, 71 byte edits in total, no code is moved) and fixes the unwind info.

Usage (examples):
  python ets2_light_patcher.py --info                        show state of the exe (auto-detects Steam path)
  python ets2_light_patcher.py --limit 600                   write eurotrucks2_light600.exe next to the exe
  python ets2_light_patcher.py --limit 500 --inplace         patch eurotrucks2.exe itself (creates eurotrucks2.exe.bak first)
  python ets2_light_patcher.py --restore                     put eurotrucks2.exe.bak back
  python ets2_light_patcher.py --exe D:\path\eurotrucks2.exe --limit 550 --out D:\tmp\x.exe

An already patched exe can be re-patched to another limit; limit 480 restores the original bytes.
Confirmed in game: 500 and 600; larger values are untested.
Not for multiplayer (TruckersMP) - the exe is modified. Back up your exe (--inplace does it for you).
"""
import argparse, os, shutil, struct, subprocess, sys

ORIG = 480
F_OLD_FRAME, F_TOP = 0x179A8, 0x178A8
H_OLD_FRAME, H_TOP = 0x17D0, 0x16D0
OLD_C1_END = 0x17820
EXPECT_SIZE = 53747600

# (field RVA, size in bytes, kind, value in the ORIGINAL exe)
TABLE = [
    (0x14cfad4, 4, 'H_RBP', -5840),
    (0x14cfad9, 4, 'H_ALLOC', 6096),
    (0x14cfaec, 4, 'C4PTR', 1952),
    (0x14cfb0c, 4, 'H_XMM', 6080),
    (0x14cfb22, 4, 'H_XMM', 6064),
    (0x14cfb35, 4, 'H_XMM', 6048),
    (0x14cfb4a, 4, 'CNT', 480),
    (0x14cfb73, 4, 'H_CALLER', 5920),
    (0x14cfb7a, 4, 'H_CALLER', 5928),
    (0x14cfb91, 4, 'H_CALLER', 5920),
    (0x14cfbd9, 4, 'H_CALLER', 5928),
    (0x14cfd24, 4, 'H_CALLER', 5920),
    (0x14cfd2a, 4, 'H_CALLER', 5920),
    (0x14cfdcc, 4, 'H_CALLER', 5920),
    (0x14cfdd7, 4, 'H_CALLER', 5920),
    (0x14cffbd, 4, 'H_EPI', 6096),
    (0x14d107e, 4, 'F_RBP', -96424),
    (0x14d1083, 4, 'F_ALLOC', 96680),
    (0x14d1092, 4, 'F_CALLER', 96512),
    (0x14d10ea, 4, 'F_CALLER', 96496),
    (0x14d10fd, 4, 'F_ALLOC', 96680),
    (0x14d1193, 4, 'F_CALLER', 96512),
    (0x14d119a, 4, 'CNT', 480),
    (0x14d11a6, 4, 'CNT', 480),
    (0x14d11c6, 4, 'CNT', 480),
    (0x14d11d6, 4, 'LIGHTB', 59520),
    (0x14d124d, 4, 'F_CALLER', 96504),
    (0x14d125a, 4, 'NEGCNT', -481),
    (0x14d1267, 4, 'CNT', 480),
    (0x14d12e9, 4, 'DATAB', 92160),
    (0x14d13a1, 4, 'F_CALLER', 96512),
    (0x14d13a8, 4, 'F_CALLER', 96488),
    (0x14d13b3, 4, 'CNT', 480),
    (0x14d13e7, 4, 'F_CALLER', 96504),
    (0x14d15ac, 4, 'F_CALLER', 96512),
    (0x14d15c0, 4, 'CNT', 480),
    (0x14d15c7, 4, 'C1FILL', 4160),
    (0x14d160d, 4, 'C1BASE', 4128),
    (0x14d161e, 4, 'CNT', 480),
    (0x14d1714, 4, 'F_CALLER', 96496),
    (0x14d176b, 4, 'F_CALLER', 96496),
    (0x14d17b6, 4, 'C2', 2208),
    (0x14d17bd, 4, 'C2', 2192),
    (0x14d17cf, 4, 'C2', 2184),
    (0x14d17db, 4, 'C2', 2176),
    (0x14d17e2, 4, 'C2', 2176),
    (0x14d17e9, 4, 'C2', 2200),
    (0x14d17ed, 4, 'CNT', 480),
    (0x14d1805, 4, 'F_CALLER', 96488),
    (0x14d1820, 4, 'C2', 2176),
    (0x14d1841, 4, 'C3PTR', 256),
    (0x14d186e, 4, 'CNT', 480),
    (0x14d187d, 4, 'CNT', 480),
    (0x14d18b3, 4, 'C2', 2192),
    (0x14d18c0, 4, 'C2', 2184),
    (0x14d19cd, 4, 'F_CALLER', 96512),
    (0x14d1a0f, 4, 'F_CALLER', 96512),
    (0x14d1a16, 4, 'F_CALLER', 96480),
    (0x14d1aec, 4, 'F_CALLER', 96472),
    (0x14d1b75, 4, 'F_CALLER', 96464),
    (0x14d1b8a, 4, 'C2', 2192),
    (0x14d1ba1, 4, 'C2', 2176),
    (0x14d1bad, 4, 'C2', 2184),
    (0x14d1e4b, 4, 'F_ALLOC', 96680),
    (0x14d1f97, 4, 'C2', 2176),
    (0x26a1322, 2, 'U_XMM_H', 378),
    (0x26a1326, 2, 'U_XMM_H', 379),
    (0x26a132a, 2, 'U_XMM_H', 380),
    (0x26a132e, 2, 'U_NV_H', 770),
    (0x26a1332, 2, 'U_ALLOC_H', 762),
    (0x26a13b6, 2, 'U_ALLOC_F', 12085),
]

def layout(n):
    if n <= 500:                       # C1 grows downward into the vacated old C2/C3 area
        hole_f = 0x1000
        c3 = F_TOP + 8
        c2h = (c3 + n * 4 + 15) & ~15
        c1 = 0x1020 - (n - ORIG) * 0xC0
    else:                              # C1 lives completely in the new hole
        c1 = F_TOP + 8
        hole_f = (c1 + n * 0xC0 - F_TOP + 15) & ~15
        c3 = 0x1040
        c2h = (c3 + n * 4 + 15) & ~15
        assert c2h + 0x20 + n * 4 <= OLD_C1_END
    hole_h = (0x10 + n * 8 + 0xFF) & ~0xFF
    return dict(hole_f=hole_f, hole_h=hole_h, c1=c1, c3=c3, c2h=c2h)

def value(kind, orig, n):
    """value a site must have when the exe is patched for limit n (orig = value in the original exe)"""
    if n == ORIG:
        return orig
    L = layout(n); hf, hh = L["hole_f"], L["hole_h"]
    return {
        "F_RBP": -(F_TOP + hf), "F_ALLOC": F_OLD_FRAME + hf,
        "H_RBP": -(H_TOP + hh), "H_ALLOC": H_OLD_FRAME + hh,
        "F_CALLER": orig + hf, "H_CALLER": orig + hh,
        "C2": L["c2h"] + (orig - 0x880),
        "C3PTR": L["c3"], "C1BASE": L["c1"], "C1FILL": L["c1"] + 0x20,
        "C4PTR": H_TOP + 0x10,
        "CNT": n, "NEGCNT": -(n + 1), "LIGHTB": n * 0x7C, "DATAB": n * 0xC0,
        "H_EPI": H_OLD_FRAME + hh, "H_XMM": orig + hh,
        "U_ALLOC_F": (F_OLD_FRAME + hf) // 8, "U_ALLOC_H": (H_OLD_FRAME + hh) // 8,
        "U_NV_H": orig + hh // 8, "U_XMM_H": orig + hh // 16,
    }[kind]

def sections(d):
    pe = struct.unpack_from("<I", d, 0x3C)[0]
    nsec = struct.unpack_from("<H", d, pe + 6)[0]
    opt = struct.unpack_from("<H", d, pe + 20)[0]
    out = []
    for i in range(nsec):
        o = pe + 24 + opt + 40 * i
        vsz, va, rsz, raw = struct.unpack_from("<IIII", d, o + 8)
        out.append((va, max(vsz, rsz), raw))
    return out

def rva2off(secs, rva):
    for va, sz, raw in secs:
        if va <= rva < va + sz:
            return raw + rva - va
    raise ValueError(f"RVA 0x{rva:x} not in any section")

def rd(d, off, size, kind):
    signed = size == 4
    return int.from_bytes(d[off:off + size], "little", signed=signed)

def wr(d, off, size, v):
    d[off:off + size] = int(v).to_bytes(size, "little", signed=(v < 0 or size == 4))

def detect(d, secs):
    """returns (state, limit): ('original',480) / ('patched',N) / ('unknown',None)"""
    cur = None
    for rva, size, kind, orig in TABLE:
        if kind == "CNT":
            cur = rd(d, rva2off(secs, rva), size, kind)
            break
    if cur is None:
        return "unknown", None
    candidates = [cur] if cur != ORIG else [ORIG]
    for n in candidates:
        if all(rd(d, rva2off(secs, r), s, k) == value(k, o, n) for r, s, k, o in TABLE):
            return ("original" if n == ORIG else "patched"), n
    return "unknown", None

def apply(d, secs, n):
    for rva, size, kind, orig in TABLE:
        wr(d, rva2off(secs, rva), size, value(kind, orig, n))

def find_exe():
    rel = os.path.join("Euro Truck Simulator 2", "bin", "win_x64", "eurotrucks2.exe")
    roots = ["Steam\\steamapps\\common", "SteamLibrary\\steamapps\\common",
             "Program Files (x86)\\Steam\\steamapps\\common", "Program Files\\Steam\\steamapps\\common",
             "Games\\Steam\\steamapps\\common", ""]
    for drv in "CDEFGHIJKLMNOPQRSTUVWXYZ":
        for r in roots:
            p = os.path.join(drv + ":\\", r, rel)
            if os.path.isfile(p):
                return p
    return None

def game_running():
    try:
        out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq eurotrucks2.exe"], capture_output=True, text=True).stdout
        return "eurotrucks2.exe" in out.lower()
    except Exception:
        return False

def main():
    ap = argparse.ArgumentParser(description="ETS2 light limit patcher (v1.62.3.3)")
    ap.add_argument("--exe", help="path to eurotrucks2.exe (default: auto-detect Steam install)")
    ap.add_argument("--limit", type=int, default=600, help="new light capacity (default 600; 480 = original)")
    ap.add_argument("--out", help="output file (default: eurotrucks2_light<N>.exe next to the exe)")
    ap.add_argument("--inplace", action="store_true", help="patch eurotrucks2.exe itself, backup to .bak first")
    ap.add_argument("--info", action="store_true", help="only show the state of the exe")
    ap.add_argument("--restore", action="store_true", help="copy eurotrucks2.exe.bak back over eurotrucks2.exe")
    a = ap.parse_args()

    exe = a.exe or find_exe()
    if not exe or not os.path.isfile(exe):
        sys.exit("eurotrucks2.exe not found - pass --exe <path>")
    print("exe:", exe)

    if a.restore:
        bak = exe + ".bak"
        if not os.path.isfile(bak):
            sys.exit("no .bak found")
        if game_running():
            sys.exit("ETS2 is running - close it first")
        shutil.copyfile(bak, exe)
        print("restored from", bak)
        return

    d = bytearray(open(exe, "rb").read())
    secs = sections(d)
    state, cur = detect(d, secs)
    if len(d) != EXPECT_SIZE and state == "unknown":
        print(f"note: size {len(d)} differs from the supported build ({EXPECT_SIZE})")
    if state == "unknown":
        sys.exit("exe is not the supported ETS2 1.62.3.3 build (or was modified differently) - not patching.\n"
                 "Update the game files / use the .bak original, or ask for an updated patcher.")
    print(f"state: {state}, current light limit: {cur}")
    if a.info:
        return

    n = a.limit
    if not ORIG <= n <= 2000:
        sys.exit("limit must be 480..2000")
    if n == cur:
        sys.exit(f"already at limit {n} - nothing to do")
    if n > 511:
        print("note: limits above 500 were only tested up to 600; large frames need stack (limit 600 = ~207 KB).")

    # restore original bytes first (so that any patched state can be re-patched), then apply
    if state == "patched":
        for rva, size, kind, orig in TABLE:
            wr(d, rva2off(secs, rva), size, orig)
    apply(d, secs, n)
    st2, cur2 = detect(d, secs)
    assert st2 in ("patched", "original") and cur2 == n, "self-check failed"

    if a.inplace:
        if game_running():
            sys.exit("ETS2 is running - close it first")
        bak = exe + ".bak"
        if os.path.exists(bak):
            print("backup exists, keeping:", bak)
        else:
            if state != "original":
                sys.exit("exe is already patched and no .bak exists - refusing (would 'back up' a patched file)")
            shutil.copyfile(exe, bak)
            print("backup written:", bak)
        out = exe
    else:
        out = a.out or os.path.join(os.path.dirname(exe), f"eurotrucks2_light{n}.exe")
        if os.path.abspath(out) == os.path.abspath(exe):
            sys.exit("--out equals the input; use --inplace")
        if os.path.exists(out) and not a.out:
            print("overwriting previous", out)
    tmp = out + ".tmp"
    open(tmp, "wb").write(d)
    os.replace(tmp, out)
    print(f"written: {out}  (limit {cur} -> {n}, {len(TABLE)} edits)")
    if not a.inplace:
        print("start this exe directly from the same folder (needs steam running), or use --inplace.")

if __name__ == "__main__":
    main()
