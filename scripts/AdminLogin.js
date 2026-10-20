var AdminLoginJs = (function () {

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
        $ApiRequest('AdminLogin/' + method, JSON.stringify(list));
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
        var t = el('lg-toast');
        t.textContent = msg;
        t.classList.add('lg-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('lg-open');
        }, 3400);
    }

    function openMenu() {
        el('lg-rail').classList.add('lg-open');
        el('lg-scrim').classList.add('lg-open');
    }

    function closeMenu() {
        if (!el('lg-rail')) {
            return;
        }
        el('lg-rail').classList.remove('lg-open');
        el('lg-scrim').classList.remove('lg-open');
    }

    function openModal(title) {
        var m = el('lg-modal');
        el('lg-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('lg-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('lg-modal');
        if (m) {
            m.classList.remove('lg-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('lg-modal');
        if (!m || !m.classList.contains('lg-open')) {
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
        if (!askBox || !askBox.classList.contains('lg-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('lg-open');
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
        askBox.className = 'lg-ask';
        askBox.innerHTML = '<div class="lg-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="lg-err" role="alert"></em><div class="lg-askacts"><button type="button" class="lg-btn lg-btn-ghost"></button><button type="button" class="lg-btn"></button></div></div>';
        document.getElementById('lg-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('lg-open')) {
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
        x.b[1].className = 'lg-btn' + (o.danger ? ' lg-askdel' : '');
        askBox.firstChild.classList.toggle('lg-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('lg-open');
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
                if (askBox && askBox.classList.contains('lg-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('lg-modal');
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

    var choosing = false;

    function ready() {
        if (getCookie('hs_tok')) {
            api('Resume');
        }
        el('lg-user').focus();
    }

    function signin() {
        var list = [{ key: 'user', vlu: val('lg-user') }, { key: 'pass', vlu: el('lg-pass').value }];
        if (choosing) {
            list.push({ key: 'new1', vlu: el('lg-new1').value });
            list.push({ key: 'new2', vlu: el('lg-new2').value });
            api('SetPassword', list);
        } else {
            api('SignIn', list);
        }
        return false;
    }

    function choose() {
        choosing = true;
        el('lg-newbox').hidden = false;
        el('lg-go').textContent = 'Save and sign in';
        el('lg-new1').focus();
    }

    function enter(tok) {
        document.cookie = 'hs_tok=' + encodeURIComponent(tok) + '; path=' + base() + '; samesite=lax; max-age=28800';
        location.href = 'Admin';
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
        signin: signin,
        choose: choose,
        enter: enter
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminLoginJs.reveal);
} else {
    AdminLoginJs.reveal();
}
