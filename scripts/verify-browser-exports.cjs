// Start the isolated review API on 5071, Vite on 3000 (pointing at that API),
// and a separate headless Chrome with --remote-debugging-port=9222.
// Run after verify-workflows.cjs to supply invoices and stock in the test DB.
// This script reads records and downloads files; it never writes business data.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { setTimeout: delay } = require('node:timers/promises');
const root = path.resolve(__dirname, '..');
const out = path.join(root, 'artifacts', 'cleanup-browser');
fs.mkdirSync(out, { recursive: true });
let checks = 0;
const check = (value, message) => { assert.ok(value, message); checks++; };

async function connect(url) {
  const socket = new WebSocket(url);
  await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
  let seq = 0;
  const pending = new Map(), listeners = new Map();
  socket.onmessage = ({ data }) => {
    const msg = JSON.parse(data);
    if (msg.id) {
      const p = pending.get(msg.id); if (!p) return;
      pending.delete(msg.id); clearTimeout(p.timer);
      msg.error ? p.reject(Error(msg.error.message)) : p.resolve(msg.result);
    } else for (const fn of listeners.get(msg.method) || []) fn(msg.params);
  };
  return {
    call(method, params = {}) {
      return new Promise((resolve, reject) => {
        const id = ++seq;
        const timer = setTimeout(() => { pending.delete(id); reject(Error('CDP timeout: ' + method)); }, 20000);
        pending.set(id, { resolve, reject, timer });
        socket.send(JSON.stringify({ id, method, params }));
      });
    },
    on(method, fn) { listeners.set(method, [...(listeners.get(method) || []), fn]); },
    close() { socket.close(); },
  };
}

