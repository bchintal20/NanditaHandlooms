"""Stop this boutique app gracefully without killing unrelated processes."""
import http.cookiejar
import json
import os
from pathlib import Path
import urllib.error
import urllib.request

port = os.environ.get('BOUTIQUE_PORT', '8765')
url = 'http://localhost:' + port
pinfile = Path(__file__).resolve().parent / 'data/access-code.txt'
try:
    pin = pinfile.read_text().strip()
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    def post(route, body):
        request = urllib.request.Request(url + route, json.dumps(body).encode(), {'Content-Type':'application/json','Origin':url})
        return opener.open(request, timeout=5).read()
    post('/api/login', {'pin':pin})
    post('/api/shutdown', {'ok':True})
    print('Boutique app stopped. Your data is saved.')
except (OSError, urllib.error.URLError) as exc:
    print('App is not running or could not be reached:', exc)
