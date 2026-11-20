#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""
Dev-time builder: derives every byte edit needed to raise ETS2's light list capacity
from 480 to N (default 500) and applies them to a COPY of eurotrucks2.exe (v1.62.3.3).

Why more than a few immediates: the light pipeline (RVA 0x14d1060 and helper 0x14cfac0)
uses containers with *inline stack storage* for 480 elements whose "grow" is only
`n <= capacity`.  Above 480 they return NULL -> crash (0x14cf29b).  So the storage itself
must grow.  We do this without moving code:
  * frames get a free "hole" right above the old locals (rbp and rsp both shift down by the
    same amount), the new storages live in the hole, caller-area refs get +hole,
  * the 0xC0-element array is extended downward into the vacated old storage.
Requires capstone + pefile (pip install capstone pefile).

usage: python build_light500.py --exe <orig.exe> --out <patched.exe> [--limit 500] [--json edits.json]
"""
import argparse, json, struct, sys
import pefile
from capstone import *
from capstone.x86 import *

OLD = 480

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--exe", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--limit", type=int, default=500)
    ap.add_argument("--json")
    a = ap.parse_args()
    N = a.limit
    assert OLD < N <= 2000
    raw = open(a.exe, "rb").read()
    pe = pefile.PE(a.exe, fast_load=False)
    text = [s for s in pe.sections if s.Name.startswith(b".text")][0]
    base, tp = text.VirtualAddress, text.PointerToRawData
    t = raw[tp:tp + text.SizeOfRawData]
    md = Cs(CS_ARCH_X86, CS_MODE_64); md.detail = True
    edits = {}   # file offset -> (old4, new4, note)

    def foff(rva): return tp + (rva - base)

    def put(file_off, size, old_val, new_val, note, signed=True):
        fmt = {4: "<i" if signed else "<I", 2: "<H"}[size]
        old = struct.pack(fmt, old_val); new = struct.pack(fmt, new_val)
        cur = raw[file_off:file_off + size]
        if cur != old:
            sys.exit(f"mismatch at file 0x{file_off:x} ({note}): have {cur.hex()} want {old.hex()}")
        if file_off in edits and edits[file_off][1] != new:
            sys.exit(f"conflicting edit at 0x{file_off:x}")
        edits[file_off] = (old, new, note)

    def ins_list(lo, hi):
        return list(md.disasm(t[lo - base:hi - base], lo))

    def patch_disp(i, new_disp, note):
        assert i.disp_size == 4, (hex(i.address), i.disp_size)
        put(foff(i.address) + i.disp_offset, 4, i.disp, new_disp, f"{i.address:x} {i.mnemonic} {i.op_str}: {note}")

    def patch_imm(i, old_imm, new_imm, note):
        assert i.imm_size in (4, 8), (hex(i.address), i.imm_size)
        put(foff(i.address) + i.imm_offset, 4, old_imm, new_imm, f"{i.address:x} {i.mnemonic} {i.op_str}: {note}")

    def mem(i):
        for op in i.operands:
            if op.type == X86_OP_MEM: return op.mem
        return None

    # ------------------------------------------------------------------ geometry
    # ---- F = 0x14d1060 .. 0x14d2020
    F_LO, F_HI = 0x14d1060, 0x14d2020
    F_OLD_FRAME, F_TOP = 0x179a8, 0x178a8
    OLD_C1_END = 0x17820                    # end of the old 480 x 0xC0 array
    if N <= 500:
        # strategy A: C1 grows downward into the vacated old C2/C3 area, C2/C3 go to the hole
        HOLE_F = 0x1000
        c3_store = F_TOP + 8            # 0x178b0  (N u32)
        c2_hdr = (c3_store + N * 4 + 15) & ~15
        c2_store = c2_hdr + 0x20
        assert c2_store + N * 4 <= F_TOP + HOLE_F, "hole too small"
        c1_base = 0x1020 - (N - OLD) * 0xC0
        assert c1_base % 16 == 0 and c1_base >= 0x120, hex(c1_base)
    else:
        # strategy B: C1 lives completely in the hole; C2/C3 move into the vacated old C1 area
        c1_base = F_TOP + 8
        HOLE_F = (c1_base + N * 0xC0 - F_TOP + 15) & ~15
        c3_store = 0x1040
        c2_hdr = (c3_store + N * 4 + 15) & ~15
        c2_store = c2_hdr + 0x20
        assert c2_store + N * 4 <= OLD_C1_END, "old C1 area too small"
    HOLE = HOLE_F
    HOLE_H = (0x10 + N * 8 + 0xFF) & ~0xFF       # helper frame hole (C4: N x 8 bytes)
    c1_fill = c1_base + 0x20
    print(f"layout: N={N} HOLE_F=0x{HOLE_F:x} HOLE_H=0x{HOLE_H:x} C1@0x{c1_base:x} C3@0x{c3_store:x} C2hdr@0x{c2_hdr:x} C2@0x{c2_store:x}")
    remap_c2 = {0x880: c2_hdr, 0x888: c2_hdr + 8, 0x890: c2_hdr + 0x10, 0x898: c2_hdr + 0x18, 0x8a0: c2_store}

    fins = ins_list(F_LO, F_HI)
    handled = set()
    for i in fins:
        m = mem(i)
        if m is None: continue
        if m.base == X86_REG_RBP and m.index == 0:
            d = m.disp
            if d in remap_c2 and d != 0x8a0 or (d == 0x8a0 and i.address == 0x14d17b3):
                patch_disp(i, remap_c2[d], "C2 relocate"); handled.add(i.address)
            elif i.address == 0x14d183e and d == 0x100:
                patch_disp(i, c3_store, "C3 storage ptr -> hole"); handled.add(i.address)
            elif i.address == 0x14d160a and d == 0x1020:
                patch_disp(i, c1_base, "C1 base"); handled.add(i.address)
            elif i.address == 0x14d15c4 and d == 0x1040:
                patch_disp(i, c1_fill, "C1 zero-fill base"); handled.add(i.address)
            elif d >= F_TOP + 0x20:
                patch_disp(i, d + HOLE, "caller-area ref +hole"); handled.add(i.address)
            elif 0x100 <= d < F_TOP:
                print("UNHANDLED rbp-ref in F:", hex(i.address), i.mnemonic, i.op_str)
        elif m.base == X86_REG_RBP and m.index != 0 and 0x100 <= m.disp < F_TOP + 0x20:
            print("UNHANDLED indexed rbp-ref in F:", hex(i.address), i.mnemonic, i.op_str)
        elif m.base == X86_REG_RSP and m.disp >= F_OLD_FRAME + 8:
            print("rsp-ref above old frame in F (check!):", hex(i.address), i.mnemonic, i.op_str)

    FRAME_NEW = F_OLD_FRAME + HOLE
    for i in fins:
        if i.address == 0x14d107a:                         # lea rbp,[rsp-0x178a8]
            assert i.mnemonic == "lea" and mem(i).disp == -F_TOP
            patch_disp(i, -(F_TOP + HOLE), "rbp shifts with hole")
        elif i.address == 0x14d1082:                       # mov eax,0x179a8
            patch_imm(i, F_OLD_FRAME, FRAME_NEW, "alloc size")
        elif i.mnemonic == "add" and i.op_str == f"rsp, {F_OLD_FRAME:#x}":
            patch_imm(i, F_OLD_FRAME, FRAME_NEW, "epilogue add rsp")
    n_epi = sum(1 for k, v in edits.items() if "epilogue add rsp" in v[2])
    print("F epilogues patched:", n_epi)

    # immediates / disps that encode the light capacity in F
    OFF = lambda ad: next(x for x in fins if x.address == ad)
    patch_imm(OFF(0x14d1197), OLD, N, "limit check")
    patch_imm(OFF(0x14d11a4), OLD, N, "limit arg for log")
    patch_imm(OFF(0x14d11c5), OLD, N, "reserve N lights")
    patch_disp(OFF(0x14d11d3), N * 0x7C, "end of N lights")
    patch_imm(OFF(0x14d1258), -(OLD + 1), -(N + 1), "append overflow check")
    patch_disp(OFF(0x14d1264), N, "append N data entries")
    patch_disp(OFF(0x14d12e6), N * 0xC0, "end of N data entries")
    patch_imm(OFF(0x14d13b0), OLD, N, "second limit check")
    patch_imm(OFF(0x14d15be), OLD, N, "zero-fill rows")
    patch_imm(OFF(0x14d1619), OLD, N, "C1 capacity")
    patch_imm(OFF(0x14d17e6), OLD, N, "C2 capacity")
    patch_imm(OFF(0x14d1867), OLD, N, "C3 capacity")
    patch_imm(OFF(0x14d187a), OLD, N, "C3 cmp capacity")

    # ---- helper 0x14cfac0 (C4: N x 8 bytes)
    H_LO, H_HI = 0x14cfac0, 0x14d01d5
    H_OLD_FRAME, H_TOP = 0x17d0, 0x16d0
    c4_store = H_TOP + 0x10
    assert c4_store + N * 8 <= H_TOP + HOLE_H
    hins = ins_list(H_LO, H_HI)
    for i in hins:
        m = mem(i)
        if i.address == 0x14cfad0:
            assert mem(i).disp == -H_TOP; patch_disp(i, -(H_TOP + HOLE_H), "rbp shifts with hole")
        elif i.address == 0x14cfad8:
            patch_imm(i, H_OLD_FRAME, H_OLD_FRAME + HOLE_H, "alloc size")
        elif i.address == 0x14cfae9:
            assert m.disp == 0x7a0; patch_disp(i, c4_store, "C4 storage ptr -> hole")
        elif i.address == 0x14cfb43:
            patch_imm(i, OLD, N, "C4 capacity")
        elif m is not None and m.base == X86_REG_RBP and m.disp >= H_TOP + 0x38:
            patch_disp(i, m.disp + HOLE_H, "caller-area ref +hole")
        elif m is not None and m.base == X86_REG_RSP and m.disp == H_OLD_FRAME and i.mnemonic == "lea":
            patch_disp(i, H_OLD_FRAME + HOLE_H, "epilogue base")
        elif m is not None and m.base == X86_REG_RSP and m.disp in (0x17a0, 0x17b0, 0x17c0) and i.mnemonic == "movaps":
            # saves of xmm6-8 are restored relative to the frame top (r11), so they must move up with it
            patch_disp(i, m.disp + HOLE_H, "xmm save follows frame top")
        elif m is not None and m.base == X86_REG_RSP and m.disp > H_OLD_FRAME:
            print("rsp-ref above old frame in helper (check!):", hex(i.address), i.mnemonic, i.op_str)
        elif m is not None and m.base == X86_REG_RBP and 0x7a0 <= m.disp < 0x16a0:
            print("UNHANDLED helper rbp-ref in C4 storage:", hex(i.address), i.mnemonic, i.op_str)

    # ---- unwind info: ALLOC_LARGE16 values
    def patch_alloc(unwind_rva, old, new, shift_saves=False, hole=0):
        o = pe.get_offset_from_rva(unwind_rva)
        nc = raw[o + 2]
        codes = raw[o + 4:o + 4 + 2 * nc]
        k = 0; found = False
        while k < nc:
            opi = codes[2 * k + 1]; op = opi & 15; info = opi >> 4
            if op == 1 and info == 0:
                v = struct.unpack_from("<H", codes, 2 * (k + 1))[0]
                if v * 8 == old:
                    put(o + 4 + 2 * (k + 1), 2, v, new // 8, f"unwind 0x{unwind_rva:x} ALLOC_LARGE", signed=False)
                    found = True
                k += 2
            elif op == 1: k += 3
            elif op == 4:   # SAVE_NONVOL: offset/8 relative to final rsp; caller-area saves move with the frame top
                v = struct.unpack_from("<H", codes, 2 * (k + 1))[0]
                if shift_saves:
                    put(o + 4 + 2 * (k + 1), 2, v, v + hole // 8, f"unwind SAVE_NONVOL reg{info}", signed=False)
                k += 2
            elif op == 8:   # SAVE_XMM128: offset/16
                v = struct.unpack_from("<H", codes, 2 * (k + 1))[0]
                if shift_saves:
                    put(o + 4 + 2 * (k + 1), 2, v, v + hole // 16, f"unwind SAVE_XMM128 xmm{info}", signed=False)
                k += 2
            elif op in (5, 9): k += 3
            else: k += 1
        if not found:
            sys.exit(f"ALLOC_LARGE {old} not found in unwind {unwind_rva:x}")
    patch_alloc(0x26a13b0, F_OLD_FRAME, FRAME_NEW)          # F: saves are rsp-relative in code too -> unchanged
    patch_alloc(0x26a131c, H_OLD_FRAME, H_OLD_FRAME + HOLE_H, shift_saves=True, hole=HOLE_H)

    # ------------------------------------------------------------------ apply
    out = bytearray(raw)
    for off, (old, new, note) in sorted(edits.items()):
        out[off:off + len(new)] = new
    open(a.out, "wb").write(out)
    print(f"{len(edits)} edits, written {a.out}")
    if a.json:
        json.dump([{"file_off": off, "old": o.hex(), "new": n.hex(), "note": note}
                   for off, (o, n, note) in sorted(edits.items())], open(a.json, "w"), indent=1)
    for off, (o, n, note) in sorted(edits.items()):
        print(f"  0x{off:08x} {o.hex()} -> {n.hex()}  {note}")

if __name__ == "__main__":
    main()
