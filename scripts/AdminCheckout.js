var AdminCheckoutJs = (function () {

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
        $ApiRequest('AdminCheckout/' + method, JSON.stringify(list));
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
        var t = el('co-toast');
        t.textContent = msg;
        t.classList.add('co-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('co-open');
        }, 3400);
    }

    function openMenu() {
        el('co-rail').classList.add('co-open');
        el('co-scrim').classList.add('co-open');
    }

    function closeMenu() {
        if (!el('co-rail')) {
            return;
        }
        el('co-rail').classList.remove('co-open');
        el('co-scrim').classList.remove('co-open');
    }

    function openModal(title) {
        var m = el('co-modal');
        el('co-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('co-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('co-modal');
        if (m) {
            m.classList.remove('co-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('co-modal');
        if (!m || !m.classList.contains('co-open')) {
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
        if (!askBox || !askBox.classList.contains('co-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('co-open');
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
        askBox.className = 'co-ask';
        askBox.innerHTML = '<div class="co-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="co-err" role="alert"></em><div class="co-askacts"><button type="button" class="co-btn co-btn-ghost"></button><button type="button" class="co-btn"></button></div></div>';
        document.getElementById('co-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('co-open')) {
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
        x.b[1].className = 'co-btn' + (o.danger ? ' co-askdel' : '');
        askBox.firstChild.classList.toggle('co-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('co-open');
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
                if (askBox && askBox.classList.contains('co-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('co-modal');
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

    var tipMode = 'none';
    var payBy = '';
    var qtimer = null;

    function ready() {
        var q = new URLSearchParams(location.search);
        api('Load', [{ key: 'bid', vlu: q.get('b') || '' }]);
    }

    function loaded(id) {
        tipMode = 'none';
        payBy = '';
    }

    function pick(id) {
        api('Pick', [{ key: 'bid', vlu: id }]);
    }

    function picked(id) {
        tipMode = 'none';
        payBy = '';
        history.replaceState(null, '', 'AdminCheckout' + (id ? '?b=' + encodeURIComponent(id) : ''));
        if (window.innerWidth < 981 && el('co-till')) {
            el('co-till').scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }

    function lines() {
        var out = [];
        document.querySelectorAll('#co-lines .co-line').forEach(function (n) {
            var i = n.querySelector('.co-lp');
            out.push({ sid: n.getAttribute('data-sid'), price: i ? i.value.trim() : '' });
        });
        return out;
    }

    function payload() {
        var f = el('co-payform');
        return [
            { key: 'bid', vlu: f ? f.getAttribute('data-bid') : '' },
            { key: 'lines', vlu: JSON.stringify(lines()) },
            { key: 'disc', vlu: val('co-disc') },
            { key: 'tipmode', vlu: tipMode },
            { key: 'tipamt', vlu: val('co-tipamt') },
            { key: 'method', vlu: payBy }
        ];
    }

    function quote() {
        clearTimeout(qtimer);
        qtimer = setTimeout(function () {
            api('Sum', payload());
        }, 200);
    }

    function tip(m) {
        tipMode = m;
        document.querySelectorAll('#co-payform .co-tipb[data-t]').forEach(function (b) {
            var on = b.getAttribute('data-t') === m;
            b.classList.toggle('co-on', on);
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
        el('co-custombox').hidden = m !== 'custom';
        if (m === 'custom') {
            el('co-tipamt').focus();
        }
        quote();
    }

    function method(m) {
        payBy = m;
        document.querySelectorAll('#co-payform .co-meth').forEach(function (b) {
            var on = b.getAttribute('data-m') === m;
            b.classList.toggle('co-on', on);
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
        el('co-e-pay').textContent = '';
    }

    function addLine() {
        var s = val('co-addsvc');
        if (!s) {
            return;
        }
        api('Line', [{ key: 'sid', vlu: s }]);
    }

    function lineAdded() {
        var h = el('co-newline');
        while (h.firstChild) {
            el('co-lines').appendChild(h.firstChild);
        }
        el('co-addsvc').value = '';
        quote();
    }

    function dropLine(b) {
        if (document.querySelectorAll('#co-lines .co-line').length < 2) {
            toast('A bill needs at least one service.');
            return;
        }
        b.closest('.co-line').remove();
        quote();
    }

    function pay() {
        var list = payload();
        var total = el('co-totals').querySelector('dd.co-grand');
        ask({ title: 'Take ' + (total ? total.textContent : 'payment') + (payBy ? ' by ' + payBy : '') + '?', text: 'Check that the card payment went through or the cash is in the till.', ok: 'Payment taken', no: 'Go back' }, function () {
            api('Pay', list);
        });
    }

    function finish(id, name) {
        ask({ title: 'Is ' + name + '’s visit done?', text: 'The visit is marked done and ready to pay.', ok: 'Visit done', no: 'Not yet' }, function () {
            api('Finish', [{ key: 'bid', vlu: id }]);
        });
    }

    function reopen(id, name) {
        ask({ title: 'Reopen this payment?', text: 'The payment for ' + name + ' is removed so it can be taken again. This is logged.', value: '', place: 'Reason (optional)', ok: 'Reopen payment', no: 'Keep it', danger: true }, function (why) {
            api('Reopen', [{ key: 'bid', vlu: id }, { key: 'why', vlu: why }]);
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
        pick: pick,
        picked: picked,
        quote: quote,
        tip: tip,
        method: method,
        addLine: addLine,
        lineAdded: lineAdded,
        dropLine: dropLine,
        pay: pay,
        finish: finish,
        reopen: reopen
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminCheckoutJs.reveal);
} else {
    AdminCheckoutJs.reveal();
}
