export interface Session { email: string | null; csrf: string; categories: string[]; }
export type ItemKind = 'product' | 'discount';
export interface ExpenseItem {
  name: string;
  amount: number | null;
  quantity?: number | null;
  unit?: string | null;
  unitPrice?: number | null;
  kind: ItemKind;
}
export interface Expense {
  id: string;
  merchant: string;
  date: string;
  amount: number | null;
  category: string;
  source: 'manual' | 'image' | 'pdf' | 'spreadsheet';
  items: ExpenseItem[];
  paymentMethod?: string | null;
  transactionTime?: string | null;
}
export interface ImportRow extends Omit<Expense, 'id' | 'merchant' | 'date'> {
  merchant: string | null;
  date: string | null;
}
export interface ImportPreview { rows: ImportRow[]; warnings: string[]; text?: string | null; }
export interface Dashboard {
  total: number;
  previous: number;
  budget: number | null;
  expenses: Expense[];
  categories: { name: string; amount: number }[];
  days: { day: number; amount: number }[];
  weekdays: { index: number; average: number }[];
}
