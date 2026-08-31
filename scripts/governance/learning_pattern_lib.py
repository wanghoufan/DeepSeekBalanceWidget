#!/usr/bin/env python3
import hashlib, json, os, re
from datetime import datetime, timezone
from lock_utils import FileLock, atomic_write_json

STOPWORDS={'THE','A','AN','TO','AND','OR','OF','IN','ON','IS','ARE','THIS','THAT','CURRENT','ISSUE','PROBLEM'}

def norm(value):
    value=str(value or '').strip().upper()
    value=re.sub(r'[^A-Z0-9_\-\u4e00-\u9fff]+','_',value).strip('_')
    return value[:180]

def tokens(text):
    raw=re.findall(r'[A-Za-z0-9]+|[\u4e00-\u9fff]{1,4}',str(text or '').upper())
    return {x for x in raw if x not in STOPWORDS and len(x)>1}

def similarity(a,b):
    if not a or not b: return 0.0
    return len(a & b)/max(1,len(a | b))

def symptom_signature(symptom):
    s=str(symptom or '').lower()
    if ('task manager' in s or '推进者' in s or 'tm' in s) and any(k in s for k in ['stop','停止','停下','continue','继续','p0']):
        return 'TM_CONTINUATION'
    if ('task manager' in s or '推进者' in s or 'tm' in s) and any(k in s for k in ['edit','修改代码','业务代码','write']):
        return 'TM_BUSINESS_WRITE_BOUNDARY'
    if any(k in s for k in ['dispatch gateway','raw launch','worker-start','直接调用']):
        return 'TM_DISPATCH_GATEWAY'
    if any(k in s for k in ['handoff','交接','resume capsule','恢复上下文']):
        return 'HANDOFF_REQUIRED'
    if any(k in s for k in ['screenshot feedback','截图反馈','ui feedback']):
        return 'UI_SCREENSHOT_FEEDBACK'
    return None

def registry_path(root):
    p=os.path.join(root,'docs','learning','LEARNING_PATTERN_REGISTRY.yaml')
    if not os.path.exists(p): raise FileNotFoundError(p)
    return p

def resolve(root,proposal=None,trigger=None,category=None,symptom=None,root_cause=None,create_candidate=True):
    p=registry_path(root)
    with FileLock(p+'.lock','pattern-resolver',timeout=5,stale_after=15):
        data=json.load(open(p,encoding='utf-8'))
        patterns=data.setdefault('patterns',{})
        proposed=norm(proposal)
        # 1) exact canonical/alias
        for cid,rec in patterns.items():
            aliases={norm(x) for x in rec.get('aliases',[])}
            if proposed and (proposed==norm(cid) or proposed in aliases):
                return {'canonical_pattern_id':cid,'match_type':'CANONICAL_OR_ALIAS','registry_path':p}
        # 2) stable known symptom heuristic
        sig=symptom_signature(symptom)
        if sig and sig in patterns:
            return {'canonical_pattern_id':sig,'match_type':'NORMALIZED_SYMPTOM','registry_path':p}

        cat=str(category or 'UNCLASSIFIED').upper()
        event_tokens=tokens(str(symptom or '')+' '+str(root_cause or ''))

        # 3) deduplicate unknown NEW_PATTERN candidates by category + symptom/root-cause similarity.
        best=None
        for cid,rec in patterns.items():
            if rec.get('status')!='NEW_PATTERN_CANDIDATE': continue
            if str(rec.get('category','')).upper()!=cat: continue
            cand_tokens=set(rec.get('candidate_tokens') or [])
            score=similarity(event_tokens,cand_tokens)
            if score>=0.58 and (best is None or score>best[0]):
                best=(score,cid,rec)
        if best:
            _,cid,rec=best
            if proposed:
                aliases=rec.setdefault('aliases',[])
                if proposed not in {norm(x) for x in aliases}: aliases.append(proposed)
            rec['candidate_tokens']=sorted(set(rec.get('candidate_tokens') or []) | event_tokens)
            rec['candidate_dedup_hits']=int(rec.get('candidate_dedup_hits',0))+1
            rec['last_candidate_match_at']=datetime.now(timezone.utc).isoformat()
            atomic_write_json(p,data)
            return {'canonical_pattern_id':cid,'match_type':'CANDIDATE_DEDUP','registry_path':p,'similarity':best[0]}

        # 4) deterministic new candidate.
        signature='|'.join(sorted(event_tokens)) or str(symptom or '').strip().lower()
        base=f"{str(trigger or '').upper()}|{cat}|{signature}"
        cid='NEW_PATTERN_'+hashlib.sha256(base.encode()).hexdigest()[:16].upper()
        if cid not in patterns and create_candidate:
            now=datetime.now(timezone.utc).isoformat()
            patterns[cid]={
              'canonical_id':cid,
              'aliases':[proposed] if proposed else [],
              'category':cat,
              'preferred_resolution':'TEMPLATE',
              'occurrences':0,
              'first_seen':None,'last_seen':None,
              'machine_enforceable':None,
              'stable_multistep_sop':None,
              'current_enforcement':'NONE',
              'last_enforcement_change':None,
              'recurrence_after_enforcement':0,
              'effectiveness':'UNKNOWN',
              'decision':'PENDING_CANDIDATE_REVIEW',
              'status':'NEW_PATTERN_CANDIDATE',
              'candidate_tokens':sorted(event_tokens),
              'candidate_dedup_hits':0,
              'created_at':now
            }
            atomic_write_json(p,data)
        return {'canonical_pattern_id':cid,'match_type':'NEW_PATTERN_CANDIDATE','registry_path':p}

