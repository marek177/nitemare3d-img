#!/usr/bin/env python3
from pathlib import Path
import struct, sys

def load(path):
    data = Path(path).read_bytes()
    first = struct.unpack_from("<I", data, 4)[0]
    header = bytearray(data[:first])
    entries = []
    pos = first
    while pos < len(data):
        w, h = data[pos], data[pos+1]
        if not w or not h:
            raise ValueError(f"bad dimensions at {pos:#x}")
        meta = data[pos+2:pos+10]
        raw = data[pos+10:pos+10+w*h]
        if len(raw) != w*h:
            raise ValueError("truncated")
        pix = bytearray(w*h)
        k = 0
        for x in range(w):
            for y in range(h):
                pix[y*w+x] = raw[k]
                k += 1
        entries.append([pos,w,h,bytes(meta),pix])
        pos += 10+w*h
    return header, entries

def save(header, entries):
    new_offsets=[]
    cursor=len(header)
    reloc={}
    for old,w,h,meta,pix in entries:
        new_offsets.append(cursor)
        if old:
            reloc[old]=cursor
        cursor += 10+w*h
    hdr=bytearray(header)
    for i in range(0,len(hdr)-3,4):
        v=struct.unpack_from("<I",hdr,i)[0]
        if v in reloc:
            struct.pack_into("<I",hdr,i,reloc[v])
    out=bytearray(hdr)
    for (_,w,h,meta,pix) in entries:
        out += bytes([w,h])+meta
        for x in range(w):
            for y in range(h):
                out.append(pix[y*w+x])
    return bytes(out),new_offsets

def main(paths):
    for name in paths:
        original=Path(name).read_bytes()
        h,e=load(name)
        rebuilt,_=save(h,e)
        assert rebuilt==original, f"byte roundtrip failed: {name}"
        print(f"{Path(name).name}: PASS byte-identical; images={len(e)} first={len(h):#x}")

        h2,e2=load(name)
        old_second=e2[1][0]
        old=e2[0]
        oldoff,w,hgt,meta,pix=old
        if w<255:
            nw=w+1
            np=bytearray(nw*hgt)
            for y in range(hgt):
                np[y*nw:y*nw+w]=pix[y*w:(y+1)*w]
                np[y*nw+w]=pix[y*w+w-1]
            e2[0]=[oldoff,nw,hgt,meta,np]
            changed,new_offsets=save(h2,e2)
            expected_delta=hgt
            assert new_offsets[1]==old_second+expected_delta
            tmp=Path(name).with_suffix(Path(name).suffix+".synthetic.tmp")
            tmp.write_bytes(changed)
            _,parsed=load(tmp)
            tmp.unlink()
            assert len(parsed)==len(e2)
            print(f"  relocation test: PASS (+{expected_delta} bytes before image #1)")

if __name__=="__main__":
    main(sys.argv[1:])
