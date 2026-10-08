"""Read-only export of the local ledger; output is intentionally Git-ignored."""
import hashlib
import json
import sqlite3
from pathlib import Path
root = Path(__file__).resolve().parent.parent
with sqlite3.connect((root / 'data' / 'boutique.sqlite3').as_uri() + '?mode=ro', uri=True) as con:
    payload = con.execute('SELECT payload FROM ledger WHERE id=1').fetchone()[0]
state = json.loads(payload)
folder = root / '.artifacts'
folder.mkdir(exist_ok=True)
target = folder / 'migration-ledger.json'
target.write_text(json.dumps(state, ensure_ascii=False), encoding='utf-8')
print(json.dumps(dict(revision=state['revision'], products=len(state['products']),
    sales=len(state['sales']), pending=len(state.get('pending', [])),
    sha256=hashlib.sha256(target.read_bytes()).hexdigest())))
