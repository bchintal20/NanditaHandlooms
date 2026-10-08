"""Local-only boutique ledger. Python standard library; no dependencies."""
import datetime as dt
import hashlib
import hmac
import json
import os
from pathlib import Path
import secrets
import socket
import sqlite3
import threading
import webbrowser
from contextlib import contextmanager
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parent
DATA = ROOT / 'data'
DATA.mkdir(exist_ok=True)
DB = DATA / 'boutique.sqlite3'
LOCK = threading.Lock()
PORT = int(os.environ.get('BOUTIQUE_PORT', '8765'))
PINFILE = DATA / 'access-code.txt'
if not PINFILE.exists():
    PINFILE.write_text(str(secrets.randbelow(900000) + 100000))
PIN = PINFILE.read_text().strip()
TOKENS = set()
ATTEMPTS = {}

@contextmanager
def connect():
    con = sqlite3.connect(DB)
    try:
        with con:
            yield con
    finally:
        con.close()

def empty_state():
    return dict(version=1, revision=0, products=[], shipments=[], sales=[], returns=[],
                pending=[], settings=dict(name='Nandita Handlooms', margin=50, pricing='margin', tax=0, exchange=90))

with connect() as con:
    con.execute('CREATE TABLE IF NOT EXISTS ledger (id INTEGER PRIMARY KEY CHECK (id=1), payload TEXT NOT NULL)')
    con.execute('INSERT OR IGNORE INTO ledger VALUES (1, ?)', (json.dumps(empty_state()),))

def read_state():
    with connect() as con:
        return json.loads(con.execute('SELECT payload FROM ledger WHERE id=1').fetchone()[0])

def number(value, label, minimum=0, maximum=1e10):
    if isinstance(value, bool) or not isinstance(value, (float, int)) or not minimum <= value <= maximum:
        raise ValueError(label + ' is invalid')
    return value

def date(value):
    dt.date.fromisoformat(value)

