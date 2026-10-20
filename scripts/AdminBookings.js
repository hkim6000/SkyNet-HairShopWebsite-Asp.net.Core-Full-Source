var AdminBookingsJs = (function () {

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
        $ApiRequest('AdminBookings/' + method, JSON.stringify(list));
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
        var t = el('bo-toast');
        t.textContent = msg;
        t.classList.add('bo-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('bo-open');
        }, 3400);
    }

    function openMenu() {
        el('bo-rail').classList.add('bo-open');
        el('bo-scrim').classList.add('bo-open');
    }

    function closeMenu() {
        if (!el('bo-rail')) {
            return;
        }
        el('bo-rail').classList.remove('bo-open');
        el('bo-scrim').classList.remove('bo-open');
    }

    function openModal(title) {
        var m = el('bo-modal');
        el('bo-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('bo-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('bo-modal');
        if (m) {
            m.classList.remove('bo-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('bo-modal');
        if (!m || !m.classList.contains('bo-open')) {
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
        if (!askBox || !askBox.classList.contains('bo-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('bo-open');
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
        askBox.className = 'bo-ask';
        askBox.innerHTML = '<div class="bo-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="bo-err" role="alert"></em><div class="bo-askacts"><button type="button" class="bo-btn bo-btn-ghost"></button><button type="button" class="bo-btn"></button></div></div>';
        document.getElementById('bo-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('bo-open')) {
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
        x.b[1].className = 'bo-btn' + (o.danger ? ' bo-askdel' : '');
        askBox.firstChild.classList.toggle('bo-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('bo-open');
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
                if (askBox && askBox.classList.contains('bo-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('bo-modal');
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

    var f = { view: 'day', day: '', sty: '', pg: 1 };
    var ownOnly = false;
    var first = true;
    var stimer = null;
    var ftimer = null;
    var formBid = '';

    function ready() {
        var q = new URLSearchParams(location.search);
        f.view = q.get('v') === 'list' ? 'list' : 'day';
        f.day = q.get('d') || '';
        api('Load', [{ key: 'view', vlu: f.view }, { key: 'day', vlu: f.day }]);
    }

    function filters() {
        return [
            { key: 'view', vlu: f.view },
            { key: 'day', vlu: val('bo-day') || f.day },
            { key: 'sty', vlu: val('bo-sty') || f.sty },
            { key: 'stat', vlu: val('bo-stat') },
            { key: 'term', vlu: val('bo-term') },
            { key: 'from', vlu: val('bo-from') },
            { key: 'to', vlu: val('bo-to') },
            { key: 'pg', vlu: String(f.pg) }
        ];
    }

    function loaded(view, day, sty, own) {
        f.view = view;
        f.day = day;
        f.sty = sty;
        ownOnly = own;
        viewed(view, day);
        if (!first) {
            return;
        }
        first = false;
        var q = new URLSearchParams(location.search);
        if (q.get('b')) {
            open(q.get('b'));
        } else if (q.get('new') === '1' || q.get('guest')) {
            add(q.get('guest') || '', '', '', '');
        }
    }

    function viewed(view, day) {
        f.view = view;
        f.day = day;
        el('bo-tday').classList.toggle('bo-act', view === 'day');
        el('bo-tlist').classList.toggle('bo-act', view === 'list');
        el('bo-dbar').hidden = view !== 'day';
        el('bo-lbar').hidden = view !== 'list';
        if (el('bo-day')) {
            el('bo-day').value = day;
        }
        history.replaceState(null, '', 'AdminBookings' + (view === 'list' ? '?v=list' : '?d=' + day));
    }

    function refresh() {
        api('View', filters());
    }

    function mode(v) {
        f.view = v;
        f.pg = 1;
        refresh();
    }

    function go() {
        f.pg = 1;
        refresh();
    }

    function page(n) {
        f.pg = n;
        refresh();
    }

    function typed() {
        clearTimeout(stimer);
        stimer = setTimeout(go, 250);
    }

    function shift(n) {
        var d = new Date((val('bo-day') || f.day) + 'T12:00:00');
        d.setDate(d.getDate() + n);
        el('bo-day').value = d.toISOString().slice(0, 10);
        go();
    }

    function today() {
        var d = new Date();
        el('bo-day').value = d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
        go();
    }

    function open(id) {
        var list = filters();
        list.push({ key: 'bid', vlu: id });
        api('Open', list);
    }

    function opened(title) {
        var m = el('bo-modal');
        if (m.classList.contains('bo-open')) {
            el('bo-mtitle').textContent = title;
            clean();
            return;
        }
        openModal(title);
    }

    function act(id, a, name) {
        var send = function (why) {
            var list = filters();
            list.push({ key: 'bid', vlu: id }, { key: 'act', vlu: a }, { key: 'why', vlu: why || '' });
            api('Act', list);
        };
        var o = {
            arrive: { title: 'Check in ' + name + '?', text: 'The visit moves to Arrived.', ok: 'Check in', no: 'Not yet' },
            undo: { title: 'Undo the check-in?', text: name + ' goes back to Booked.', ok: 'Undo check-in', no: 'Keep it' },
            done: { title: 'Mark ' + name + ' as done?', text: 'The visit moves to Checkout, waiting for payment.', ok: 'Mark done', no: 'Not yet' },
            back: { title: 'Back to in the chair?', text: name + ' goes back to Arrived.', ok: 'Move back', no: 'Keep it' },
            noshow: { title: 'Mark ' + name + ' as a no-show?', text: 'The time becomes free for others.', ok: 'Mark no-show', no: 'Keep booking', danger: true, value: '', place: 'Note (optional)' },
            cancel: { title: 'Cancel this booking?', text: 'The time becomes free for others.', ok: 'Cancel booking', no: 'Keep it', danger: true, value: '', place: 'Reason (optional)' },
            restore: { title: 'Restore this booking?', text: 'Only possible while the time is still free.', ok: 'Restore', no: 'Leave it' }
        }[a];
        ask(o, send);
    }

    function add(gid, sty, day, at) {
        formBid = '';
        api('Form', [{ key: 'gid', vlu: gid }, { key: 'fsty', vlu: sty }, { key: 'fday', vlu: day }, { key: 'fat', vlu: at }]);
    }

    function edit(id) {
        formBid = id;
        api('Form', [{ key: 'bid', vlu: id }]);
    }

    function pick(e, col) {
        if (e.target !== col) {
            return;
        }
        var a = parseInt(col.getAttribute('data-a'), 10);
        var m = a + Math.floor(e.offsetY / 1.6 / 15) * 15;
        var t = String(Math.floor(m / 60)).padStart(2, '0') + ':' + String(m % 60).padStart(2, '0');
        add('', col.getAttribute('data-sty'), val('bo-day') || f.day, t);
    }

    function slots() {
        api('Slots', [{ key: 'svc', vlu: val('bo-f-svc') }, { key: 'bsty', vlu: val('bo-f-sty') }, { key: 'bday', vlu: val('bo-f-day') }, { key: 'bat', vlu: val('bo-f-at') }, { key: 'bid', vlu: formBid }]);
    }

    function find(v) {
        clearTimeout(ftimer);
        ftimer = setTimeout(function () {
            api('Find', [{ key: 'term', vlu: (v || '').trim() }]);
        }, 250);
    }

    function choose(gid) {
        api('Choose', [{ key: 'gid', vlu: gid }]);
    }

    function chosen(gid) {
        el('bo-f-gid').value = gid;
        el('bo-gpick').hidden = true;
        el('bo-gnew').hidden = true;
        el('bo-gsel').hidden = false;
        markDirty();
    }

    function unpick() {
        el('bo-f-gid').value = '';
        el('bo-gpick').hidden = false;
        el('bo-gnew').hidden = false;
        el('bo-gsel').hidden = true;
        el('bo-sugg').innerHTML = '';
        el('bo-f-find').value = '';
        el('bo-f-find').focus();
    }

    function save(bid) {
        var list = collect('bo-form', filters());
        list.push({ key: 'bid', vlu: bid });
        api('Save', list);
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
        loaded: loaded,
        viewed: viewed,
        mode: mode,
        go: go,
        page: page,
        typed: typed,
        shift: shift,
        today: today,
        open: open,
        opened: opened,
        act: act,
        add: add,
        edit: edit,
        pick: pick,
        slots: slots,
        find: find,
        choose: choose,
        chosen: chosen,
        unpick: unpick,
        save: save
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminBookingsJs.reveal);
} else {
    AdminBookingsJs.reveal();
}
