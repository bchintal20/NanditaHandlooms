"""Stage only public website assets; never copy the repository recursively."""
import json
from pathlib import Path
root = Path(__file__).resolve().parent.parent
out = root / '.artifacts' / 'site'
out.mkdir(parents=True, exist_ok=True)
app = (root / 'app.js').read_text(encoding='utf-8')
start = app.index('function login(){')
end = app.index('function dialog(', start)
app = app[:start] + """function login(){window.location.assign('/.auth/login/aad?post_login_redirect_uri=%2F');}
""" + app[end:]
app = app.replace('let data=await res.json();', "let data=await res.json().catch(()=>({error:res.status===401?'Sign in to continue':'Request failed'}));")
app = app.replace('Your computer holds the shared ledger.', 'Azure holds your shared ledger.')
app = app.replace('Saved locally · revision ', 'Saved online · revision ')
app = app.replace('The app also saves the previous ledger to <code>data/last-good-backup.json</code> before every change.', 'Previous saves are retained in cloud recovery history. Download a backup regularly.')
start = app.index('<div class="panel section-gap"><h2>Computer + iPhone</h2>')
end = app.index("</div>`;$('#settingsForm')", start) + len('</div>')
app = app[:start] + """<div class="panel section-gap"><h2>Access from your devices</h2><p>Open this same website on your computer or phone. Both use the shared cloud ledger. Your computer can be switched off.</p><p class="muted">An internet connection is required. Safari → Share → Add to Home Screen creates a shortcut. The first request after inactivity may take longer while storage wakes up.</p><a href="/.auth/logout?post_logout_redirect_uri=%2F">Sign out</a></div>""" + app[end:]
html = (root / 'index.html').read_text(encoding='utf-8')
html = html.replace('Stored on your computer', 'Shared cloud ledger').replace('Local connection','Secure online access')
html = html.replace('No cloud subscription · Back up regularly · All payment methods included in reports','Back up regularly · All payment methods included in reports')
(out / 'index.html').write_text(html, encoding='utf-8')
(out / 'app.js').write_text(app, encoding='utf-8')
(out / 'style.css').write_bytes((root / 'style.css').read_bytes())
config = {
    'platform': {'apiRuntime': 'dotnet-isolated:10.0'},
    'routes': [
        {'route': '/api/health', 'allowedRoles': ['anonymous']},
        {'route': '/api/*', 'allowedRoles': ['authenticated']}
    ],
    'globalHeaders': {'X-Content-Type-Options':'nosniff', 'X-Frame-Options':'DENY',
        'Referrer-Policy':'no-referrer', 'Cache-Control':'no-store',
        'Content-Security-Policy':"default-src 'self'; img-src 'self' data:; script-src 'self'; style-src 'self' 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'"}
}
(out / 'staticwebapp.config.json').write_text(json.dumps(config, indent=2), encoding='utf-8')
allowed = {'index.html','app.js','style.css','staticwebapp.config.json'}
actual = {p.name for p in out.iterdir()}
if actual != allowed:
    raise RuntimeError('Unexpected staged files: ' + repr(actual - allowed))
print('Public UI staged: ' + ', '.join(sorted(actual)))