def validate(s):
    if s.get('version') != 1:
        raise ValueError('Unsupported backup version')
    for key in ('products', 'shipments', 'sales', 'returns'):
        if not isinstance(s.get(key), list) or len(s[key]) > 100000:
            raise ValueError('Invalid ' + key)
        ids = [x.get('id') for x in s[key]]
        if any(not isinstance(i, str) or not i for i in ids) or len(ids) != len(set(ids)):
            raise ValueError('Duplicate or invalid record IDs')
    number(s['settings']['margin'], 'Margin', 0, 99.9)
    number(s['settings']['tax'], 'Tax rate', 0, 30)
    number(s['settings']['exchange'], 'Exchange rate', .000001)
    if s['settings'].get('pricing', 'margin') not in ('margin', 'markup'):
        raise ValueError('Invalid pricing mode')
    shipments = {x['id']: x for x in s['shipments']}
    products = {x['id']: x for x in s['products']}
    for sh in shipments.values():
        if sh['method'] not in ('quantity', 'value', 'weight', 'manual'):
            raise ValueError('Invalid allocation method')
        for k in ('domestic', 'international', 'duty', 'brokerage', 'other'):
            number(sh[k], k)
    for p in products.values():
        if not str(p['name']).strip() or not str(p['sku']).strip():
            raise ValueError('Product name and SKU are required')
        date(p['date'])
        for k in ('qty', 'reserved', 'openingSold'):
            if k == 'openingSold':
                p.setdefault(k, 0)
            number(p[k], k)
            if int(p[k]) != p[k]:
                raise ValueError('Quantities must be whole numbers')
        if p['qty'] < 1:
            raise ValueError('Received quantity must be at least one')
        for k in ('purchase', 'packaging', 'weight', 'share'):
            number(p[k], k)
        number(p['exchange'], 'Exchange rate', .000001)
        if p['currency'] not in ('INR', 'USD'):
            raise ValueError('Invalid currency')
        if p['shipment'] and p['shipment'] not in shipments:
            raise ValueError('Missing shipment')
        if p.get('instagram'):
            u = urlparse(p['instagram'])
            if u.scheme != 'https' or u.hostname not in ('instagram.com', 'www.instagram.com'):
                raise ValueError('Use an https://www.instagram.com/ post or reel link')
        if p.get('photo') and (not p['photo'].startswith('data:image/jpeg;base64,') or len(p['photo']) > 1000000):
            raise ValueError('Photo must be an app-compressed JPEG')
    sale_map = {x['id']: x for x in s['sales']}
    sold = {pid: p.get('openingSold', 0) for pid, p in products.items()}
    for sale in s['sales']:
        date(sale['date'])
        if sale['payment'] not in ('Zelle', 'Venmo', 'Cash', 'Other'):
            raise ValueError('Invalid payment method')
        number(sale['price'], 'Selling price')
        number(sale['tax'], 'Tax collected')
        number(sale['expense'], 'Selling expense')
        charge = sale.setdefault('shippingCharge', 0)
        shipping_cost = sale.setdefault('shippingCost', 0)
        shipping_taxable = sale.setdefault('shippingTaxable', charge if sale['taxable'] else 0)
        number(charge, 'Shipping charged to customer')
        if shipping_cost is not None:
            number(shipping_cost, 'Actual customer shipping expense')
        number(shipping_taxable, 'Taxable portion of shipping', 0, charge)
        if not sale['taxable'] and shipping_taxable != 0:
            raise ValueError('Shipping on a nontaxable sale must have zero taxable amount')
        if sale['taxable'] and shipping_taxable < charge and not sale.get('shippingReason', '').strip():
            raise ValueError('A nontaxable shipping portion requires documentation notes')
        if not sale['taxable'] and (sale['tax'] != 0 or not sale['reason'].strip()):
            raise ValueError('Nontaxable sales require a reason and zero tax')
        p = products.get(sale['product'])
        if not p:
            raise ValueError('Missing sale product')
        number(sale['qty'], 'Sale quantity', 1)
        if int(sale['qty']) != sale['qty']:
            raise ValueError('Sale quantity must be a whole number')
        sold[p['id']] += sale['qty']
    refunded = set()
    for r in s['returns']:
        date(r['date'])
        if r['sale'] not in sale_map or r['sale'] in refunded:
            raise ValueError('Each sale may have one full refund')
        sale = sale_map[r['sale']]
        if r['date'] < sale['date']:
            raise ValueError('Refund cannot precede sale')
        refunded.add(r['sale'])
        if r['restock']:
            sold[sale['product']] -= sale['qty']
    for pid, p in products.items():
        if sold[pid] + p['reserved'] > p['qty']:
            raise ValueError('Sold plus reserved quantity exceeds received quantity for ' + p['sku'])
    pending_counts = {pid: 0 for pid in products}
    for pending in s.get('pending', []):
        if pending['product'] not in products:
            raise ValueError('Missing product for historical sale')
        number(pending['qty'], 'Historical sale quantity', 0)
        number(pending['total'], 'Historical sale total')
        pending_counts[pending['product']] += pending['qty']
    if any(pending_counts[pid] != p['openingSold'] for pid, p in products.items()):
        raise ValueError('Historical review quantities do not match opening sold quantities')
    for sh in shipments.values():
        group = [p for p in products.values() if p['shipment'] == sh['id']]
        if group and sh['method'] in ('weight', 'manual'):
            key = 'weight' if sh['method'] == 'weight' else 'share'
            if any(p[key] <= 0 for p in group):
                raise ValueError('Every shipment item needs a positive ' + key)
        if group and sh['method'] == 'value' and sum(p['purchase'] * p['qty'] for p in group) == 0:
            raise ValueError('Value allocation needs purchase costs')

def save_state(s, restore=False):
    validate(s)
    with LOCK, connect() as con:
        con.execute('BEGIN IMMEDIATE')
        current = json.loads(con.execute('SELECT payload FROM ledger WHERE id=1').fetchone()[0])
        if s.get('revision') != current['revision']:
            raise RuntimeError('Another device changed the ledger. Refresh and try again.')
        # Preserve a recovery copy before every change, including imports.
        backup = DATA / 'last-good-backup.json'
        temporary = DATA / 'backup-writing.tmp'
        temporary.write_text(json.dumps(current, ensure_ascii=False), encoding='utf-8')
        temporary.replace(backup)
        s['revision'] = current['revision'] + 1
        con.execute('UPDATE ledger SET payload=? WHERE id=1', (json.dumps(s, ensure_ascii=False),))
    return s

def authorize(handler):
    raw = handler.headers.get('Cookie', '')
    token = next((x.strip()[8:] for x in raw.split(';') if x.strip().startswith('session=')), '')
    return token in TOKENS

