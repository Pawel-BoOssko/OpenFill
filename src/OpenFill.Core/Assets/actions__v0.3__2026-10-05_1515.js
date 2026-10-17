/*
 * OpenFill - actions on the page.
 * Metadata: wersja 0.3, data 2026-10-05 15:15
 * Changes 0.2: "type" writes character by character (autocomplete with a delay), click with the full sequence of
 * pointer events (libraries reacting to mousedown), marker of a touched field (validation), rect() for
 * "real" clicks and typing through CDP (isTrusted events) on the C# side.
 * Injecting values and clicking with a full set of events, so that pages with controlled
 * fields (React/Vue/ProseMirror) register the change. Experience from Open Browser: just setting
 * .value is not enough - you have to use the native setter and send input/change and keyboard events.
 */
(function () {
  const root = window.__openfill || (window.__openfill = {});
  if (!root.resolve) root.resolve = id => document.querySelector('[data-of-id="' + id + '"]');

  function nativeSet(el, value) {
    const proto = el.nodeName === 'TEXTAREA' ? window.HTMLTextAreaElement.prototype
      : el.nodeName === 'SELECT' ? window.HTMLSelectElement.prototype
        : window.HTMLInputElement.prototype;
    const desc = Object.getOwnPropertyDescriptor(proto, 'value');
    if (desc && desc.set) desc.set.call(el, value); else el.value = value;
  }

  function fire(el, type, extra) {
    el.dispatchEvent(new Event(type, Object.assign({ bubbles: true, cancelable: true }, extra || {})));
  }
  function fireKey(el, type, key) {
    el.dispatchEvent(new KeyboardEvent(type, { bubbles: true, cancelable: true, key: key || '', composed: true }));
  }

  function setText(el, value) {
    el.dataset && (el.dataset.ofTouched = '1');
    el.focus();
    if (el.isContentEditable) {
      el.textContent = '';
      document.execCommand && document.execCommand('insertText', false, value);
      if (!el.textContent) el.textContent = value;
      fire(el, 'input');
      return true;
    }
    nativeSet(el, '');
    fireKey(el, 'keydown');
    nativeSet(el, value);
    fire(el, 'input');
    fire(el, 'change');
    fireKey(el, 'keyup');
    return true;
  }

  // Typing character by character: pages with autocomplete often react only to subsequent input events.
  async function typeText(el, value) {
    el.dataset && (el.dataset.ofTouched = '1');
    el.focus();
    if (el.isContentEditable) { return setText(el, value); }
    nativeSet(el, '');
    fire(el, 'input');
    let cur = '';
    for (const ch of String(value)) {
      fireKey(el, 'keydown', ch);
      fireKey(el, 'keypress', ch);
      cur += ch;
      nativeSet(el, cur);
      el.dispatchEvent(new InputEvent('input', { bubbles: true, cancelable: true, data: ch, inputType: 'insertText' }));
      fireKey(el, 'keyup', ch);
      await new Promise(r => setTimeout(r, 25));
    }
    fire(el, 'change');
    return true;
  }

  function pointerClick(el) {
    const r = el.getBoundingClientRect();
    const opts = { bubbles: true, cancelable: true, composed: true, clientX: r.left + r.width / 2, clientY: r.top + r.height / 2, button: 0 };
    for (const t of ['pointerover', 'pointerenter', 'mouseover', 'pointerdown', 'mousedown']) {
      el.dispatchEvent(t.startsWith('pointer') ? new PointerEvent(t, Object.assign({ pointerType: 'mouse', isPrimary: true }, opts)) : new MouseEvent(t, opts));
    }
    el.focus && el.focus();
    for (const t of ['pointerup', 'mouseup']) {
      el.dispatchEvent(t.startsWith('pointer') ? new PointerEvent(t, Object.assign({ pointerType: 'mouse', isPrimary: true }, opts)) : new MouseEvent(t, opts));
    }
    el.click();
  }

  // Coordinates of the element centre after scrolling - for real input events through CDP.
  root.rect = function (arg) {
    const el = root.resolve(arg.id);
    if (!el) return { ok: false, error: 'NOT_FOUND', id: arg.id };
    el.scrollIntoView && el.scrollIntoView({ block: 'center', inline: 'center' });
    const r = el.getBoundingClientRect();
    let x = r.left + r.width / 2, y = r.top + r.height / 2;
    // Same-origin frames: offset by the iframe position.
    let w = el.ownerDocument.defaultView;
    while (w && w !== window && w.frameElement) { const fr = w.frameElement.getBoundingClientRect(); x += fr.left; y += fr.top; w = w.parent; }
    return { ok: r.width > 0 && r.height > 0, x, y, w: r.width, h: r.height };
  };

  function setSelect(el, value) {
    let matched = false;
    for (const o of Array.from(el.options)) {
      const hit = o.value === value || o.textContent.trim() === value ||
        o.textContent.trim().toLowerCase() === String(value).toLowerCase();
      if (hit) { o.selected = true; matched = true; } else if (!el.multiple) { o.selected = false; }
    }
    fire(el, 'input'); fire(el, 'change');
    return matched;
  }

  function setChecked(el, checked) {
    if (el.checked !== checked) { el.focus(); el.click(); }
    if (el.checked !== checked) { el.checked = checked; fire(el, 'input'); fire(el, 'change'); }
    return el.checked === checked;
  }

  root.act = function (arg) {
    try {
      const el = root.resolve(arg.id);
      if (!el) return { ok: false, error: 'NOT_FOUND', id: arg.id };
      el.scrollIntoView && el.scrollIntoView({ block: 'center', inline: 'center' });
      const tag = el.nodeName.toLowerCase();
      const type = (el.getAttribute('type') || '').toLowerCase();

      switch (arg.action) {
        case 'type': {
          if (tag === 'select' || type === 'checkbox' || type === 'radio') return root.act(Object.assign({}, arg, { action: 'set' }));
          return typeText(el, String(arg.value ?? '')).then(() => ({ ok: true, id: arg.id, value: el.value !== undefined ? el.value : (el.textContent || '') }));
        }
        case 'set': {
          if (tag === 'select') return { ok: setSelect(el, arg.value), id: arg.id, value: el.value };
          if (type === 'checkbox' || type === 'radio') return { ok: setChecked(el, !!arg.value), id: arg.id, checked: el.checked };
          setText(el, String(arg.value ?? ''));
          return { ok: true, id: arg.id, value: el.value !== undefined ? el.value : (el.textContent || '') };
        }
        case 'check': return { ok: setChecked(el, true), id: arg.id, checked: el.checked };
        case 'uncheck': return { ok: setChecked(el, false), id: arg.id, checked: el.checked };
        case 'select': return { ok: setSelect(el, arg.value), id: arg.id, value: el.value };
        case 'click': {
          pointerClick(el);
          return { ok: true, id: arg.id };
        }
        case 'clear': { setText(el, ''); return { ok: true, id: arg.id }; }
        case 'focus': { el.focus(); return { ok: true, id: arg.id }; }
        case 'pressEnter': { fireKey(el, 'keydown', 'Enter'); fireKey(el, 'keypress', 'Enter'); fireKey(el, 'keyup', 'Enter'); return { ok: true, id: arg.id }; }
        default: return { ok: false, error: 'UNKNOWN_ACTION', action: arg.action };
      }
    } catch (e) {
      return { ok: false, error: String(e && e.message || e) };
    }
  };

  // Bulk filling of many fields in one call (fewer model rounds).
  root.actMany = async function (arg) {
    const results = [];
    for (const step of (arg.steps || [])) {
      results.push(await root.act(step));
      await new Promise(r => setTimeout(r, 40));
    }
    return { ok: results.every(r => r && r.ok), results };
  };

  return true;
})();
