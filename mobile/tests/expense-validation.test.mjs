import assert from 'node:assert/strict';
import test from 'node:test';
import { expenseError, imageError, itemError, itemTotal, matchesProduct } from '../src/app/core/expense-validation.ts';

const expense = overrides => ({ id: 'fixed-id', merchant: 'SPAR', date: '2026-11-19',
  amount: 400, category: 'Élelmiszer', source: 'image', items: [{name: 'Tej', amount: 400, kind: 'product'}], ...overrides });

test('negative products are rejected while explicit discounts remain valid', () => {
  assert.ok(itemError({name: 'Tej', amount: -100, kind: 'product'}));
  assert.equal(itemError({name: 'Akció', amount: -100, kind: 'discount'}), null);
  assert.ok(itemError({name: 'Akció', amount: 100, kind: 'discount'}));
  assert.ok(itemError({name: 'Akció', amount: 0, kind: 'discount'}));
  assert.equal(itemError({name: 'Ajándék', amount: 0, kind: 'product'}), null);
});
test('unrecognized and invalid dates remain mandatory corrections', () => {
  for (const date of ['', '2026-02-30', '2026-13-01', 'not-a-date']) assert.ok(expenseError(expense({date})));
  assert.equal(expenseError(expense()), null);
});
test('nonfinite, excessive and overprecise amounts cannot be saved', () => {
  for (const amount of [null, NaN, Infinity, -1, 0, 1000000000, .001]) assert.ok(expenseError(expense({amount})));
});
test('image selection enforces supported extensions and the API byte limit', () => {
  assert.equal(imageError({name:'receipt.JFIF',size:12 * 1024 * 1024}), null);
  for (const file of [{name:'file.exe',size:20},{name:'receipt.jpg',size:0},{name:'receipt.png',size:12 * 1024 * 1024 + 1}]) assert.ok(imageError(file));
});
test('product search matches words instead of unrelated compound products', () => {
  assert.equal(matchesProduct('MAGYAR TEJ 2,8%', 'tej'), true);
  assert.equal(matchesProduct('TEJFÖL 20%', 'tej'), false);
  assert.equal(matchesProduct('SÁRGA BURGONYA', 'sarga burgonya'), true);
  assert.equal(matchesProduct('Tej', ''), false);
});
test('item totals include discounts once using minor-unit arithmetic', () => {
  assert.equal(itemTotal([{name:'A',amount:.1,kind:'product'},{name:'B',amount:.2,kind:'product'},{name:'Akció',amount:-.1,kind:'discount'}]), .2);
});
