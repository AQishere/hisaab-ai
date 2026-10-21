import React from 'react';
import { useParams } from 'react-router-dom';
import type { Customer, Invoice, Payment } from '../api/types';
import { useFetch } from '../hooks/useFetch';
import { formatDate, formatMoney, formatNumber, unitLabel } from '../utils/format';
import { COMPANY } from '../config/company';

// A paper invoice: always black on white regardless of the app theme. "Save as PDF" in the browser's
// print dialog produces the PDF. The toolbar is hidden when printing.
const PRINT_CSS = `
  .invoice-page { flex: 1; width: 100%; box-sizing: border-box; background: #f3f3f3; min-height: 100vh; padding: 24px 16px; color: #111; font-family: Inter, 'Segoe UI', Arial, sans-serif; }
  .invoice-sheet { background: #fff; max-width: 820px; margin: 0 auto; padding: 48px 56px; border-radius: 8px; box-shadow: 0 1px 4px rgba(0,0,0,.12); }
  .invoice-toolbar { max-width: 820px; margin: 0 auto 16px; display: flex; gap: 8px; justify-content: flex-end; }
  .invoice-toolbar button { font: inherit; font-size: 14px; font-weight: 600; padding: 8px 16px; border-radius: 8px; border: 1px solid #111; background: #fff; color: #111; cursor: pointer; }
  .invoice-toolbar button.primary { background: #0e5a3a; border-color: #0e5a3a; color: #fbf6ea; }
  .invoice-sheet h1 { font-size: 28px; margin: 0; letter-spacing: -0.5px; }
  .invoice-sheet table { width: 100%; border-collapse: collapse; font-size: 13px; }
  .invoice-sheet th { text-align: left; font-size: 11px; text-transform: uppercase; letter-spacing: .5px; color: #555; border-bottom: 2px solid #111; padding: 8px 6px; }
  .invoice-sheet td { border-bottom: 1px solid #ddd; padding: 8px 6px; vertical-align: top; }
  .invoice-sheet .num { text-align: right; white-space: nowrap; }
  .invoice-muted { color: #555; font-size: 13px; line-height: 1.6; }
  .invoice-label { font-size: 11px; text-transform: uppercase; letter-spacing: .5px; color: #555; margin-bottom: 4px; }
  .invoice-totals td { border: none; padding: 4px 6px; }
  .invoice-totals tr.net td { font-size: 16px; font-weight: 700; border-top: 2px solid #111; padding-top: 8px; }
  @media (max-width: 640px) { .invoice-sheet { padding: 24px 16px; } .invoice-head { flex-direction: column; } }
  @media print {
    @page { size: A4; margin: 14mm; }
    .invoice-page { background: #fff; padding: 0; min-height: auto; }
    .invoice-sheet { box-shadow: none; padding: 0; max-width: none; border-radius: 0; }
    .invoice-toolbar { display: none; }
    tr { break-inside: avoid; }
  }
`;

