"""Detached local app launcher; logs and shutdown remain in the app folder."""
import os
from pathlib import Path
import runpy
import sys

root = Path(__file__).resolve().parent
os.chdir(root)
(root / 'data').mkdir(exist_ok=True)
(root / 'data' / 'app-process.txt').write_text(str(os.getpid()))
sys.stdout = (root / 'app.log').open('w', encoding='utf-8', buffering=1)
sys.stderr = (root / 'error.log').open('w', encoding='utf-8', buffering=1)
sys.argv = [str(root / 'server.py'), '--wifi', '--no-browser', '--diagnostics']
runpy.run_path(str(root / 'server.py'), run_name='__main__')
