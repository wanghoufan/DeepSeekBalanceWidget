#!/usr/bin/env python3
"""
Ephemeral HMAC capability for internal-only governance helpers.

Security model:
- Secret is supplied only to trusted governance/runtime processes through
  ORCA_GOVERNANCE_INTERNAL_SECRET.
- Worker/Agent environments must scrub this secret.
- Internal writers require a scoped, one-shot token; direct Agent calls fail closed.
"""
import base64, hashlib, hmac, json, os, time, uuid
from datetime import datetime, timezone
from lock_utils import FileLock

ENV_SECRET="ORCA_GOVERNANCE_INTERNAL_SECRET"
GENERIC_SCOPES={"REGISTER_SKILL","TEMPLATE_SYNC"}

def _secret():
    s=os.environ.get(ENV_SECRET)
    if not s or len(s)<32:
        raise RuntimeError("trusted internal secret unavailable")
    return s.encode()

def _b64e(b): return base64.urlsafe_b64encode(b).decode().rstrip("=")
def _b64d(s):
    return base64.urlsafe_b64decode(s + "="*((4-len(s)%4)%4))

def issue(scope, claims=None, ttl_seconds=30):
    if scope not in GENERIC_SCOPES:
        raise RuntimeError("internal capability scope is not generic-issuable")
    now=int(time.time())
    payload={
      "capability_id":"ICAP-"+uuid.uuid4().hex,
      "scope":scope,
      "claims":claims or {},
      "issued_at":now,
      "expires_at":now+int(ttl_seconds),
      "one_shot":True
    }
    raw=json.dumps(payload,sort_keys=True,separators=(",",":")).encode()
    sig=hmac.new(_secret(),raw,hashlib.sha256).digest()
    return _b64e(raw)+"."+_b64e(sig)

def _decode(token):
    try:
        a,b=token.split(".",1)
        raw=_b64d(a); sig=_b64d(b)
        expect=hmac.new(_secret(),raw,hashlib.sha256).digest()
        if not hmac.compare_digest(sig,expect):
            raise RuntimeError("invalid internal capability signature")
        return json.loads(raw)
    except Exception as e:
        if isinstance(e,RuntimeError): raise
        raise RuntimeError("malformed internal capability") from e

def verify_and_consume(repo_root, token, required_scope, required_claims=None):
    payload=_decode(token)
    now=int(time.time())
    if payload.get("scope")!=required_scope:
        raise RuntimeError("internal capability scope mismatch")
    if now>int(payload.get("expires_at",0)):
        raise RuntimeError("internal capability expired")
    if payload.get("one_shot") is not True:
        raise RuntimeError("internal capability must be one-shot")
    required_claims=required_claims or {}
    claims=payload.get("claims") or {}
    for k,v in required_claims.items():
        if claims.get(k)!=v:
            raise RuntimeError("internal capability claim mismatch: "+k)

    ledger=os.path.join(repo_root,"docs","runtime","INTERNAL_CAPABILITY_CONSUMED.jsonl")
    os.makedirs(os.path.dirname(ledger),exist_ok=True)
    cid=payload.get("capability_id")
    with FileLock(ledger+".lock","internal-capability-consume",timeout=5,stale_after=15):
        if os.path.exists(ledger):
            for line in open(ledger,encoding="utf-8"):
                if not line.strip(): continue
                rec=json.loads(line)
                if rec.get("capability_id")==cid:
                    raise RuntimeError("internal capability replay")
        with open(ledger,"a",encoding="utf-8") as f:
            f.write(json.dumps({
              "capability_id":cid,
              "scope":required_scope,
              "used_at":datetime.now(timezone.utc).isoformat()
            },ensure_ascii=False,separators=(",",":"))+"\n")
            f.flush(); os.fsync(f.fileno())
    return payload
