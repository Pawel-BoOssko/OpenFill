/*
 * OpenFill - extractor of the logical structure of the page.
 * Metadata:
 *   wersja: 0.3
 *   data:   2026-10-05 15:15
 *   changes 0.2: suggestion lists (role=option), non-semantic buttons (onclick/tabindex),
 *               dialogs and the cookie banner, page messages (alert/status), field validation errors,
 *               headings for context, visibility checks only for candidates (faster on large pages).
 * Returns a clean picture of the page for the model: forms, fields (label, type, value, options),
 * buttons, links, selections, and relations (field -> form, button -> form).
 * Every interactive element gets a stable identifier "of<N>" stored in the data-of-id attribute,
 * so the model can point at the element after an action, even if the page redraws.
 *
 * Design: no dependencies, robust to missing ARIA, descends into shadow DOM (open) and same-origin frames.
 */
(function () {
  const COUNTER_ATTR = 'data-of-id';
  let root = window.__openfill || (window.__openfill = {});
  if (!root.seq) root.seq = 1;
  if (!root.byId) root.byId = new Map();

  function cssPath(el) {
    // A short, reasonably stable path - a helper, not the main way of addressing.
    if (!el || el.nodeType !== 1) return '';
    if (el.id) return '#' + CSS.escape(el.id);
    const parts = [];
    let node = el;
    while (node && node.nodeType === 1 && parts.length < 5) {
      let sel = node.nodeName.toLowerCase();
      if (node.classList && node.classList.length)
        sel += '.' + Array.from(node.classList).slice(0, 2).map(c => CSS.escape(c)).join('.');
      const parent = node.parentNode;
      if (parent && parent.children) {
        const sameTag = Array.from(parent.children).filter(c => c.nodeName === node.nodeName);
        if (sameTag.length > 1) sel += ':nth-of-type(' + (sameTag.indexOf(node) + 1) + ')';
      }
      parts.unshift(sel);
      node = node.parentNode && node.parentNode.host ? node.parentNode.host : (node.parentNode);
      if (node === document.documentElement) break;
    }
    return parts.join(' > ');
  }

  function visible(el) {
    if (!el || el.nodeType !== 1) return false;
    const st = (el.ownerDocument.defaultView || window).getComputedStyle(el);
    if (!st || st.display === 'none' || st.visibility === 'hidden' || st.visibility === 'collapse') return false;
    if (parseFloat(st.opacity || '1') === 0) return false;
    const r = el.getBoundingClientRect();
    if (r.width < 1 && r.height < 1) return false;
    return true;
  }

  function assignId(el) {
    let id = el.getAttribute(COUNTER_ATTR);
    if (id && root.byId.get(id) === el) return id;
    id = 'of' + (root.seq++);
    el.setAttribute(COUNTER_ATTR, id);
    root.byId.set(id, el);
    return id;
  }

  function labelFor(el) {
    // Order: aria-label, aria-labelledby, <label for>, parent label, placeholder, name, adjacent text.
    const aria = el.getAttribute && el.getAttribute('aria-label');
    if (aria && aria.trim()) return aria.trim();
    const labelledby = el.getAttribute && el.getAttribute('aria-labelledby');
    if (labelledby) {
      const txt = labelledby.split(/\s+/).map(id => {
        const n = el.ownerDocument.getElementById(id);
        return n ? n.textContent.trim() : '';
      }).join(' ').trim();
      if (txt) return txt;
    }
    if (el.id) {
      const lab = el.ownerDocument.querySelector('label[for="' + CSS.escape(el.id) + '"]');
      if (lab && lab.textContent.trim()) return lab.textContent.trim();
    }
    let p = el.closest && el.closest('label');
    if (p && p.textContent.trim()) return p.textContent.trim();
    const ph = el.getAttribute && el.getAttribute('placeholder');
    if (ph && ph.trim()) return ph.trim();
    const title = el.getAttribute && el.getAttribute('title');
    if (title && title.trim()) return title.trim();
    const name = el.getAttribute && el.getAttribute('name');
    if (name && name.trim()) return name.trim();
    // Adjacent text to the left of / above the field.
    const prev = el.previousElementSibling;
    if (prev && prev.textContent && prev.textContent.trim() && prev.textContent.trim().length < 60)
      return prev.textContent.trim();
    return '';
  }

  function describedBy(el) {
    const ids = (el.getAttribute('aria-describedby') || el.getAttribute('aria-errormessage') || '').split(/\s+/).filter(Boolean);
    return ids.map(id => { const n = el.ownerDocument.getElementById(id); return n ? n.textContent.trim() : ''; }).join(' ').trim();
  }

  function clip(s, n) { s = (s || '').replace(/\s+/g, ' ').trim(); return s.length > n ? s.slice(0, n) + '…' : s; }

  function describeField(el) {
    const tag = el.nodeName.toLowerCase();
    const type = (el.getAttribute('type') || (tag === 'textarea' ? 'textarea' : tag === 'select' ? 'select' : 'text')).toLowerCase();
    const field = {
      id: assignId(el),
      role: 'field',
      tag, type,
      label: clip(labelFor(el), 120),
      name: el.getAttribute('name') || undefined,
      required: el.required || el.getAttribute('aria-required') === 'true' || undefined,
      disabled: el.disabled || undefined,
      readonly: el.readOnly || undefined,
      placeholder: el.getAttribute('placeholder') || undefined,
      css: cssPath(el)
    };
    if (tag === 'select') {
      const opts = Array.from(el.options || []).slice(0, 40).map(o => ({ value: o.value, label: clip(o.textContent, 60), selected: o.selected || undefined }));
      field.options = opts;
      field.value = el.multiple ? Array.from(el.selectedOptions).map(o => o.value) : el.value;
    } else if (type === 'checkbox' || type === 'radio') {
      field.checked = el.checked;
      field.value = el.value;
    } else {
      const val = el.value || '';
      field.value = clip(val, 200);
      if (el.getAttribute('role') === 'combobox' || el.getAttribute('aria-autocomplete') || el.getAttribute('list'))
        field.autocomplete = true;
      if (el.isContentEditable && !el.value) field.value = clip(el.textContent, 200);
    }
    const invalid = el.getAttribute('aria-invalid') === 'true' || (el.validity && el.validity.valid === false && el.dataset.ofTouched === '1');
    if (invalid) {
      field.invalid = true;
      const err = describedBy(el) || (el.validationMessage || '');
      if (err) field.error = clip(err, 160);
    }
    return field;
  }

  function buttonText(el) {
    const v = el.value && el.type && /button|submit|reset/i.test(el.type) ? el.value : '';
    let lb = '';
    const ids = el.getAttribute('aria-labelledby');
    if (ids) lb = ids.split(/\s+/).map(id => { const n = el.ownerDocument.getElementById(id); return n ? n.textContent.trim() : ''; }).join(' ').trim();
    const img = !el.textContent.trim() && el.querySelector && el.querySelector('img[alt]');
    return clip(el.getAttribute('aria-label') || lb || el.textContent || v || el.title || (img && img.alt) || '', 80);
  }

  function describeButton(el) {
    const type = (el.getAttribute('type') || '').toLowerCase();
    const special = buttonKind(el);
    const tag = el.nodeName.toLowerCase();
    const isSubmit = type === 'submit' || (tag === 'button' && !type && el.closest && el.closest('form'));
    const b = {
      id: assignId(el),
      role: 'button',
      text: buttonText(el),
      kind: special || (isSubmit ? 'submit' : (type === 'reset' ? 'reset' : 'button')),
      disabled: el.disabled || el.getAttribute('aria-disabled') === 'true' || undefined,
      css: cssPath(el)
    };
    const st = el.getAttribute('aria-checked') || el.getAttribute('aria-selected') || el.getAttribute('aria-pressed') || el.getAttribute('aria-expanded');
    if (st === 'true' || st === 'false') b.state = st === 'true';
    return b;
  }

  function isButtonLike(el) {
    const tag = el.nodeName.toLowerCase();
    if (tag === 'button') return true;
    if (tag === 'input' && /^(submit|button|reset|image)$/i.test(el.getAttribute('type') || '')) return true;
    const role = el.getAttribute('role');
    if (role === 'button' || role === 'menuitem' || role === 'tab' || role === 'option' || role === 'menuitemradio' ||
        role === 'menuitemcheckbox' || role === 'switch' || role === 'checkbox' || role === 'radio' || role === 'treeitem') return true;
    if (tag === 'a' && role === 'button') return true;
    if (tag === 'summary') return true;
    // Non-semantic "buttons": div/span with a click handler and short text.
    if ((tag === 'div' || tag === 'span' || tag === 'li' || tag === 'label') && !el.querySelector('a,button,input,select,textarea,[role=button],[role=option]')) {
      if (el.hasAttribute('onclick')) return true;
      const ti = el.getAttribute('tabindex');
      if (ti !== null && ti !== '-1' && (el.textContent || '').trim().length > 0 && (el.textContent || '').trim().length < 80) return true;
    }
    return false;
  }

  function buttonKind(el) {
    const role = el.getAttribute('role');
    if (role === 'option' || role === 'menuitemradio' || role === 'treeitem') return 'option';
    if (role === 'checkbox' || role === 'switch' || role === 'menuitemcheckbox') return 'toggle';
    if (role === 'radio') return 'radio';
    if (role === 'tab') return 'tab';
    return null;
  }

  function isFieldLike(el) {
    const tag = el.nodeName.toLowerCase();
    if (tag === 'textarea' || tag === 'select') return true;
    if (tag === 'input') {
      const t = (el.getAttribute('type') || 'text').toLowerCase();
      return !/^(submit|button|reset|image|hidden)$/.test(t);
    }
    if (el.isContentEditable) return true;
    const role = el.getAttribute('role');
    return role === 'textbox' || role === 'combobox' || role === 'searchbox' || role === 'spinbutton';
  }

  function* walk(docOrRoot) {
    const win = (docOrRoot.defaultView) || window;
    const treeRoot = docOrRoot.body || docOrRoot;
    const stack = [treeRoot];
    while (stack.length) {
      const node = stack.pop();
      if (!node) continue;
      if (node.nodeType === 1) {
        yield node;
        if (node.shadowRoot) stack.push(node.shadowRoot);
      }
      const kids = node.children;
      if (kids) for (let i = kids.length - 1; i >= 0; i--) stack.push(kids[i]);
      else if (node.childNodes) for (let i = node.childNodes.length - 1; i >= 0; i--) {
        const c = node.childNodes[i];
        if (c.nodeType === 1) stack.push(c);
      }
    }
  }

  function formKey(el) {
    const f = el.closest && el.closest('form');
    if (f) return assignId(f);
    const grp = el.closest && el.closest('[role="form"],fieldset,[data-form],section,dialog,[role="dialog"]');
    return grp ? assignId(grp) : null;
  }

  // Elements carrying context (not for clicking): headings, dialogs, page messages.
  function isNotable(el, tag) {
    if (tag === 'h1' || tag === 'h2' || tag === 'h3') return true;
    if (tag === 'dialog' && el.open) return true;
    const role = el.getAttribute('role');
    if (role === 'dialog' || role === 'alertdialog' || el.getAttribute('aria-modal') === 'true') return true;
    if (role === 'alert' || role === 'status') return true;
    const live = el.getAttribute('aria-live');
    if (live === 'assertive' || live === 'polite') return true;
    if (el.classList && el.classList.length) {
      const cls = el.className && typeof el.className === 'string' ? el.className : '';
      if (/(^|[\s_-])(error|alert|success|toast|notification|invalid-feedback|form-error|message)([\s_-]|$)/i.test(cls) && (el.textContent || '').trim().length > 0 && (el.textContent || '').trim().length < 300 && el.children.length <= 3) return true;
    }
    return false;
  }

  let ctx = null;
  function collectNotable(el, tag) {
    const text = clip(el.textContent, 220);
    if (!text) return;
    if (tag === 'h1' || tag === 'h2' || tag === 'h3') { if (ctx.headings.length < 8) ctx.headings.push(clip(text, 100)); return; }
    const role = el.getAttribute('role');
    if (tag === 'dialog' || role === 'dialog' || role === 'alertdialog' || el.getAttribute('aria-modal') === 'true') {
      if (ctx.dialogs.length < 4) {
        const name = el.getAttribute('aria-label') || (el.querySelector('h1,h2,h3,[role=heading]') || {}).textContent || '';
        ctx.dialogs.push({ id: assignId(el), name: clip(name || text, 100), modal: el.getAttribute('aria-modal') === 'true' || tag === 'dialog' || undefined });
      }
      return;
    }
    if (ctx.messages.length < 10 && !ctx.messages.some(m => m.text === text))
      ctx.messages.push({ text, kind: role === 'alert' || /error|invalid/i.test(el.className || '') ? 'alert' : 'status' });
  }

  function extract(opts) {
    opts = opts || {};
    const maxFields = opts.maxFields || 150;
    const forms = new Map();
    const looseFields = [];
    const buttons = [];
    const links = [];
    let fieldCount = 0;
    ctx = { headings: [], dialogs: [], messages: [] };

    const docs = [document];
    // Same-origin frames.
    for (const fr of Array.from(document.querySelectorAll('iframe'))) {
      try { if (fr.contentDocument) docs.push(fr.contentDocument); } catch (e) { /* cross-origin */ }
    }

    for (const doc of docs) {
      for (const el of walk(doc)) {
        if (fieldCount >= maxFields) break;
        const tagName = el.nodeName.toLowerCase();
        const fieldLike = isFieldLike(el);
        const buttonLike = !fieldLike && isButtonLike(el);
        const linkLike = !fieldLike && !buttonLike && tagName === 'a' && el.getAttribute('href');
        const notable = !fieldLike && !buttonLike && !linkLike && isNotable(el, tagName);
        if (!fieldLike && !buttonLike && !linkLike && !notable) continue;
        if (!opts.includeHidden && !visible(el)) continue;

        if (notable) { collectNotable(el, tagName); continue; }

        if (fieldLike) {
          const f = describeField(el);
          const key = formKey(el);
          if (key) {
            if (!forms.has(key)) {
              const formEl = root.byId.get(key);
              forms.set(key, {
                id: key, role: 'form',
                name: (formEl && (formEl.getAttribute('name') || formEl.getAttribute('aria-label'))) || undefined,
                fields: [], buttons: []
              });
            }
            forms.get(key).fields.push(f);
          } else {
            looseFields.push(f);
          }
          fieldCount++;
        } else if (buttonLike) {
          const b = describeButton(el);
          const key = formKey(el);
          if (key && forms.has(key)) forms.get(key).buttons.push(b);
          else if (key) { /* form not created yet */ buttons.push(Object.assign({ form: key }, b)); }
          else buttons.push(b);
        } else if (linkLike && links.length < 60) {
          const txt = clip(el.textContent, 60);
          if (txt) links.push({ id: assignId(el), role: 'link', text: txt, href: el.href, css: cssPath(el) });
        }
      }
    }

    // Buttons assigned to forms created later.
    for (let i = buttons.length - 1; i >= 0; i--) {
      const key = buttons[i].form;
      if (key && forms.has(key)) { delete buttons[i].form; forms.get(key).buttons.push(buttons[i]); buttons.splice(i, 1); }
    }

    return {
      ok: true,
      url: location.href,
      title: document.title,
      ts: Date.now(),
      forms: Array.from(forms.values()),
      looseFields,
      buttons,
      links,
      headings: ctx.headings,
      dialogs: ctx.dialogs,
      messages: ctx.messages,
      counts: { fields: fieldCount, forms: forms.size, buttons: buttons.length, links: links.length },
      truncated: fieldCount >= maxFields
    };
  }

  root.extract = extract;
  root.resolve = function (id) { return root.byId.get(id) || document.querySelector('[' + COUNTER_ATTR + '="' + id + '"]'); };
  return true;
})();
