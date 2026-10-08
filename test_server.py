import copy
import http.client
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import threading
import unittest

class ServerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.TemporaryDirectory()
        app = Path(cls.tmp.name)
        shutil.copy(Path(__file__).with_name('server.py'), app / 'server.py')
        spec = importlib.util.spec_from_file_location('boutique_test_server', app / 'server.py')
        cls.m = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.m)
        cls.server = cls.m.ThreadingHTTPServer(('127.0.0.1', 0), cls.m.Handler)
        cls.port = cls.server.server_port
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()
        cls.seed = json.loads(Path(__file__).with_name('initial-workbook-import.json').read_text(encoding='utf-8'))
    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.tmp.cleanup()
    def request(self, path, body=None, cookie=None, origin=True):
        con = http.client.HTTPConnection('127.0.0.1', self.port)
        headers = {'Content-Type':'application/json'}
        if origin: headers['Origin'] = 'http://127.0.0.1:'+str(self.port)
        if cookie: headers['Cookie'] = cookie
        con.request('POST' if body is not None else 'GET', path, json.dumps(body) if body is not None else None, headers)
        res = con.getresponse()
        data = json.loads(res.read())
        result = res.status, data, res.getheader('Set-Cookie')
        con.close()
        return result
    def test_http_save_restore_conflicts_and_auth(self):
        self.assertEqual(self.request('/api/state')[0],401)
        self.assertEqual(self.request('/api/login',{'pin':self.m.PIN},origin=False)[0],403)
        status, _, cookie = self.request('/api/login',{'pin':self.m.PIN})
        self.assertEqual(status,200)
        cookie = cookie.split(';')[0]
        original = self.request('/api/state',cookie=cookie)[1]
        seed=copy.deepcopy(self.seed)
        seed['revision']=original['revision']
        status,saved,_=self.request('/api/state',seed,cookie)
        self.assertEqual(status,200)
        self.assertEqual(saved['revision'],1)
        self.assertEqual(self.request('/api/state',seed,cookie)[0],409)
        invalid=copy.deepcopy(saved);invalid['products'][0]['qty']=1
        self.assertEqual(self.request('/api/state',invalid,cookie)[0],400)
        self.assertEqual(self.request('/data/boutique.sqlite3',cookie=cookie)[0],404)
        self.assertTrue((Path(self.tmp.name)/'data/last-good-backup.json').exists())
        restored=copy.deepcopy(saved);restored['settings']['name']='Restored boutique'
        self.assertEqual(self.request('/api/restore',restored,cookie)[0],200)
        self.assertEqual(self.request('/api/state',cookie=cookie)[1]['settings']['name'],'Restored boutique')
    def test_validation_inventory_and_refunds(self):
        seed=copy.deepcopy(self.seed);self.m.validate(seed)
        p=seed['products'][0]
        s=dict(id='test-sale',product=p['id'],qty=1,date='2026-10-07',payment='Cash',price=100,tax=8.75,expense=0,taxable=True,reason='')
        seed['sales'].append(s);self.m.validate(seed)
        s['qty']=10
        with self.assertRaises(ValueError): self.m.validate(seed)
        s['qty']=1;s['taxable']=False
        with self.assertRaises(ValueError): self.m.validate(seed)
        s['taxable']=True
        seed['returns']=[dict(id='r',sale=s['id'],date='2026-10-08',restock=True)]
        self.m.validate(seed)
        seed['returns'].append(dict(id='r2',sale=s['id'],date='2026-10-09',restock=True))
        with self.assertRaises(ValueError): self.m.validate(seed)
    def test_historical_review_preserves_stock(self):
        seed=copy.deepcopy(self.seed);p=seed['products'][0];pending=seed['pending'][0]
        p['openingSold']-=1;pending['qty']-=1
        seed['sales'].append(dict(id='verified',product=p['id'],qty=1,date='2026-06-09',payment='Cash',price=100,tax=8.75,expense=0,taxable=True,reason=''))
        self.m.validate(seed)
        p['openingSold']-=1
        with self.assertRaises(ValueError): self.m.validate(seed)

    def test_z_shutdown(self):
        self.assertEqual(self.request('/api/shutdown', {'ok':True})[0],401)
        status, _, cookie = self.request('/api/login', {'pin':self.m.PIN})
        self.assertEqual(status,200)
        self.assertEqual(self.request('/api/shutdown', {'ok':True},cookie.split(';')[0])[0],200)

    def test_shipping_validation(self):
        seed=copy.deepcopy(self.seed)
        sale=dict(id='shipping-test',product=seed['products'][0]['id'],qty=1,date='2026-10-07',payment='Zelle',price=102,tax=0,expense=0,taxable=False,reason='Documented out-of-state sale',shippingCharge=10,shippingCost=None,shippingTaxable=0)
        seed['sales'].append(sale);self.m.validate(seed)
        sale['shippingCost']=-1
        with self.assertRaises(ValueError): self.m.validate(seed)
        sale['shippingCost']=7;sale['taxable']=True;sale['tax']=8.93
        with self.assertRaises(ValueError): self.m.validate(seed)
        sale['shippingReason']='Separately stated carrier delivery; receipt retained'
        self.m.validate(seed)
        sale['shippingTaxable']=11
        with self.assertRaises(ValueError): self.m.validate(seed)

if __name__ == '__main__': unittest.main()
