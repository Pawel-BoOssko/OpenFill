// One focused regression check for the actual showAsk note shortcut.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const source = fs.readFileSync(path.join(__dirname,
  '..', 'src', 'OpenFill.Core', 'Assets', 'panel__v0.18__2026-10-12_2034.html'), 'utf8');
const match = source.match(/c\.querySelector\('\.note'\)\.addEventListener\('keydown', e => \{([\s\S]*?)\n    \}\);/);
assert.ok(match, 'showAsk note keydown handler not found');
let cases = 0;
function test(name, attrs, expectedSend, expectedPrevent) {
  const deliveries = [];
  let prevented = false;
  const onKeydown = new Function('reply', 'input',
    'return e => {' + match[1] + '};')(
      value => deliveries.push(value), {value:'answer'});
  onKeydown(Object.assign({
    key:'Enter', shiftKey:false, isComposing:false, repeat:false,
    preventDefault() { prevented = true; }
  }, attrs));
  assert.equal(deliveries.length, expectedSend ? 1 : 0, name + ' send');
  if (expectedSend) assert.equal(deliveries[0], 'answer', name + ' content');
  assert.equal(prevented, expectedPrevent, name + ' prevented');
  cases++;
}
test('Enter sends', {}, true, true);
test('Shift+Enter remains newline', {shiftKey:true}, false, false);
test('Held Enter does not resend', {repeat:true}, false, true);
test('IME composition untouched', {isComposing:true}, false, false);
test('Other keys untouched', {key:'a'}, false, false);
console.log('ASK_ENTER_SHORTCUT_PASS cases=' + cases);
