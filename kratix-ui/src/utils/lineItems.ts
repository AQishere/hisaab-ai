export interface LineDraft {
  productID: string;
  quantity: string;
  ratePerUnit: string;
}

export const emptyLine = (): LineDraft => ({ productID: '', quantity: '', ratePerUnit: '' });

export const lineTotal = (l: LineDraft) => (Number(l.quantity) || 0) * (Number(l.ratePerUnit) || 0);

export const linesValid = (lines: LineDraft[]) =>
  lines.length > 0 && lines.every((l) => l.productID && Number(l.quantity) > 0 && Number(l.ratePerUnit) >= 0 && l.ratePerUnit !== '');

export const toLinePayload = (lines: LineDraft[]) =>
  lines.map((l) => ({ productID: Number(l.productID), quantity: Number(l.quantity), ratePerUnit: Number(l.ratePerUnit) }));
