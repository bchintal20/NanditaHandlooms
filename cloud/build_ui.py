"""Stage only public website assets; never copy the repository recursively."""
import json
import hashlib
from pathlib import Path
root = Path(__file__).resolve().parent.parent
out = root / '.artifacts' / 'site'
out.mkdir(parents=True, exist_ok=True)
app = (root / 'app.js').read_text(encoding='utf-8')
app = app.replace("api('/api/state'", "api('/api/state?photos=lazy'")
app = app.replace("api('/api/restore'", "api('/api/restore?photos=lazy'")
start = app.index('function login(){')
end = app.index('function dialog(', start)
app = app[:start] + (root / 'cloud' / 'account-ui.js').read_text(encoding='utf-8') + '\n' + app[end:]
app = app.replace('let data=await res.json();', "let data=await res.json().catch(()=>({error:res.status===401?'Sign in to continue':'Request failed'}));")
app = app.replace('Your computer holds the shared ledger.', 'Azure holds your shared ledger.')
app = app.replace('Saved locally · revision ', 'Saved online · revision ')
app = app.replace('The app also saves the previous ledger to <code>data/last-good-backup.json</code> before every change.', 'Previous saves are retained in cloud recovery history. Download a backup regularly.')
start = app.index('<div class="panel section-gap"><h2>Computer + iPhone</h2>')
end = app.index("</div>`;$('#settingsForm')", start) + len('</div>')
app = app[:start] + """<div class="panel section-gap"><h2>Access from your devices</h2><p>Open this same website on your computer or phone. Both use the shared cloud ledger. Your computer can be switched off.</p><p class="muted">An internet connection is required. Safari → Share → Add to Home Screen creates a shortcut. The first request after inactivity may take longer while storage wakes up.</p><div id="accountControls"></div></div>""" + app[end:]
# Account controls are only injected into the hosted Settings screen.
app = app.replace("$('#settingsForm').onsubmit", "accountSettings();$('#settingsForm').onsubmit")
# Private invitation fragments are read once and removed before any ledger request.
last = app.rfind('load();')
app = app[:last] + "function accountStart(){if(location.hash.startsWith('#account=')){let token=location.hash.slice(9);window.history.replaceState(null,'',location.pathname+location.search);state=null;boutiqueUser=null;$('#nav').innerHTML='';accountForm('activate',token);}else load();}window.addEventListener('hashchange',accountStart);accountStart();" + app[last+len('load();'):]
html = (root / 'index.html').read_text(encoding='utf-8')
asset_version=hashlib.sha256(app.encode('utf-8')).hexdigest()[:12]
html=html.replace('src="/app.js"',f'src="/app.js?v={asset_version}"')
html = html.replace('Stored on your computer', 'Shared cloud ledger').replace('Local connection','Secure online access')
html = html.replace('No cloud subscription · Back up regularly · All payment methods included in reports','Back up regularly · All payment methods included in reports')
(out / 'index.html').write_text(html, encoding='utf-8')
(out / 'app.js').write_text(app, encoding='utf-8')
(out / 'style.css').write_bytes((root / 'style.css').read_bytes())
config = {
    'platform': {'apiRuntime': 'dotnet-isolated:10.0'},
    'routes': [
        {'route': '/api/health', 'allowedRoles': ['anonymous']},
        {'route': '/api/*', 'allowedRoles': ['anonymous']}  # Backend cookie authorization is mandatory.
    ],
    'globalHeaders': {'X-Content-Type-Options':'nosniff', 'X-Frame-Options':'DENY',
        'Referrer-Policy':'no-referrer', 'Cache-Control':'no-store',
        'Content-Security-Policy':"default-src 'self'; img-src 'self' data:; script-src 'self'; style-src 'self' 'unsafe-inline'; connect-src 'self'; frame-src https://www.instagram.com; frame-ancestors 'none'; base-uri 'self'; form-action 'self'"}
}
(out / 'staticwebapp.config.json').write_text(json.dumps(config, indent=2), encoding='utf-8')
allowed = {'index.html','app.js','style.css','staticwebapp.config.json'}
actual = {p.name for p in out.iterdir()}
if actual != allowed:
    raise RuntimeError('Unexpected staged files: ' + repr(actual - allowed))
print('Public UI staged: ' + ', '.join(sorted(actual)))
