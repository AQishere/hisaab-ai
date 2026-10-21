// Builds the public demo's data: about 75 days of a flour mill's work, entered through the real API so stock, the
// ledger, balances and document numbers are all consistent. Then it moves each simulated day's timestamps onto that
// day and dumps the database to CrescentFlourMill/hisaab-ai/Demo/seed.sql, which the API loads in demo mode.
//
// Needs: Docker container "kratix-pg" (postgres:17) on port 5433 and an empty database DEMO_GEN_DATABASE in it,
// and the API running on DEMO_GEN_BASE_URL against that database with ASPNETCORE_ENVIRONMENT=Development.
//   DEMO_GEN_BASE_URL=http://localhost:5072 DEMO_GEN_DATABASE=kratix_demo_gen node scripts/demo-data/generate.cjs
const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');

const base = process.env.DEMO_GEN_BASE_URL;
const database = process.env.DEMO_GEN_DATABASE;
if (!base || !database?.startsWith('kratix_demo_gen'))
  throw Error('Set DEMO_GEN_BASE_URL and DEMO_GEN_DATABASE (kratix_demo_gen...); see the comment at the top.');
const root = path.resolve(__dirname, '../..');
const DAYS = 75;

// Seeded random numbers, so the same script gives the same data.
let seed = 20261021;
const rand = () => ((seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648);
const between = (lo, hi) => lo + Math.floor(rand() * (hi - lo + 1));
const pick = list => list[Math.floor(rand() * list.length)];
const round = (n, step) => Math.round(n / step) * step;

let token;
async function request(p, method = 'GET', body) {
  const response = await fetch(base + '/api' + p, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) },
    ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
  });
  const raw = await response.text();
  if (!response.ok) throw Error(`${method} ${p}: ${response.status} ${raw.slice(0, 300)}`);
  try { return JSON.parse(raw); } catch { return raw; }
}
const post = (p, b) => request(p, 'POST', b);
const put = (p, b) => request(p, 'PUT', b);
const get = p => request(p);

const pad = n => String(n).padStart(2, '0');
const localStamp = d => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}.${String(d.getMilliseconds()).padStart(3, '0')}`;
const anchor = new Date(); anchor.setHours(0, 0, 0, 0);
const dayDate = offset => { const d = new Date(anchor); d.setDate(d.getDate() + offset); return d; };
const iso = offset => { const d = dayDate(offset); return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`; };
const wait = ms => new Promise(r => setTimeout(r, ms));

// Each simulated day runs inside its own real-time window; afterwards every timestamp in a window is moved onto
// that day's working hours.
const windows = [];
async function day(offset, work) {
  await wait(1100);
  const start = new Date();
  await work();
  await wait(50);
  windows.push({ day: iso(offset), start: localStamp(start), end: localStamp(new Date()) });
}