async function main() {
  const config = JSON.parse(fs.readFileSync(path.join(root, 'CrescentFlourMill/Kratix.ERP/appsettings.Development.json'), 'utf8'));
  const owner = config.DemoUsers.find(u => u.Role === 'Owner');
  const authResponse = await fetch('http://localhost:5071/api/auth/login', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: owner.Username, password: owner.Password }),
  });
  check(authResponse.ok, 'Review owner can sign in');
  const auth = await authResponse.json();
  const headers = { Authorization: 'Bearer ' + auth.token };
  const invoices = await (await fetch('http://localhost:5071/api/invoices', { headers })).json();
  check(invoices.length > 0, 'Review invoices exist');
  const browserInfo = await (await fetch('http://localhost:9222/json/version')).json();
  const browser = await connect(browserInfo.webSocketDebuggerUrl);
  const target = await (await fetch('http://localhost:9222/json/new?about:blank', { method: 'PUT' })).json();
  const page = await connect(target.webSocketDebuggerUrl);
  const errors = [], downloads = new Map();
  page.on('Runtime.exceptionThrown', e => errors.push(e.exceptionDetails.text));
  browser.on('Browser.downloadWillBegin', e => downloads.set(e.guid, { name: e.suggestedFilename, done: false }));
  browser.on('Browser.downloadProgress', e => { if (e.state === 'completed' && downloads.has(e.guid)) downloads.get(e.guid).done = true; });
  const evaluate = async expression => {
    const result = await page.call('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
    if (result.exceptionDetails) throw Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text);
    return result.result.value;
  };
  const waitFor = async (fn, label) => {
    const deadline = Date.now() + 20000;
    while (Date.now() < deadline) { if (await fn()) return; await delay(100); }
    throw Error('Timed out: ' + label);
  };
  const ready = () => waitFor(() => evaluate("document.readyState === 'complete' && !document.querySelector('[role=progressbar]')"), 'page ready');
  const navigate = async route => { await page.call('Page.navigate', { url: 'http://localhost:3000' + route }); await delay(250); await ready(); };
  const clickText = async (selector, label) => evaluate(`(() => { const el = [...document.querySelectorAll(${JSON.stringify(selector)})].find(e => e.textContent.trim() === ${JSON.stringify(label)}); if (!el || el.disabled) throw Error('Missing/disabled button'); el.click(); })()`);
  try {
    await page.call('Page.enable'); await page.call('Runtime.enable');
    await page.call('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
    await browser.call('Browser.setDownloadBehavior', { behavior: 'allow', downloadPath: out, eventsEnabled: true });
    await page.call('Page.addScriptToEvaluateOnNewDocument', { source: `sessionStorage.setItem('authToken', ${JSON.stringify(auth.token)}); sessionStorage.setItem('user', ${JSON.stringify(JSON.stringify(auth.user))}); window.__printCalls=0; window.print=()=>{window.__printCalls++};` });
    for (const route of ['dashboard','suppliers','customers','products','warehouses','purchase-orders','inbound-receipts','production','inventory','sales-orders','invoices','payments','users']) {
      await navigate('/' + route);
      await waitFor(() => evaluate("!!document.querySelector('main')"), route + ' layout');
      await ready();
      check(await evaluate("!!document.querySelector('main') && !document.body.innerText.includes('Could not load data.')"), route + ' loads');
    }
    await navigate('/reports');
    await waitFor(() => evaluate("document.querySelectorAll('[role=tab]').length === 4"), 'report tabs');
    for (const [kind,label] of [['stock','Stock'],['production','Production'],['sales','Sales'],['receivables','Receivables ageing']]) {
      await clickText('[role=tab]', label);
      await waitFor(() => evaluate("[...document.querySelectorAll('button')].some(b => b.textContent.trim()==='CSV' && !b.disabled)"), label + ' data');
      const before = downloads.size;
      await clickText('button','CSV');
      await waitFor(() => downloads.size > before && [...downloads.values()].every(d=>d.done), label + ' download');
      const download = [...downloads.values()].at(-1);
      check(download.name.startsWith(kind+'-') && download.name.endsWith('.csv'), label + ' filename');
      const csv = fs.readFileSync(path.join(out, download.name));
      check(csv.subarray(0,3).equals(Buffer.from([239,187,191])), label + ' UTF-8 BOM');
      check(csv.toString('utf8').includes('\r\n'), label + ' contains records');
      await clickText('button','Print');
      check(await evaluate('window.__printCalls > 0'), label + ' print command');
      await page.call('Emulation.setEmulatedMedia', { media: 'print' });
      check(await evaluate("[...document.querySelectorAll('.MuiDrawer-root,.MuiAppBar-root,.no-print')].every(e=>getComputedStyle(e).display==='none')"), label + ' print hides controls');
      check(await evaluate("[...document.querySelectorAll('table')].every(e => e.scrollWidth <= document.querySelector('main').clientWidth)"), label + ' table fits');
      const pdf = await page.call('Page.printToPDF', { preferCSSPageSize: true, printBackground: true });
      fs.writeFileSync(path.join(out,kind+'.pdf'),Buffer.from(pdf.data,'base64'));
      check(Buffer.from(pdf.data,'base64').subarray(0,5).toString()==='%PDF-', label + ' PDF generated');
      await page.call('Emulation.setEmulatedMedia', { media: '' });
    }
    // Exercise escaping and Excel formula handling through an actual browser download.
    const before = downloads.size;
    await evaluate(`(async()=>{const {downloadCsv}=await import('/src/utils/csv.ts');downloadCsv('csv-edge-cases.csv',['Name','Quantity'],[['comma, quote " and\\nnewline',-12],['=1+1',3],['اردو',0],[null,2]])})()`);
    await waitFor(() => downloads.size>before && [...downloads.values()].every(d=>d.done), 'edge CSV');
    const edge = fs.readFileSync(path.join(out,'csv-edge-cases.csv'),'utf8');
    check(edge.includes('"comma, quote "" and\nnewline",-12'), 'CSV quotes and multiline field; numeric negative unchanged');
    check(edge.includes("'=1+1,3"), 'Formula-like name stays text');
    check(edge.includes('اردو,0') && edge.endsWith(',2'), 'Unicode and null CSV values');

    // A slow response must not overwrite a newer filter, and revisiting a filter
    // must show loading until its new request finishes. Use the real hook with a
    // controlled transport so completion order is deterministic.
    await evaluate(`(async()=>{
      const entry=await (await fetch('/src/main.tsx')).text();
      const dep=name=>entry.split('"').find(value=>value.startsWith('/node_modules/.vite/deps/'+name+'.js?'));
      const ReactModule=await import(dep('react'));
      const React=ReactModule.default??ReactModule;
      const dom=await import(dep('react-dom_client'));
      const {createRoot}=dom.default??dom;
      const {useFetch}=await import('/src/hooks/useFetch.ts');
      const {default:client}=await import('/src/api/client.ts');
      const adapter=client.defaults.adapter;
      window.__requests=[];
      client.defaults.adapter=config=>new Promise(resolve=>window.__requests.push({url:config.url,resolve:value=>resolve({data:value,status:200,statusText:'OK',headers:{},config})}));
      const host=document.createElement('div');document.body.appendChild(host);
      const root=createRoot(host);
      function Probe(){const[url,setUrl]=React.useState('/probe-a');const result=useFetch(url);React.useEffect(()=>{window.__probe={...result,setUrl}},[result]);return null;}
      window.__cleanupProbe=()=>{root.unmount();host.remove();client.defaults.adapter=adapter};
      root.render(React.createElement(Probe));
    })()`);
    await waitFor(()=>evaluate('window.__requests.length===1 && !!window.__probe'),'first hook request');
    check(await evaluate('window.__probe.loading && window.__probe.data===null'),'Initial hook loading');
    await evaluate("window.__probe.setUrl('/probe-b')");
    await waitFor(()=>evaluate('window.__requests.length===2'),'second hook request');
    await evaluate("window.__requests[1].resolve('new response')");
    await waitFor(()=>evaluate("window.__probe.data==='new response' && !window.__probe.loading"),'new hook response');
    await evaluate("window.__requests[0].resolve('old response')");
    await delay(100);
    check(await evaluate("window.__probe.data==='new response'"),'Stale response ignored');
    await evaluate('window.__probe.setUrl(null)');
    await waitFor(()=>evaluate('window.__probe.data===null && !window.__probe.loading'),'disabled hook');
    await evaluate("window.__probe.setUrl('/probe-b')");
    await waitFor(()=>evaluate('window.__requests.length===3'),'revisited hook');
    check(await evaluate('window.__probe.loading'),'Revisited URL waits for fresh result');
    await evaluate("window.__requests[2].resolve('refreshed')");
    await waitFor(()=>evaluate("window.__probe.data==='refreshed' && !window.__probe.loading"),'refreshed hook');
    await evaluate('window.__probe.reload()');
    await waitFor(()=>evaluate('window.__requests.length===4'),'explicit reload');
    check(await evaluate("window.__probe.loading && window.__probe.data==='refreshed'"),'Reload keeps rows while loading');
    await evaluate('window.__cleanupProbe()');

    await navigate('/invoices/'+invoices[0].invoiceID+'/print');
    await waitFor(() => evaluate("!!document.querySelector('.invoice-toolbar .primary')"), 'invoice ready');
    check(await evaluate("document.body.innerText.includes('Demo address:') && document.body.innerText.includes('accounts@crescent-flour.example')"), 'Dummy company details shown');
    await clickText('button','Print / Save as PDF');
    check(await evaluate('window.__printCalls === 1'), 'Invoice print command');
    await page.call('Emulation.setEmulatedMedia', { media: 'print' });
    check(await evaluate("getComputedStyle(document.querySelector('.invoice-toolbar')).display==='none'"), 'Invoice toolbar hidden');
    const pdf=await page.call('Page.printToPDF',{preferCSSPageSize:true,printBackground:true});
    check((Buffer.from(pdf.data,'base64').toString('latin1').match(/\/Type \/Page\b/g)||[]).length===1,'Short invoice fits on one PDF page');
    fs.writeFileSync(path.join(out,'invoice.pdf'),Buffer.from(pdf.data,'base64'));
    const shot=await page.call('Page.captureScreenshot',{format:'png',captureBeyondViewport:true});
    fs.writeFileSync(path.join(out,'invoice-print.png'),Buffer.from(shot.data,'base64'));
    check(errors.length===0, 'No browser runtime exceptions: '+errors.join('; '));
    console.log(`PASS: ${checks} browser checks. CSVs, PDFs and invoice screenshot: artifacts/cleanup-browser/`);
  } catch (error) {
    const shot = await page.call('Page.captureScreenshot', { format: 'png' });
    fs.writeFileSync(path.join(out, 'failure.png'), Buffer.from(shot.data, 'base64'));
    console.error(await evaluate('document.body.innerText.slice(0,1500)'));
    throw error;
  } finally {
    await browser.call('Target.closeTarget',{targetId:target.id});
    page.close(); browser.close();
  }
}
main().catch(e=>{ console.error(e.message); process.exitCode=1; });
