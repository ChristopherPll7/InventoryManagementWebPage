const fs = require('fs');
const path = require('path');
const vm = require('vm');
const assert = require('assert');
function input(dataset) {
  return { dataset, value: '', error: '', listeners: {},
    setCustomValidity(value) { this.error = value; },
    addEventListener(name, fn) { this.listeners[name] = fn; } };
}
const name = input({ trimMax: '150' });
const quantity = input({});
const document = {
  body: { appendChild() {}, classList: { add() {}, remove() {} } },
  createElement() { return { setAttribute() {} }; },
  querySelector() { return null; },
  querySelectorAll(selector) {
    if (selector === '[data-trim-max]') return [name];
    if (selector === 'input[name="Quantity"]') return [quantity];
    return [];
  },
  addEventListener() {}
};
vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../../src/InventoryManagement.Web/wwwroot/js/site.js'), 'utf8'),
  { document, window: { addEventListener() {} } });
let passed = 0;
function check(input, value, valid) {
  input.value = value;
  input.listeners.input();
  assert.equal(input.error === '', valid);
  assert.equal(input.value, value, 'Validation must not alter the attempted value');
  passed++;
}
check(name, ' ' + 'a'.repeat(150) + ' ', true);
check(name, ' ' + 'a'.repeat(151) + ' ', false);
check(name, 'Corrected', true);
check(quantity, '1.5', false);
check(quantity, 'abc', false);
check(quantity, '0', false);
check(quantity, '2147483648', false);
check(quantity, '2147483647', true);
check(quantity, '1', true);
console.log(`${passed} JavaScript validation checks passed (DOM doubles; not browser end-to-end).`);
