#!/usr/bin/env python3
import json, os, subprocess, sys

TEST_INTERNAL_SECRET='governance-contract-test-secret-abcdefghijklmnopqrstuvwxyz-123456789'

def trusted_template_sync(template_root, promotion_id=None, initialize=False, delivery_version='TEST', script_path=None):
    template_root=os.path.abspath(template_root)
    if script_path is None:
        candidate=os.path.join(template_root,'scripts','governance','template-sync')
        if os.path.isfile(candidate):
            script_path=candidate
        else:
            base=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..'))
            script_path=os.path.join(base,'scripts','governance','template-sync')

    govdir=os.path.dirname(script_path)
    sys.path.insert(0,govdir)
    old=os.environ.get('ORCA_GOVERNANCE_INTERNAL_SECRET')
    os.environ['ORCA_GOVERNANCE_INTERNAL_SECRET']=TEST_INTERNAL_SECRET
    try:
        from internal_capability import issue
        claims={
          'template_root':template_root,
          'promotion_id':promotion_id,
          'initialize':bool(initialize),
          'caller_holds_template_lock':False,
          'delivery_version':delivery_version
        }
        token=issue('TEMPLATE_SYNC',claims,ttl_seconds=30)
    finally:
        if old is None:
            os.environ.pop('ORCA_GOVERNANCE_INTERNAL_SECRET',None)
        else:
            os.environ['ORCA_GOVERNANCE_INTERNAL_SECRET']=old
        try: sys.path.remove(govdir)
        except ValueError: pass

    env=os.environ.copy()
    env['ORCA_GOVERNANCE_INTERNAL_SECRET']=TEST_INTERNAL_SECRET
    cmd=[sys.executable,'-S',script_path,'--template-root',template_root,'--internal-token',token]
    if promotion_id is not None:
        cmd += ['--promotion-id',promotion_id]
    if initialize:
        cmd.append('--initialize')
    if delivery_version is not None:
        cmd += ['--delivery-version',delivery_version]
    r=subprocess.run(cmd,text=True,capture_output=True,timeout=30,env=env)
    if r.returncode!=0:
        raise AssertionError('trusted_template_sync failed: '+r.stdout+r.stderr)
    return json.loads(r.stdout)
