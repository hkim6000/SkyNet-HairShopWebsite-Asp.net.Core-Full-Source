var AdminTipsJs = (function () {

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
        $ApiRequest('AdminTips/' + method, JSON.stringify(list));
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
        var t = el('tp-toast');
        t.textContent = msg;
        t.classList.add('tp-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('tp-open');
        }, 3400);
    }

    function openMenu() {
        el('tp-rail').classList.add('tp-open');
        el('tp-scrim').classList.add('tp-open');
    }

    function closeMenu() {
        if (!el('tp-rail')) {
            return;
        }
        el('tp-rail').classList.remove('tp-open');
        el('tp-scrim').classList.remove('tp-open');
    }

    function openModal(title) {
        var m = el('tp-modal');
        el('tp-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('tp-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('tp-modal');
        if (m) {
            m.classList.remove('tp-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('tp-modal');
        if (!m || !m.classList.contains('tp-open')) {
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
        if (!askBox || !askBox.classList.contains('tp-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('tp-open');
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
        askBox.className = 'tp-ask';
        askBox.innerHTML = '<div class="tp-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="tp-err" role="alert"></em><div class="tp-askacts"><button type="button" class="tp-btn tp-btn-ghost"></button><button type="button" class="tp-btn"></button></div></div>';
        document.getElementById('tp-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('tp-open')) {
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
        x.b[1].className = 'tp-btn' + (o.danger ? ' tp-askdel' : '');
        askBox.firstChild.classList.toggle('tp-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('tp-open');
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
                if (askBox && askBox.classList.contains('tp-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('tp-modal');
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

    function range() {
        return [{ key: 'from', vlu: val('tp-from') }, { key: 'to', vlu: val('tp-to') }, { key: 'sty', vlu: val('tp-sty') }];
    }

    function go() {
        api('Report', range());
    }

    function iso(d) {
        return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    }

    function preset(p) {
        var t = new Date();
        var a = new Date(t), b = new Date(t);
        if (p === 'week') {
            a.setDate(t.getDate() - ((t.getDay() + 6) % 7));
        } else if (p === 'month') {
            a = new Date(t.getFullYear(), t.getMonth(), 1);
        } else if (p === 'last') {
            a = new Date(t.getFullYear(), t.getMonth() - 1, 1);
            b = new Date(t.getFullYear(), t.getMonth(), 0);
        }
        el('tp-from').value = iso(a);
        el('tp-to').value = iso(b);
        go();
    }

    function csv() {
        api('Csv', range());
    }

    function download(name, text) {
        var a = document.createElement('a');
        a.href = 'data:text/csv;charset=utf-8,' + encodeURIComponent(text);
        a.download = name;
        document.body.appendChild(a);
        a.click();
        a.remove();
        toast('CSV downloaded');
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
        go: go,
        preset: preset,
        csv: csv,
        download: download
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminTipsJs.reveal);
} else {
    AdminTipsJs.reveal();
}
