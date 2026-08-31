#!/usr/bin/env python3
import json, os, time, uuid
from datetime import datetime, timezone

def _pid_alive(pid):
    if not isinstance(pid,int) or pid<=0: return False
    try:
        os.kill(pid,0); return True
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    except Exception:
        return False

class FileLock:
    def __init__(self,path,owner,timeout=5.0,stale_after=30.0):
        self.path=os.path.abspath(path); self.owner=owner
        self.timeout=float(timeout); self.stale_after=float(stale_after)
        self.token=uuid.uuid4().hex; self.acquired=False
    def _meta(self):
        return {"pid":os.getpid(),"owner":self.owner,"token":self.token,
                "created_at":datetime.now(timezone.utc).isoformat(),
                "created_epoch":time.time()}
    def _stale(self):
        try:
            meta=json.load(open(self.path,encoding='utf-8'))
            created=float(meta.get("created_epoch",0) or 0)
            pid=meta.get("pid")
            # A valid lock document with a dead/invalid owner is stale immediately.
            # This is distinct from a freshly-created but not-yet-written lock file.
            if not _pid_alive(pid):
                return True
            if created:
                return time.time()-created>self.stale_after
        except Exception:
            # Fresh unreadable JSON may simply mean the owner has created the file
            # and is still writing metadata. Give it the stale_after grace window.
            pass
        try:
            age=time.time()-os.path.getmtime(self.path)
            return age>self.stale_after
        except FileNotFoundError:
            return False
    def acquire(self):
        os.makedirs(os.path.dirname(self.path),exist_ok=True)
        start=time.time()
        while True:
            try:
                fd=os.open(self.path,os.O_CREAT|os.O_EXCL|os.O_WRONLY)
                with os.fdopen(fd,'w',encoding='utf-8') as f:
                    json.dump(self._meta(),f,ensure_ascii=False); f.flush(); os.fsync(f.fileno())
                self.acquired=True; return self
            except FileExistsError:
                if self._stale():
                    try: os.unlink(self.path)
                    except FileNotFoundError: pass
                    continue
                if time.time()-start>self.timeout: raise TimeoutError("lock timeout "+self.path)
                time.sleep(.05)
    def release(self):
        if not self.acquired: return
        try:
            meta=json.load(open(self.path,encoding='utf-8'))
            if meta.get("token")==self.token: os.unlink(self.path)
        except FileNotFoundError: pass
        finally: self.acquired=False
    def __enter__(self): return self.acquire()
    def __exit__(self,*_): self.release()

def atomic_write_json(path,obj):
    path=os.path.abspath(path); os.makedirs(os.path.dirname(path),exist_ok=True)
    tmp=f"{path}.tmp.{os.getpid()}.{uuid.uuid4().hex}"
    with open(tmp,'w',encoding='utf-8') as f:
        json.dump(obj,f,ensure_ascii=False,indent=2); f.write("\n"); f.flush(); os.fsync(f.fileno())
    os.replace(tmp,path)
