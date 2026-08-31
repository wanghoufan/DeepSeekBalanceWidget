#!/usr/bin/env python3
import json, os, platform, shlex, shutil, subprocess, sys, tempfile, hashlib

DANGEROUS_CODEX={
 "--full-auto","--dangerously-bypass-approvals-and-sandbox","--yolo",
 "--approve-for-me","--not-so-yolo","--dangerously-bypass-hook-trust"
}

def repo_root_from_here():
    return os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..','..'))

def require_macos():
    if sys.platform!='darwin' and os.environ.get('ORCA_MAC_ADAPTER_TEST_MODE')!='1':
        raise RuntimeError('MAC_RUNTIME_V1 requires macOS (darwin)')

def which(name):
    p=shutil.which(name)
    if not p: raise RuntimeError(f'executable not found: {name}')
    return p

def run(argv,cwd=None,env=None,timeout=30,expect_json=False):
    r=subprocess.run(argv,cwd=cwd,env=env,text=True,capture_output=True,timeout=timeout)
    if r.returncode!=0:
        raise RuntimeError(f'command failed {argv!r}: {r.stderr.strip() or r.stdout.strip()}')
    if expect_json:
        try: return json.loads(r.stdout)
        except Exception as e: raise RuntimeError(f'non-json output from {argv!r}: {r.stdout[:500]}') from e
    return r.stdout

def safe_codex_argv(argv):
    lowered={x.strip() for x in argv}
    bad=sorted(lowered & DANGEROUS_CODEX)
    if bad: raise RuntimeError('dangerous Codex argv denied: '+','.join(bad))
    if '--sandbox' not in argv: raise RuntimeError('Codex launcher requires explicit --sandbox')
    if '--ask-for-approval' not in argv and '-a' not in argv:
        raise RuntimeError('Codex launcher requires explicit approval policy')
    return True

def q(s): return shlex.quote(str(s))

def extract(obj,keys):
    if isinstance(obj,dict):
        # Orca envelopes every JSON RPC response as `{id, ok, result}`.  The
        # top-level id identifies that RPC call, while task/run/dispatch ids
        # live below result.  Resolve result first so callers never mistake a
        # transport request id for a durable orchestration identity.
        if 'result' in obj:
            x=extract(obj['result'],keys)
            if x: return x
        for k in keys:
            if obj.get(k): return obj[k]
        for v in obj.values():
            x=extract(v,keys)
            if x: return x
    elif isinstance(obj,list):
        for v in obj:
            x=extract(v,keys)
            if x: return x
    return None

def file_sha256(path):
    h=hashlib.sha256()
    with open(path,'rb') as f:
        for c in iter(lambda:f.read(65536),b''): h.update(c)
    return h.hexdigest()

def load_config(project_root=None):
    rr=repo_root_from_here()
    p=os.path.join(rr,'docs','runtime','MAC_RUNTIME_CONFIG.json')
    return json.load(open(p,encoding='utf-8'))

def sanitized_worker_command(argv,env_pairs=None):
    env_pairs=env_pairs or {}
    parts=['env','-u','ORCA_GOVERNANCE_INTERNAL_SECRET']
    for k,v in env_pairs.items():
        parts.append(f'{k}={v}')
    parts.extend(argv)
    return ' '.join(q(x) for x in parts)
