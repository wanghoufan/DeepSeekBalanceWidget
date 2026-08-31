#!/usr/bin/env python3
import json, os
from lock_utils import FileLock, atomic_write_json

def update_queue(root, item, owner='promotion-queue-writer'):
    q=os.path.join(root,'docs','learning','PROMOTION_QUEUE.yaml')
    os.makedirs(os.path.dirname(q),exist_ok=True)
    with FileLock(q+'.lock',owner,timeout=5,stale_after=15):
        data={'schema_version':'2.3.1-self-learning-final','authoritative':True,'items':[]}
        if os.path.exists(q) and os.path.getsize(q):
            data=json.load(open(q,encoding='utf-8'))
        items=data.setdefault('items',[])
        key=item.get('learning_event_id')
        for i,x in enumerate(items):
            if x.get('learning_event_id')==key:
                items[i]={**x,**item}
                break
        else:
            items.append(item)
        atomic_write_json(q,data)
        return item
