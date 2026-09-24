#!/usr/bin/env python3
# Purpose: Implements p2 m ed ia c om pa re tooling used by repository verification and development workflows.
import argparse, json, math, os, struct, subprocess, tempfile, wave, zlib, binascii
from pathlib import Path

def paeth(a,b,c):
    p=a+b-c; pa=abs(p-a); pb=abs(p-b); pc=abs(p-c)
    return a if pa<=pb and pa<=pc else b if pb<=pc else c

def read_png(path):
    data=Path(path).read_bytes(); sig=b'\x89PNG\r\n\x1a\n'
    if not data.startswith(sig): raise ValueError('not PNG')
    pos=8; idat=b''; w=h=ct=bd=None
    while pos<len(data):
        ln=struct.unpack('>I',data[pos:pos+4])[0]; typ=data[pos+4:pos+8]; body=data[pos+8:pos+8+ln]; pos+=12+ln
        if typ==b'IHDR': w,h,bd,ct,_,_,_=struct.unpack('>IIBBBBB',body)
        elif typ==b'IDAT': idat+=body
        elif typ==b'IEND': break
    if bd!=8 or ct not in (2,6): raise ValueError('only 8-bit RGB/RGBA PNG supported')
    chans=3 if ct==2 else 4; stride=w*chans; raw=zlib.decompress(idat); rows=[]; prev=bytearray(stride); off=0
    for _ in range(h):
        f=raw[off]; off+=1; scan=bytearray(raw[off:off+stride]); off+=stride
        for i in range(stride):
            a=scan[i-chans] if i>=chans else 0; b=prev[i]; c=prev[i-chans] if i>=chans else 0
            if f==1: scan[i]=(scan[i]+a)&255
            elif f==2: scan[i]=(scan[i]+b)&255
            elif f==3: scan[i]=(scan[i]+((a+b)//2))&255
            elif f==4: scan[i]=(scan[i]+paeth(a,b,c))&255
            elif f!=0: raise ValueError('unsupported PNG filter')
        rows.append(bytes(scan)); prev=scan
    return w,h,chans,b''.join(rows)

def png_diff(a,b,out):
    wa,ha,ca,aa=read_png(a); wb,hb,cb,bb=read_png(b)
    if (wa,ha,ca)!=(wb,hb,cb): return 100.0, {'method':'png-pixel','sizeMismatch':True}
    pixels=wa*ha; changed=0; total_abs=0; diff=bytearray()
    for i in range(pixels):
        ia=i*ca; ds=[abs(aa[ia+j]-bb[ia+j]) for j in range(3)]; total_abs+=sum(ds)
        if max(ds)>8: changed+=1
        diff.extend((min(255,ds[0]*4),min(255,ds[1]*4),min(255,ds[2]*4)))
    Path(out).write_bytes(f'P6\n{wa} {ha}\n255\n'.encode()+diff)
    return changed*100.0/max(1,pixels), {'method':'png-pixel','meanAbsoluteChannelError':total_abs/max(1,pixels*3),'changedPixels':changed,'pixels':pixels}

def wav_diff(a,b):
    with wave.open(a,'rb') as x, wave.open(b,'rb') as y:
        if (x.getnchannels(),x.getsampwidth(),x.getframerate())!=(y.getnchannels(),y.getsampwidth(),y.getframerate()): return 100.0, {'method':'wav-sample','formatMismatch':True}
        n=min(x.getnframes(),y.getnframes()); width=x.getsampwidth(); xa=x.readframes(n); ya=y.readframes(n)
        if width!=2: return binary_diff_bytes(xa,ya)[0], {'method':'wav-byte-fallback','sampleWidth':width}
        vals_a=struct.unpack('<'+'h'*(len(xa)//2),xa); vals_b=struct.unpack('<'+'h'*(len(ya)//2),ya)
        mse=sum((u-v)**2 for u,v in zip(vals_a,vals_b))/max(1,len(vals_a)); rms=math.sqrt(mse)/32768.0
        return min(100.0,rms*100.0), {'method':'wav-sample','normalizedRmsDifference':rms,'framesCompared':n}

def binary_diff_bytes(a,b):
    m=max(len(a),len(b)); d=abs(len(a)-len(b))+sum(x!=y for x,y in zip(a,b)); return (d*100.0/max(1,m), {'method':'binary','bytesCompared':min(len(a),len(b))})

def binary_diff(a,b): return binary_diff_bytes(Path(a).read_bytes(),Path(b).read_bytes())

def video_diff(a,b,tmp):
    try:
        subprocess.run(['ffmpeg','-version'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,check=True)
    except Exception:
        pct,meta=binary_diff(a,b); meta['method']='video-binary-fallback'; meta['precision']='low'; return pct,meta
    dirs=[]
    try:
        for src,label in ((a,'a'),(b,'b')):
            d=Path(tmp)/label; d.mkdir(parents=True,exist_ok=True); dirs.append(d)
            subprocess.run(['ffmpeg','-v','error','-i',str(src),'-vf','fps=2,scale=320:-1','-frames:v','120',str(d/'%04d.png')],check=True)
        fa=sorted(dirs[0].glob('*.png')); fb=sorted(dirs[1].glob('*.png')); n=min(len(fa),len(fb))
        if n==0: return 100.0, {'method':'video-frames','error':'no frames'}
        diffs=[]
        for i in range(n):
            p,_=png_diff(fa[i],fb[i],Path(tmp)/f'diff-{i:04d}.ppm'); diffs.append(p)
        frame_count_penalty=abs(len(fa)-len(fb))*100/max(1,max(len(fa),len(fb)))
        return max(sum(diffs)/n,frame_count_penalty), {'method':'video-frames','precision':'high','framesCompared':n,'maxFrameDifference':max(diffs),'meanFrameDifference':sum(diffs)/n}
    except Exception as e:
        pct,meta=binary_diff(a,b); meta.update({'method':'video-binary-fallback','precision':'low','ffmpegError':str(e)}); return pct,meta

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--kind',choices=['visual','video','audio'],required=True); ap.add_argument('--baseline',required=True); ap.add_argument('--current',required=True); ap.add_argument('--output',required=True); ap.add_argument('--threshold',type=float,default=1.0); ns=ap.parse_args()
    out=Path(ns.output); out.parent.mkdir(parents=True,exist_ok=True); diff_artifact=''
    if ns.kind=='visual' and Path(ns.baseline).suffix.lower()=='.png' and Path(ns.current).suffix.lower()=='.png':
        diff_artifact=str(out.with_suffix('.diff.ppm')); pct,meta=png_diff(ns.baseline,ns.current,diff_artifact)
    elif ns.kind=='audio' and Path(ns.baseline).suffix.lower()=='.wav' and Path(ns.current).suffix.lower()=='.wav': pct,meta=wav_diff(ns.baseline,ns.current)
    elif ns.kind=='video':
        with tempfile.TemporaryDirectory() as td: pct,meta=video_diff(ns.baseline,ns.current,td)
    else: pct,meta=binary_diff(ns.baseline,ns.current); meta['precision']='low'
    result={'kind':ns.kind,'differencePercent':round(pct,4),'thresholdPercent':ns.threshold,'passed':pct<=ns.threshold,'diffArtifact':diff_artifact,'comparison':meta}
    out.write_text(json.dumps(result,indent=2),encoding='utf-8'); print(json.dumps(result))
if __name__=='__main__': main()
