var AdminGuestsJs = (function () {

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
        $ApiRequest('AdminGuests/' + method, JSON.stringify(list));
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
        var t = el('gu-toast');
        t.textContent = msg;
        t.classList.add('gu-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('gu-open');
        }, 3400);
    }

    function openMenu() {
        el('gu-rail').classList.add('gu-open');
        el('gu-scrim').classList.add('gu-open');
    }

    function closeMenu() {
        if (!el('gu-rail')) {
            return;
        }
        el('gu-rail').classList.remove('gu-open');
        el('gu-scrim').classList.remove('gu-open');
    }

    function openModal(title) {
        var m = el('gu-modal');
        el('gu-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('gu-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('gu-modal');
        if (m) {
            m.classList.remove('gu-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('gu-modal');
        if (!m || !m.classList.contains('gu-open')) {
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
        if (!askBox || !askBox.classList.contains('gu-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('gu-open');
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
        askBox.className = 'gu-ask';
        askBox.innerHTML = '<div class="gu-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="gu-err" role="alert"></em><div class="gu-askacts"><button type="button" class="gu-btn gu-btn-ghost"></button><button type="button" class="gu-btn"></button></div></div>';
        document.getElementById('gu-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('gu-open')) {
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
        x.b[1].className = 'gu-btn' + (o.danger ? ' gu-askdel' : '');
        askBox.firstChild.classList.toggle('gu-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('gu-open');
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
                if (askBox && askBox.classList.contains('gu-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('gu-modal');
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

    var pg = 1;
    var stimer = null;
    var cur = '';

    function ready() {
        api('Load');
    }

    function loaded() {
        var g = new URLSearchParams(location.search).get('g');
        if (g) {
            open(g);
        }
    }

    function refresh() {
        api('Rows', [{ key: 'term', vlu: val('gu-term') }, { key: 'sort', vlu: val('gu-sort') }, { key: 'pg', vlu: String(pg) }]);
    }

    function go() {
        pg = 1;
        refresh();
    }

    function page(n) {
        pg = n;
        refresh();
    }

    function typed() {
        clearTimeout(stimer);
        stimer = setTimeout(go, 250);
    }

    function open(id) {
        api('Open', [{ key: 'gid', vlu: id }]);
    }

    function opened(title, id) {
        cur = id;
        var m = el('gu-modal');
        if (m.classList.contains('gu-open')) {
            el('gu-mtitle').textContent = title;
            clean();
        } else {
            openModal(title);
        }
        history.replaceState(null, '', 'AdminGuests' + (id ? '?g=' + encodeURIComponent(id) : ''));
    }

    function gone() {
        cur = '';
        history.replaceState(null, '', 'AdminGuests');
    }

    function save(id) {
        api('Save', collect('gu-form', [{ key: 'gid', vlu: id }]));
        return false;
    }

    function drop(id, name, visits) {
        if (visits > 0) {
            toast(name + ' has visits on record, so the guest cannot be deleted.');
            return;
        }
        ask({ title: 'Delete ' + name + '?', text: 'The guest record is removed for good.', ok: 'Delete guest', no: 'Keep it', danger: true }, function () {
            api('Drop', [{ key: 'gid', vlu: id }]);
        });
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
        loaded: loaded,
        refresh: refresh,
        go: go,
        page: page,
        typed: typed,
        open: open,
        opened: opened,
        gone: gone,
        save: save,
        drop: drop
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminGuestsJs.reveal);
} else {
    AdminGuestsJs.reveal();
}
