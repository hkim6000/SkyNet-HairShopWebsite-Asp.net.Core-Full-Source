var AdminStylistsJs = (function () {

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
        $ApiRequest('AdminStylists/' + method, JSON.stringify(list));
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
        var t = el('sy-toast');
        t.textContent = msg;
        t.classList.add('sy-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('sy-open');
        }, 3400);
    }

    function openMenu() {
        el('sy-rail').classList.add('sy-open');
        el('sy-scrim').classList.add('sy-open');
    }

    function closeMenu() {
        if (!el('sy-rail')) {
            return;
        }
        el('sy-rail').classList.remove('sy-open');
        el('sy-scrim').classList.remove('sy-open');
    }

    function openModal(title) {
        var m = el('sy-modal');
        el('sy-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('sy-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('sy-modal');
        if (m) {
            m.classList.remove('sy-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('sy-modal');
        if (!m || !m.classList.contains('sy-open')) {
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
        if (!askBox || !askBox.classList.contains('sy-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('sy-open');
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
        askBox.className = 'sy-ask';
        askBox.innerHTML = '<div class="sy-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="sy-err" role="alert"></em><div class="sy-askacts"><button type="button" class="sy-btn sy-btn-ghost"></button><button type="button" class="sy-btn"></button></div></div>';
        document.getElementById('sy-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('sy-open')) {
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
        x.b[1].className = 'sy-btn' + (o.danger ? ' sy-askdel' : '');
        askBox.firstChild.classList.toggle('sy-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('sy-open');
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
                if (askBox && askBox.classList.contains('sy-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('sy-modal');
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

    var pkTimer = null;

    function ready() {
        api('Load');
        var pk = el('sy-pk');
        pk.addEventListener('mousedown', function (e) {
            if (e.target === pk) {
                pkClose();
            }
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && pk.classList.contains('sy-open')) {
                e.stopImmediatePropagation();
                pkClose();
            }
        }, true);
    }

    function edit(id) {
        api('Edit', [{ key: 'pid', vlu: id }]);
    }

    function rows(sel, fn) {
        var out = [];
        document.querySelectorAll(sel).forEach(function (r) {
            out.push(fn(r));
        });
        return out;
    }

    function save(id) {
        var list = collect('sy-form', [{ key: 'pid', vlu: id }]);
        list.push({ key: 'svcs', vlu: JSON.stringify(rows('#sy-form .sy-svc:checked', function (n) { return n.value; })) });
        list.push({ key: 'week', vlu: JSON.stringify(rows('#sy-form .sy-wkr', function (r) {
            return { on: r.querySelector('.sy-won').checked, from: r.querySelector('.sy-wf').value, to: r.querySelector('.sy-wt').value, breakFrom: r.querySelector('.sy-wbf').value, breakTo: r.querySelector('.sy-wbt').value };
        })) });
        list.push({ key: 'off', vlu: JSON.stringify(rows('#sy-offs .sy-offr', function (r) {
            return { from: r.querySelector('.sy-ofrom').value, to: r.querySelector('.sy-oto').value, note: r.querySelector('.sy-onote').value.trim() };
        })) });
        api('Save', list);
        return false;
    }

    function drop(id, name) {
        ask({ title: 'Delete ' + name + '?', text: 'Only possible if they have no bookings on record. Otherwise untick Taking bookings.', ok: 'Delete stylist', no: 'Keep', danger: true }, function () {
            api('Drop', [{ key: 'pid', vlu: id }]);
        });
    }

    function offAdd() {
        el('sy-offs').appendChild(el('sy-offtpl').content.cloneNode(true));
        var r = el('sy-offs').lastElementChild;
        r.querySelector('.sy-ofrom').focus();
        markDirty();
    }

    function offDrop(b) {
        b.parentNode.remove();
        markDirty();
    }

    function pkOpen() {
        var pk = el('sy-pk');
        pk.classList.add('sy-open');
        pk.setAttribute('aria-hidden', 'false');
        el('sy-pkmsg').textContent = '';
        api('Photos', [{ key: 'pterm', vlu: val('sy-pkq') }]);
        el('sy-pkq').focus();
    }

    function pkClose() {
        var pk = el('sy-pk');
        pk.classList.remove('sy-open');
        pk.setAttribute('aria-hidden', 'true');
    }

    function pkSearch() {
        clearTimeout(pkTimer);
        pkTimer = setTimeout(function () {
            api('Photos', [{ key: 'pterm', vlu: val('sy-pkq') }]);
        }, 250);
    }

    function pkPick(b) {
        el('sy-f-photo').value = b.getAttribute('data-src');
        el('sy-pv').innerHTML = '<img src="' + b.getAttribute('data-thumb').replace(/"/g, '&quot;') + '" alt="">';
        markDirty();
        pkClose();
    }

    function noPhoto() {
        el('sy-f-photo').value = '';
        el('sy-pv').innerHTML = '<span class="sy-ini">?</span>';
        markDirty();
    }

    function pkUpload() {
        el('sy-pkmsg').textContent = 'Uploading...';
        api('Upload');
    }

    function pkUploaded(msg) {
        el('sy-pkfile').value = '';
        el('sy-pkmsg').textContent = msg;
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
        offAdd: offAdd,
        offDrop: offDrop,
        pkOpen: pkOpen,
        pkClose: pkClose,
        pkSearch: pkSearch,
        pkPick: pkPick,
        noPhoto: noPhoto,
        pkUpload: pkUpload,
        pkUploaded: pkUploaded
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminStylistsJs.reveal);
} else {
    AdminStylistsJs.reveal();
}