def record_occurrence(root,canonical_id,proposed_alias=None):
    p=registry_path(root)
    with FileLock(p+'.lock','pattern-registry',timeout=5,stale_after=15):
        data=json.load(open(p,encoding='utf-8'))
        rec=data['patterns'][canonical_id]
        now=datetime.now(timezone.utc).isoformat()
        alias=norm(proposed_alias)
        if alias and alias!=norm(canonical_id):
            aliases=rec.setdefault('aliases',[])
            if alias not in {norm(x) for x in aliases}: aliases.append(alias)
        rec['occurrences']=int(rec.get('occurrences',0))+1
        rec['first_seen']=rec.get('first_seen') or now
        rec['last_seen']=now
        if rec.get('current_enforcement') not in (None,'','NONE'):
            rec['recurrence_after_enforcement']=int(rec.get('recurrence_after_enforcement',0))+1
            rec['effectiveness']='RECURRENCE_DETECTED'
        atomic_write_json(p,data)
        return {
          'canonical_pattern_id':canonical_id,
          'recurrence_count':rec['occurrences'],
          'current_enforcement':rec.get('current_enforcement'),
          'preferred_resolution':rec.get('preferred_resolution')
        }

def merge_candidate(root,source_id,target_id,aliases=None,decision='MERGED'):
    p=registry_path(root)
    with FileLock(p+'.lock','pattern-candidate-merge',timeout=5,stale_after=15):
        data=json.load(open(p,encoding='utf-8')); patterns=data.get('patterns',{})
        src=patterns.get(source_id); tgt=patterns.get(target_id)
        if not src or not tgt: raise RuntimeError('source/target pattern missing')
        if source_id==target_id: raise RuntimeError('source and target are identical')
        merged_aliases=set(tgt.get('aliases') or []) | set(src.get('aliases') or []) | set(aliases or [])
        tgt['aliases']=sorted(merged_aliases)
        tgt['occurrences']=int(tgt.get('occurrences',0))+int(src.get('occurrences',0))
        tgt['candidate_tokens']=sorted(set(tgt.get('candidate_tokens') or []) | set(src.get('candidate_tokens') or []))
        tgt['last_seen']=max(x for x in [tgt.get('last_seen'),src.get('last_seen')] if x) if (tgt.get('last_seen') or src.get('last_seen')) else None
        src['status']='MERGED'
        src['merged_into']=target_id
        src['decision']=decision
        src['merged_at']=datetime.now(timezone.utc).isoformat()
        atomic_write_json(p,data)
        return {'source':source_id,'target':target_id,'status':'MERGED'}
