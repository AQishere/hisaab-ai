export interface Supplier {
  supplierID: number;
  supplierName: string;
  contactPerson: string;
  phoneNumber: string;
  ntn: string;
  address: string;
  paymentTerms: string;
  isActive: boolean;
  createdDate: string;
}

export interface Customer {
  customerID: number;
  customerName: string;
  type: string;
  contactPerson: string;
  phoneNumber: string;
  address: string;
  creditLimit: number;
  outstandingBalance: number;
  isActive: boolean;
  createdDate: string;
}

export interface Product {
  productID: number;
  productCode: string;
  productName: string;
  category: string;
  unit: string;
  hsnCode: string;
  createdDate: string;
}

export interface Warehouse {
  warehouseID: number;
  warehouseName: string;
  location: string;
  capacity: number;
  createdDate: string;
}

export interface POLineItem {
  lineItemID: number;
  productID: number;
  productName: string;
  quantity: number;
  unit: string;
  ratePerUnit: number;
  totalAmount: number;
  receivedQuantity: number;
}

export interface PurchaseOrder {
  poid: number;
  poNumber: string;
  supplierID: number;
  supplierName: string;
  poDate: string;
  expectedDeliveryDate: string | null;
  status: string;
  totalAmount: number;
  lineItems: POLineItem[];
  createdDate: string;
}

export interface InboundReceipt {
  receiptID: number;
  receiptNumber: string;
  poid: number;
  poNumber: string;
  truckNumber: string;
  driverName: string;
  driverPhone: string;
  arrivalDateTime: string | null;
  grossWeight: number | null;
  tareWeight: number | null;
  netWeight: number | null;
  moisture: number | null;
  impurities: number | null;
  grainType: string | null;
  qcResult: string | null;
  warehouseID: number | null;
  warehouseName: string | null;
  binCode: string | null;
  lotNumber: string | null;
  status: string;
  receivedDate: string | null;
  createdDate: string;
}

export interface InventoryItem {
  inventoryID: number;
  productID: number;
  productCode: string;
  productName: string;
  category: string;
  unit: string;
  warehouseID: number;
  warehouseName: string;
  binCode: string | null;
  lotNumber: string | null;
  quantityAvailable: number;
  lastUpdatedDate: string;
}

export interface BatchMaterial {
  batchMaterialID: number;
  productID: number;
  productName: string;
  materialType: 'Input' | 'Output' | 'Waste';
  quantity: number;
  sourceLotNumber: string | null;
  isQualityApproved: boolean;
}

export interface ProductionBatch {
  batchID: number;
  batchNumber: string;
  productionDate: string;
  shiftNumber: number;
  status: string;
  totalInputQuantity: number;
  totalOutputQuantity: number;
  totalWasteQuantity: number;
  yieldPercentage: number;
  operatorName: string;
  materials: BatchMaterial[];
  createdDate: string;
}

export interface SOLineItem {
  lineItemID: number;
  productID: number;
  productName: string;
  quantity: number;
  ratePerUnit: number;
  totalAmount: number;
  dispatchedQuantity: number;
  dispatchStatus: string;
  truckNumber: string | null;
  driverName: string | null;
  dispatchDate: string | null;
}

export interface SalesOrder {
  salesOrderID: number;
  soNumber: string;
  customerID: number;
  customerName: string;
  soDate: string;
  expectedDeliveryDate: string | null;
  status: string;
  totalAmount: number;
  lineItems: SOLineItem[];
  createdDate: string;
}

export interface Invoice {
  invoiceID: number;
  invoiceNumber: string;
  salesOrderID: number;
  soNumber: string;
  customerID: number;
  customerName: string;
  invoiceDate: string;
  totalQuantity: number;
  totalAmount: number;
  taxAmount: number;
  netAmount: number;
  status: string;
  dueDate: string | null;
  balanceRemaining: number;
  createdDate: string;
}

export interface Payment {
  paymentID: number;
  invoiceID: number;
  invoiceNumber: string;
  paymentDate: string;
  paymentMode: string;
  paymentAmount: number;
  reference: string;
  createdDate: string;
}