const InvoicePrint: React.FC = () => {
  const { id } = useParams();
  const invoice = useFetch<Invoice>(`/invoices/${id}`);
  const payments = useFetch<Payment[]>(`/payments/invoice/${id}`);
  const customer = useFetch<Customer>(invoice.data ? `/customers/${invoice.data.customerID}` : null);

  if (invoice.error) return <div className="invoice-page"><style>{PRINT_CSS}</style><div className="invoice-sheet">{invoice.error}</div></div>;
  const loadError = payments.error || customer.error;
  if (loadError) return <div className="invoice-page"><style>{PRINT_CSS}</style><div className="invoice-sheet">{loadError}</div></div>;
  if (!invoice.data || payments.loading || customer.loading) return <div className="invoice-page"><style>{PRINT_CSS}</style><div className="invoice-sheet">Loading invoice...</div></div>;

  const inv = invoice.data;
  const paid = inv.netAmount - inv.balanceRemaining;
  const companyLines = [COMPANY.address, COMPANY.phone && `Phone ${COMPANY.phone}`, COMPANY.email, COMPANY.ntn && `NTN ${COMPANY.ntn}`].filter(Boolean);
  const c = customer.data;

  return (
    <div className="invoice-page">
      <style>{PRINT_CSS}</style>
      <div className="invoice-toolbar">
        <button type="button" onClick={() => window.close()}>Close</button>
        <button type="button" className="primary" onClick={() => window.print()}>Print / Save as PDF</button>
      </div>

      <div className="invoice-sheet">
        <div className="invoice-head" style={{ display: 'flex', justifyContent: 'space-between', gap: 24, marginBottom: 32 }}>
          <div>
            <div style={{ fontSize: 20, fontWeight: 700 }}>{COMPANY.name}</div>
            {companyLines.map((l) => <div key={l} className="invoice-muted">{l}</div>)}
          </div>
          <div style={{ textAlign: 'right' }}>
            <h1>INVOICE</h1>
            <div style={{ fontSize: 16, fontWeight: 600, marginTop: 4 }}>{inv.invoiceNumber}</div>
            <div className="invoice-muted">Status: {inv.status === 'PartiallyPaid' ? 'Partly paid' : inv.status}</div>
          </div>
        </div>

        <div className="invoice-head" style={{ display: 'flex', justifyContent: 'space-between', gap: 24, marginBottom: 28 }}>
          <div>
            <div className="invoice-label">Bill to</div>
            <div style={{ fontWeight: 600 }}>{inv.customerName}</div>
            {c?.contactPerson && <div className="invoice-muted">Attn: {c.contactPerson}</div>}
            {c?.address && <div className="invoice-muted">{c.address}</div>}
            {c?.phoneNumber && <div className="invoice-muted">{c.phoneNumber}</div>}
          </div>
          <table style={{ width: 'auto' }}>
            <tbody>
              {[['Invoice date', formatDate(inv.invoiceDate)], ['Due date', formatDate(inv.dueDate)], ['Sales order', inv.soNumber]].map(([k, v]) => (
                <tr key={k}><td className="invoice-muted" style={{ border: 'none', padding: '2px 12px 2px 0' }}>{k}</td><td style={{ border: 'none', padding: '2px 0', fontWeight: 600 }}>{v}</td></tr>
              ))}
            </tbody>
          </table>
        </div>

        <table>
          <thead>
            <tr><th>Item</th><th className="num">Quantity</th><th className="num">Rate</th><th className="num">Amount</th></tr>
          </thead>
          <tbody>
            {inv.lines.map((l) => (
              <tr key={l.invoiceLineID}>
                <td>{l.productName}<div className="invoice-muted">{l.productCode}{l.kgPerBag ? ` · ${formatNumber(l.kgPerBag)} kg per bag` : ''}</div></td>
                <td className="num">{formatNumber(l.quantity, 3)} {unitLabel(l.quantity, l.unit)}</td>
                <td className="num">{formatMoney(l.ratePerUnit)}</td>
                <td className="num">{formatMoney(l.totalAmount)}</td>
              </tr>
            ))}
          </tbody>
        </table>

        <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 16 }}>
          <table className="invoice-totals" style={{ width: 300 }}>
            <tbody>
              <tr><td>Subtotal</td><td className="num">{formatMoney(inv.totalAmount)}</td></tr>
              <tr><td>Tax</td><td className="num">{formatMoney(inv.taxAmount)}</td></tr>
              <tr className="net"><td>Total</td><td className="num">{formatMoney(inv.netAmount)}</td></tr>
              {paid > 0 && <tr><td>Paid</td><td className="num">{formatMoney(paid)}</td></tr>}
              <tr><td style={{ fontWeight: 700 }}>Balance due</td><td className="num" style={{ fontWeight: 700 }}>{formatMoney(inv.balanceRemaining)}</td></tr>
            </tbody>
          </table>
        </div>

        {(payments.data?.length ?? 0) > 0 && (
          <div style={{ marginTop: 32 }}>
            <div className="invoice-label">Payments received</div>
            <table>
              <thead><tr><th>Date</th><th>Mode</th><th>Reference</th><th className="num">Amount</th></tr></thead>
              <tbody>
                {payments.data!.map((p) => (
                  <tr key={p.paymentID}><td>{formatDate(p.paymentDate)}</td><td>{p.paymentMode}</td><td>{p.reference || '-'}</td><td className="num">{formatMoney(p.paymentAmount)}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="invoice-muted" style={{ marginTop: 40, borderTop: '1px solid #ddd', paddingTop: 12 }}>
          Thank you for your business. Please quote {inv.invoiceNumber} with your payment.
        </div>
      </div>
    </div>
  );
};

export default InvoicePrint;