async function main() {
  const cfg = JSON.parse(fs.readFileSync(path.join(root, 'CrescentFlourMill/hisaab-ai/appsettings.Development.json'), 'utf8').replace(/^﻿/, ''));
  const owner = cfg.DemoUsers.find(u => u.Role === 'Owner');
  token = (await post('/auth/login', { username: owner.Username, password: owner.Password })).token;
  if ((await get('/products')).length) throw Error('The database already has products; start from an empty one.');

  let m = {};
  await day(-DAYS, async () => {
    m.rawGodown = await post('/warehouses', { warehouseName: 'Raw Wheat Godown', location: 'Block A, mill compound', capacity: 1500000 });
    m.fgStore = await post('/warehouses', { warehouseName: 'Finished Goods Store', location: 'Block B, loading bay', capacity: 600000 });
    m.packStore = await post('/warehouses', { warehouseName: 'Packaging Store', location: 'Block C', capacity: 50000 });
    const supplier = (supplierName, contactPerson, phoneNumber, address, paymentTerms) =>
      post('/suppliers', { supplierName, contactPerson, phoneNumber, ntn: '', address, paymentTerms });
    m.suppliers = [
      await supplier('Sindh Grain Traders', 'Imran Memon', '0300-2145870', 'Grain Market, Hyderabad', '30 days'),
      await supplier('Punjab Agro Supplies', 'Bilal Chaudhry', '0321-6654120', 'Ghalla Mandi, Okara', '15 days'),
      await supplier("Farmer's Cooperative Sahiwal", 'Rana Tariq', '0333-7012459', 'Sahiwal', 'Cash on delivery'),
      await supplier('Bahawalpur Wheat Co.', 'Asif Baloch', '0345-8820147', 'Bahawalpur', '30 days'),
    ];
    m.bagSupplier = await supplier('Lahore Packaging Industries', 'Usman Sheikh', '0300-4417785', 'Kot Lakhpat, Lahore', '30 days');
    const customer = (customerName, type, contactPerson, phoneNumber, address, creditLimit) =>
      post('/customers', { customerName, type, contactPerson, phoneNumber, address, creditLimit });
    m.customers = [
      await customer('Karachi Wholesale Mart', 'Wholesaler', 'Faisal Siddiqui', '0300-2290041', 'Jodia Bazaar, Karachi', 6000000),
      await customer('Lahore Bakery Group', 'Bakery', 'Hina Aslam', '0322-4100982', 'Gulberg III, Lahore', 4000000),
      await customer('Faisalabad Distributors', 'Distributor', 'Kamran Javed', '0301-7765210', 'Satiana Road, Faisalabad', 8000000),
      await customer('Ali Sons Retail', 'Retailer', 'Ali Raza', '0315-3328870', 'Saddar, Rawalpindi', 1500000),
      await customer('Multan Food Distributors', 'Distributor', 'Shahid Qureshi', '0306-6120347', 'Vehari Road, Multan', 5000000),
      await customer('Golden Crust Bakers', 'Bakery', 'Sana Mirza', '0333-5129904', 'F-10 Markaz, Islamabad', 2500000),
      await customer('Hyderabad Kiryana Network', 'Wholesaler', 'Naveed Shah', '0302-3871260', 'Latifabad, Hyderabad', 3000000),
    ];
    const product = (productCode, productName, category, unit, kgPerBag = null) =>
      post('/products', { productCode, productName, category, unit, kgPerBag, hsnCode: '' });
    m.wheat = await product('WHT-001', 'Wheat (Raw)', 'Raw Material', 'kg');
    m.atta20 = await product('ATTA-20', 'Chakki Atta 20 kg', 'Finished Good', 'bag', 20);
    m.atta10 = await product('ATTA-10', 'Chakki Atta 10 kg', 'Finished Good', 'bag', 10);
    m.maida = await product('MAIDA-50', 'Fine Maida 50 kg', 'Finished Good', 'bag', 50);
    m.suji = await product('SUJI-25', 'Suji 25 kg', 'Finished Good', 'bag', 25);
    m.bran = await product('BRAN-001', 'Wheat Bran (Chokar)', 'By-product', 'kg');
    m.waste = await product('WST-001', 'Cleaning Waste', 'Waste', 'kg');
    m.bags = await product('PKG-BAG', 'Printed PP Bag', 'Packaging', 'piece');
  });

  const line = (p, quantity, ratePerUnit) => ({ productID: p.productID, quantity, ratePerUnit });
  const prices = () => ({ atta20: between(2380, 2480), atta10: between(1220, 1270), maida: between(6900, 7200), suji: between(3600, 3800), bran: between(58, 66) });
  const trucks = ['LES-4412', 'KHI-7730', 'MNA-2291', 'FDA-6610', 'HYD-1187', 'LHR-9023', 'SKT-5501', 'BWP-3378'];
  const drivers = ['Gul Khan', 'Akram Ali', 'Nazir Ahmed', 'Sher Muhammad', 'Iqbal Hussain', 'Rafiq Masih', 'Javed Iqbal'];
  const operators = ['Shakeel (Shift A)', 'Mushtaq (Shift B)', 'Arif (Shift A)'];
  const lots = { wheat: 0, bags: 0 };

  const wheatPO = async (offset, qty, send = true, expected = offset + 2) => {
    const po = await post('/purchaseorders', { supplierID: pick(m.suppliers).supplierID, poDate: iso(offset), expectedDeliveryDate: iso(expected),
      lineItems: [line(m.wheat, qty, between(98, 108))] });
    if (send) await put('/purchaseorders/' + po.poid, { status: 'Sent', expectedDeliveryDate: iso(expected) });
    return po;
  };
  // One truck through gate, weighbridge, QC and GRN. stopAt leaves it part-way (for the "needs attention" list).
  const truck = async (po, net, stopAt = null, reject = false) => {
    const r = await post('/inboundreceipts/gate-entry', { poid: po.poid, poLineItemID: po.lineItems[0].lineItemID, truckNumber: pick(trucks), driverName: pick(drivers), driverPhone: '' });
    if (stopAt === 'gate') return r;
    const tare = between(9000, 12000);
    await put(`/inboundreceipts/${r.receiptID}/weighbridge`, { grossWeight: net + tare, tareWeight: tare });
    if (stopAt === 'weighed') return r;
    await put(`/inboundreceipts/${r.receiptID}/qc`, reject
      ? { moisture: 15.8, impurities: 4.2, grainType: 'Soft wheat', qcResult: 'Rejected' }
      : { moisture: between(105, 125) / 10, impurities: between(5, 18) / 10, grainType: pick(['Hard red wheat', 'Galaxy-2013', 'Akbar-2019']), qcResult: 'Approved' });
    if (reject || stopAt === 'qc') return r;
    await put(`/inboundreceipts/${r.receiptID}/grn`, { warehouseID: m.rawGodown.warehouseID, lotNumber: `WL-${pad(++lots.wheat)}`, binCode: '' });
    return r;
  };
  const receiveWheat = async po => {
    const ordered = po.lineItems[0].quantity;
    const first = round(ordered * (between(45, 55) / 100), 10);
    await truck(po, first);
    await truck(po, round(ordered - first + between(-150, 150), 10));
    const detail = await get('/purchaseorders/' + po.poid);
    if (detail.status !== 'Received') await put(`/purchaseorders/${po.poid}/close`, { reason: 'Supplier delivered slightly under the order' });
  };
  const buyBags = async offset => {
    const po = await post('/purchaseorders', { supplierID: m.bagSupplier.supplierID, poDate: iso(offset), expectedDeliveryDate: iso(offset),
      lineItems: [line(m.bags, 6000, between(32, 36))] });
    await put('/purchaseorders/' + po.poid, { status: 'Sent', expectedDeliveryDate: iso(offset) });
    await post('/inboundreceipts/count-receipt', { poid: po.poid, poLineItemID: po.lineItems[0].lineItemID, quantity: 6000,
      warehouseID: m.packStore.warehouseID, lotNumber: `BAG-${pad(++lots.bags)}`, binCode: '' });
  };

  const stock = async () => get('/inventory');
  // Mill one batch from the oldest wheat lots. finish=false leaves it in progress.
  const mill = async (offset, target, finish = true) => {
    const batch = await post('/productionbatches', { productionDate: iso(offset), shiftNumber: between(1, 2), operatorName: pick(operators) });
    const bpath = '/productionbatches/' + batch.batchID;
    let input = 0;
    for (const lot of (await stock()).filter(i => i.productID === m.wheat.productID && i.quantityAvailable > 0)) {
      if (input >= target) break;
      const qty = Math.min(lot.quantityAvailable, target - input);
      await post(bpath + '/add-input', { inventoryID: lot.inventoryID, productID: m.wheat.productID, quantity: qty, sourceLotNumber: lot.lotNumber, sourceWarehouseID: lot.warehouseID });
      input += qty;
    }
    if (!input) return null;
    const share = (pct, kgPerBag) => Math.floor(input * pct / 100 / kgPerBag) * kgPerBag;
    const outputs = [[m.atta20, share(between(46, 50), 20)], [m.atta10, share(between(12, 15), 10)], [m.maida, share(between(9, 11), 50)], [m.suji, share(between(3, 4), 25)]];
    const waste = round(input * between(18, 26) / 1000, 1);
    const bran = Math.round((input - outputs.reduce((s, [, q]) => s + q, 0) - waste) * 1000) / 1000;
    for (const [p, q] of [...outputs, [m.bran, bran]]) if (q > 0) await post(bpath + '/add-output', { productID: p.productID, quantity: q, materialType: 'Output' });
    await post(bpath + '/add-output', { productID: m.waste.productID, quantity: waste, materialType: 'Waste' });
    if (!finish) return batch;
    const materials = (await get(bpath)).materials.filter(x => x.materialType === 'Output' && !x.isReversed);
    for (const x of materials) await put(`${bpath}/approve-output/${x.batchMaterialID}`, {});
    await put(bpath + '/finalize', { warehouseID: m.fgStore.warehouseID });
    const bagLots = (await stock()).filter(i => i.productID === m.bags.productID && i.quantityAvailable > 0);
    for (const x of materials) {
      const product = [m.atta20, m.atta10, m.maida, m.suji].find(p => p.productID === x.productID);
      if (!product) continue;
      let kgLeft = x.quantity;
      for (const lot of bagLots) {
        const kg = Math.min(kgLeft, Math.floor(lot.quantityAvailable) * product.kgPerBag);
        if (kg <= 0) continue;
        await post(`${bpath}/pack-output/${x.batchMaterialID}`, { quantityKg: kg, warehouseID: m.fgStore.warehouseID, packagingInventoryID: lot.inventoryID });
        lot.quantityAvailable -= kg / product.kgPerBag;
        kgLeft -= kg;
      }
    }
    return batch;
  };

  const available = async p => (await stock()).filter(i => i.productID === p.productID && i.stockForm === 'Standard').reduce((s, i) => s + i.quantityAvailable, 0);
  // A sales order sized to the stock on hand.
  const sell = async (offset, customer, wanted) => {
    const price = prices();
    const items = [];
    for (const [p, qty, rate] of wanted(price)) {
      const have = await available(p);
      const q = Math.min(qty, p.unit === 'bag' ? Math.floor(have * 0.6) : round(have * 0.6, 10));
      if (q > 0) items.push(line(p, q, rate));
    }
    if (!items.length) return null;
    return post('/salesorders', { customerID: customer.customerID, soDate: iso(offset), expectedDeliveryDate: iso(offset + between(0, 2)), lineItems: items });
  };
  const dispatchAll = async (so, partial = false) => {
    for (const l of so.lineItems) {
      const q = partial ? (l.quantity > 1 ? Math.floor(l.quantity / 2) : 0) : l.quantity;
      if (q > 0) await put(`/salesorders/${so.salesOrderID}/dispatch/${l.lineItemID}`, { dispatchedQuantity: q, truckNumber: pick(trucks), driverName: pick(drivers) });
    }
  };
  const invoices = [];
  const invoice = async (so, offset, dueDays) => {
    const inv = await post('/invoices', { salesOrderID: so.salesOrderID, invoiceDate: iso(offset), taxAmount: 0, dueDays });
    invoices.push({ ...inv, offset, dueDays });
    return inv;
  };
  const pay = (inv, offset, amount, mode = pick(['Bank Transfer', 'Bank Transfer', 'Cheque', 'Cash'])) =>
    post('/payments', { invoiceID: inv.invoiceID, paymentDate: iso(offset), paymentMode: mode, paymentAmount: amount,
      reference: mode === 'Cheque' ? `CHQ-${between(100000, 999999)}` : mode === 'Bank Transfer' ? `TRX${between(10000000, 99999999)}` : '' });

  const mixes = [
    p => [[m.atta20, between(150, 400), p.atta20], [m.atta10, between(50, 150), p.atta10]],
    p => [[m.maida, between(30, 90), p.maida], [m.suji, between(10, 40), p.suji]],
    p => [[m.atta20, between(200, 500), p.atta20], [m.bran, between(1500, 4000), p.bran]],
    p => [[m.atta20, between(80, 200), p.atta20], [m.maida, between(10, 40), p.maida], [m.atta10, between(40, 120), p.atta10]],
    p => [[m.bran, between(2000, 6000), p.bran]],
  ];
  const openPOs = [];
  const toInvoice = [];

  for (let offset = -DAYS + 1; offset <= 0; offset++) {
    const recent = offset > -4;
    await day(offset, async () => {
      if ((offset + DAYS) % 18 === 1) await buyBags(offset);
      if ((offset + DAYS) % 4 === 1 && offset < -2) openPOs.push({ po: await wheatPO(offset, round(between(36000, 52000), 1000)), due: offset + 2 });
      for (const o of openPOs.filter(o => o.due === offset)) await receiveWheat(o.po);
      if (offset === -30) {
        const po = await wheatPO(offset, 20000);
        await truck(po, 9800, null, true);
        await truck(po, 10040);
        await put(`/purchaseorders/${po.poid}/close`, { reason: 'First truck rejected at QC for moisture; supplier will not resend' });
      }
      if (offset % 2 === 0 || offset === -1) await mill(offset, round(between(13000, 17000), 100));
      if (offset === 0) await mill(offset, 8000, false);  // the shift still running
      const orders = between(1, 3);
      for (let i = 0; i < orders; i++) {
        const so = await sell(offset, pick(m.customers), pick(mixes));
        if (!so) continue;
        if (recent && i === 0) continue;               // confirmed, waiting for trucks
        if (offset === -1 && i === 1) { await dispatchAll(so, true); continue; }
        await dispatchAll(so);
        toInvoice.push({ so, offset });
      }
      for (const t of toInvoice.filter(t => t.offset === offset)) {
        const inv = await invoice(t.so, offset, pick([7, 15, 15, 30]));
        const age = -offset;
        const r = rand();
        if (age > 35) await pay(inv, offset + between(3, inv.dueDays ?? 15), inv.netAmount);
        else if (age > 12 && r < 0.75) await pay(inv, offset + between(2, 10), inv.netAmount);
        else if (r < 0.35) await pay(inv, offset, Math.round(inv.netAmount * 0.4));
      }
    });
  }
  // Late payments that land "today", and wheat that is ordered but not yet in.
  await day(0, async () => {
    const po = await wheatPO(-1, 40000, true, 0);
    await truck(po, 19960, 'weighed');
    await wheatPO(0, 45000, true, 3);
    await wheatPO(0, 30000, false, 5);
    const unpaid = invoices.filter(i => i.offset < -20 && i.offset > -35);
    for (const inv of unpaid.slice(0, 2)) {
      const due = (await get('/invoices/' + inv.invoiceID)).balanceRemaining;
      if (due > 0) await pay(inv, 0, due);
    }
  });
  console.log(`Entered ${DAYS} days: ${invoices.length} invoices.`);

  // Move each window onto its day, 09:00 to 18:00, then dump.
  const shift = windows.map(w => `('${w.day}'::timestamp, '${w.start}'::timestamp, '${w.end}'::timestamp)`).join(',\n  ');
  const sql = `
CREATE TEMP TABLE demo_windows(day timestamp, ws timestamp, we timestamp);
INSERT INTO demo_windows VALUES
  ${shift};
DO $$
DECLARE c record;
BEGIN
  FOR c IN SELECT table_name, column_name FROM information_schema.columns
           WHERE table_schema = 'public' AND data_type = 'timestamp without time zone' AND table_name <> '__EFMigrationsHistory'
  LOOP
    EXECUTE format('UPDATE %I t SET %I = w.day + interval ''9 hours'' + (t.%I - w.ws) * (32400.0 / GREATEST(EXTRACT(EPOCH FROM w.we - w.ws), 1))
                    FROM demo_windows w WHERE t.%I BETWEEN w.ws AND w.we', c.table_name, c.column_name, c.column_name, c.column_name);
  END LOOP;
END $$;
-- The seed is public: no hashes of the development passwords. The API sets an unusable one on every load.
UPDATE "Users" SET "PasswordHash" = 'set-on-load';`;
  execFileSync('docker', ['exec', '-i', 'kratix-pg', 'psql', '-v', 'ON_ERROR_STOP=1', '-q', '-U', 'postgres', '-d', database], { input: sql, stdio: ['pipe', 'inherit', 'inherit'] });
  const dump = execFileSync('docker', ['exec', 'kratix-pg', 'pg_dump', '-U', 'postgres', '--data-only', '--column-inserts', '--no-owner',
    '--exclude-table=public."__EFMigrationsHistory"', database], { maxBuffer: 256 * 1024 * 1024 }).toString();
  const body = dump.split('\n').filter(l => !l.startsWith('\\') && !l.startsWith('--') && !/^SET |set_config\('search_path'/.test(l) && l.trim()).join('\n');
  const out = path.join(root, 'CrescentFlourMill/hisaab-ai/Demo/seed.sql');
  fs.mkdirSync(path.dirname(out), { recursive: true });
  fs.writeFileSync(out, `-- anchor: ${iso(0)}\n-- Generated by scripts/demo-data/generate.cjs; do not edit by hand.\n${body}\n`);
  console.log(`Wrote ${path.relative(root, out)} (${Math.round(fs.statSync(out).size / 1024)} KB).`);
}
main().catch(err => { console.error(err.message); process.exitCode = 1; });
