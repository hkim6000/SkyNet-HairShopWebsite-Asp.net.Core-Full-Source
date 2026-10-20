var StylistsJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Stylists/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('st-toast');
        t.textContent = msg;
        t.classList.add('st-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('st-open');
        }, 3200);
    }

    function openNav() {
        el('st-nav').classList.add('st-open');
        el('st-scrim').classList.add('st-open');
    }

    function closeNav() {
        el('st-nav').classList.remove('st-open');
        el('st-scrim').classList.remove('st-open');
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
        ready();
    }

    var back = null;

    function ready() {
        var m = el('st-modal');
        m.addEventListener('mousedown', function (e) {
            if (e.target === m) {
                hide();
            }
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                hide();
            }
        });
        if (m.classList.contains('st-open')) {
            m.setAttribute('aria-hidden', 'false');
        }
    }

    function show(id) {
        back = document.activeElement;
        call('Show', [{ key: 'who', vlu: id }]);
    }

    function shown(id) {
        var m = el('st-modal');
        m.classList.add('st-open');
        m.setAttribute('aria-hidden', 'false');
        history.replaceState(null, '', 'Stylists?w=' + encodeURIComponent(id));
        var c = m.querySelector('.st-mclose');
        if (c) {
            c.focus();
        }
    }

    function hide() {
        var m = el('st-modal');
        if (!m.classList.contains('st-open')) {
            return;
        }
        m.classList.remove('st-open');
        m.setAttribute('aria-hidden', 'true');
        history.replaceState(null, '', 'Stylists');
        if (back && back.focus) {
            back.focus();
        }
    }

    return {
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        reveal: reveal,
        show: show,
        shown: shown,
        hide: hide
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', StylistsJs.reveal);
} else {
    StylistsJs.reveal();
}
