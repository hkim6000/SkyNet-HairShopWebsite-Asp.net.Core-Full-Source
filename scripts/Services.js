var ServicesJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Services/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('sv-toast');
        t.textContent = msg;
        t.classList.add('sv-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('sv-open');
        }, 3200);
    }

    function openNav() {
        el('sv-nav').classList.add('sv-open');
        el('sv-scrim').classList.add('sv-open');
    }

    function closeNav() {
        el('sv-nav').classList.remove('sv-open');
        el('sv-scrim').classList.remove('sv-open');
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
        ready();
    }

    function ready() {
    }

    function filter(c) {
        call('Filter', [{ key: 'cat', vlu: c }]);
    }

    function filtered(c) {
        history.replaceState(null, '', 'Services' + (c ? '?c=' + encodeURIComponent(c) : ''));
    }

    return {
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        reveal: reveal,
        filter: filter,
        filtered: filtered
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ServicesJs.reveal);
} else {
    ServicesJs.reveal();
}