class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def send(self, status, payload, cookie=None, content_type='application/json'):
        import sys
        if '--diagnostics' in sys.argv:
            try:
                diagnostic = DATA / 'connection-check.log'
                if diagnostic.exists() and diagnostic.stat().st_size > 65536:
                    diagnostic.write_text('', encoding='utf-8')
                with diagnostic.open('a', encoding='utf-8') as log:
                    route = urlparse(self.path).path
                    known_route = route if route in ('/', '/app.js', '/style.css', '/api/login', '/api/state', '/api/shutdown') else '/other'
                    log.write(f'{dt.datetime.now().isoformat(timespec="seconds")} {self.client_address[0]} {self.command} {known_route} {status}\n')
            except OSError:
                pass
        data = json.dumps(payload).encode() if content_type == 'application/json' else payload
        self.send_response(status)
        self.send_header('Content-Type', content_type)
        self.send_header('Content-Length', str(len(data)))
        self.send_header('Cache-Control', 'no-store')
        self.send_header('X-Content-Type-Options', 'nosniff')
        self.send_header('X-Frame-Options', 'DENY')
        self.send_header('Referrer-Policy', 'no-referrer')
        if cookie:
            self.send_header('Set-Cookie', cookie)
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        route = urlparse(self.path).path
        if route == '/api/state':
            if not authorize(self):
                return self.send(401, {'error': 'Enter your access code'})
            return self.send(200, read_state())
        allowed = {'/': ('index.html', 'text/html; charset=utf-8'), '/app.js': ('app.js', 'text/javascript; charset=utf-8'), '/style.css': ('style.css', 'text/css; charset=utf-8')}
        if route in allowed:
            name, mime = allowed[route]
            return self.send(200, (ROOT / name).read_bytes(), content_type=mime)
        self.send(404, {'error': 'Not found'})

    def do_POST(self):
        try:
            # Same-origin browser requests only: avoids websites modifying a LAN ledger.
            origin = self.headers.get('Origin')
            if origin != 'http://' + self.headers.get('Host', ''):
                return self.send(403, {'error': 'Request must come from this app'})
            length = int(self.headers.get('Content-Length', 0))
            if not 0 < length <= 15_000_000:
                return self.send(413, {'error': 'Request too large (15 MB limit)'})
            body = json.loads(self.rfile.read(length))
            if self.path == '/api/login':
                import time
                ip = self.client_address[0]
                times = [t for t in ATTEMPTS.get(ip, []) if time.time() - t < 60]
                ATTEMPTS[ip] = times
                if len(times) >= 10:
                    return self.send(429, {'error': 'Wait one minute before trying again'})
                times.append(time.time())
                if not hmac.compare_digest(str(body.get('pin', '')), PIN):
                    return self.send(401, {'error': 'Incorrect access code'})
                token = secrets.token_urlsafe(32)
                TOKENS.add(token)
                return self.send(200, {'ok': True}, 'session=' + token + '; HttpOnly; SameSite=Strict; Path=/')
            if not authorize(self):
                return self.send(401, {'error': 'Enter your access code'})
            if self.path == '/api/shutdown':
                self.send(200, {'ok': True})
                threading.Thread(target=self.server.shutdown, daemon=True).start()
                return
            if self.path in ('/api/state', '/api/restore'):
                return self.send(200, save_state(body, self.path == '/api/restore'))
            self.send(404, {'error': 'Not found'})
        except RuntimeError as exc:
            self.send(409, {'error': str(exc)})
        except (ValueError, KeyError, TypeError) as exc:
            self.send(400, {'error': str(exc)})
        except Exception:
            self.send(500, {'error': 'Could not save. Check disk space and retry; keep a backup.'})

if __name__ == '__main__':
    import sys
    initial = ROOT / 'initial-workbook-import.json'
    current = read_state()
    if initial.exists() and current['revision'] == 0 and not current['products']:
        seed = json.loads(initial.read_text(encoding='utf-8'))
        save_state(seed)
    host = '0.0.0.0' if '--wifi' in sys.argv else '127.0.0.1'
    try:
        server = ThreadingHTTPServer((host, PORT), Handler)
    except OSError:
        print('The app port is already in use. If the boutique app is running, use Stop Boutique.bat before switching to Wi-Fi mode.', flush=True)
        print('Open http://localhost:' + str(PORT) + ' to access the current instance.', flush=True)
        raise SystemExit(1)
    print('\nNandita Handlooms | Your local boutique ledger', flush=True)
    print('Computer: http://localhost:' + str(PORT), flush=True)
    print('Access code: ' + PIN, flush=True)
    if host == '0.0.0.0':
        ips = set(socket.gethostbyname_ex(socket.gethostname())[2])
        for ip in ips:
            if not ip.startswith('127.'):
                print('iPhone on same Wi-Fi: http://' + ip + ':' + str(PORT), flush=True)
        print('Use a trusted private Wi-Fi network. Keep this window open.', flush=True)
    print('Data: ' + str(DB), flush=True)
    print('Close this window or press Ctrl+C to stop.\n', flush=True)
    if '--no-browser' not in sys.argv:
        webbrowser.open('http://localhost:' + str(PORT))
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        server.server_close()
