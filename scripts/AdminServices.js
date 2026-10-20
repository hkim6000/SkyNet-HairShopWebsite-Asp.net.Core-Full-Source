var AdminServicesJs = (function () {

    var ttimer = null;
    var askBox = null, askDone = null, askCheck = null, askBack = null;
    var dirty = false;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function base() {
        return location.pathname.replace(/[^\/]*$/, '');
    }

    function getCookie(name) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('hs_tok') });
        $ApiRequest('AdminServices/' + method, JSON.stringify(list));
    }

    function expired() {
        document.cookie = 'hs_tok=; path=' + base() + '; max-age=0';
        location.href = 'AdminLogin';
    }

    function signout() {
        document.cookie = 'hs_tok=; path=' + base() + '; max-age=0';
        location.href = 'AdminLogin';
    }

    function toast(msg) {
        var t = el('sc-toast');
        t.textContent = msg;
        t.classList.add('sc-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('sc-open');
        }, 3400);
    }

    function openMenu() {
        el('sc-rail').classList.add('sc-open');
        el('sc-scrim').classList.add('sc-open');
    }

    function closeMenu() {
        if (!el('sc-rail')) {
            return;
        }
        el('sc-rail').classList.remove('sc-open');
        el('sc-scrim').classList.remove('sc-open');
    }

    function openModal(title) {
        var m = el('sc-modal');
        el('sc-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('sc-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('sc-modal');
        if (m) {
            m.classList.remove('sc-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('sc-modal');
        if (!m || !m.classList.contains('sc-open')) {
            return;
        }
        if (dirty) {
            ask({ title: 'Leave without saving?', text: 'Your changes will be lost.', ok: 'Leave', no: 'Keep editing', danger: true }, shut);
            return;
        }
        shut();
    }

    function markDirty() {
        dirty = true;
    }

    function clean() {
        dirty = false;
    }

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('sc-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('sc-open');
        askDone = null;
        askCheck = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askOk() {
        var x = askParts();
        var m = askCheck ? askCheck(x.i.value.trim()) : '';
        if (m) {
            x.e.textContent = m;
            x.i.focus();
            return;
        }
        askClose(true);
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'sc-ask';
        askBox.innerHTML = '<div class="sc-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="sc-err" role="alert"></em><div class="sc-askacts"><button type="button" class="sc-btn sc-btn-ghost"></button><button type="button" class="sc-btn"></button></div></div>';
        document.getElementById('sc-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('sc-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Enter' && e.target !== x.b[0]) {
                e.preventDefault();
                e.stopPropagation();
                askOk();
            } else if (e.key === 'Tab') {
                var f = [x.i, x.b[0], x.b[1]].filter(function (n) { return n.offsetParent !== null; });
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1)) % f.length].focus();
            }
        }, true);
    }

    function ask(o, done) {
        if (!askBox) {
            askBuild();
        }
        var x = askParts(), input = typeof o.value === 'string';
        askBack = document.activeElement;
        askDone = done;
        askCheck = o.check || null;
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'sc-btn' + (o.danger ? ' sc-askdel' : '');
        askBox.firstChild.classList.toggle('sc-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('sc-open');
        if (input) {
            x.i.focus();
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function collect(root, extra) {
        var list = extra || [];
        document.querySelectorAll('#' + root + ' [data-k]').forEach(function (n) {
            if (n.type === 'checkbox') {
                list.push({ key: n.getAttribute('data-k'), vlu: n.checked ? '1' : '0' });
            } else if (n.type === 'password') {
                list.push({ key: n.getAttribute('data-k'), vlu: n.value });
            } else {
                list.push({ key: n.getAttribute('data-k'), vlu: (n.value || '').trim() });
            }
        });
        return list;
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('sc-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('sc-modal');
        if (m) {
            m.addEventListener('input', markDirty);
            m.addEventListener('change', markDirty);
            m.addEventListener('mousedown', function (e) {
                if (e.target === m) {
                    closeModal();
                }
            });
        }
        ready();
    }

    function ready() {
        api('Load');
    }

    function edit(id) {
        api('Edit', [{ key: 'sid', vlu: id }]);
    }

    function save(id) {
        api('Save', collect('sc-form', [{ key: 'sid', vlu: id }]));
        return false;
    }

    function drop(id, name) {
        ask({ title: 'Delete ' + name + '?', text: 'Only possible if it was never booked. Otherwise hide it.', ok: 'Delete service', no: 'Keep it', danger: true }, function () {
            api('Drop', [{ key: 'sid', vlu: id }]);
        });
    }

    function cats() {
        api('Cats');
    }

    function addCat() {
        var t = el('sc-cattpl');
        el('sc-cats').appendChild(t.content.cloneNode(true));
        var rows = el('sc-cats').querySelectorAll('input');
        rows[rows.length - 1].focus();
        markDirty();
    }

    function catUp(b) {
        var r = b.parentNode;
        if (r.previousElementSibling) {
            r.parentNode.insertBefore(r, r.previousElementSibling);
            markDirty();
        }
    }

    function saveCats() {
        var list = [];
        document.querySelectorAll('#sc-cats .sc-cat').forEach(function (r) {
            list.push({ key: r.getAttribute('data-key'), label: r.querySelector('input').value.trim() });
        });
        api('SaveCats', [{ key: 'cats', vlu: JSON.stringify(list) }]);
        return false;
    }

    return {
        expired: expired,
        signout: signout,
        toast: toast,
        openMenu: openMenu,
        closeMenu: closeMenu,
        openModal: openModal,
        shut: shut,
        closeModal: closeModal,
        clean: clean,
        ask: ask,
        reveal: reveal,
        edit: edit,
        save: save,
        drop: drop,
        cats: cats,
        addCat: addCat,
        catUp: catUp,
        saveCats: saveCats
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminServicesJs.reveal);
} else {
    AdminServicesJs.reveal();
}
