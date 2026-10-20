var GalleryJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Gallery/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('gl-toast');
        t.textContent = msg;
        t.classList.add('gl-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('gl-open');
        }, 3200);
    }

    function openNav() {
        el('gl-nav').classList.add('gl-open');
        el('gl-scrim').classList.add('gl-open');
    }

    function closeNav() {
        el('gl-nav').classList.remove('gl-open');
        el('gl-scrim').classList.remove('gl-open');
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
        ready();
    }

    var at = 0;
    var back = null;
    var sx = null;

    function tiles() {
        return Array.prototype.slice.call(document.querySelectorAll('#gl-wall .gl-tile'));
    }

    function ready() {
        var lb = el('gl-lb');
        lb.addEventListener('mousedown', function (e) {
            if (e.target === lb) {
                close();
            }
        });
        lb.addEventListener('touchstart', function (e) {
            sx = e.touches[0].clientX;
        }, { passive: true });
        lb.addEventListener('touchend', function (e) {
            if (sx === null) {
                return;
            }
            var dx = e.changedTouches[0].clientX - sx;
            sx = null;
            if (Math.abs(dx) > 40) {
                step(dx < 0 ? 1 : -1);
            }
        });
        document.addEventListener('keydown', function (e) {
            if (!lb.classList.contains('gl-open')) {
                return;
            }
            if (e.key === 'Escape') {
                close();
            } else if (e.key === 'ArrowRight') {
                step(1);
            } else if (e.key === 'ArrowLeft') {
                step(-1);
            }
        });
    }

    function filter(t) {
        call('Filter', [{ key: 'tag', vlu: t }]);
    }

    function filtered(t) {
        history.replaceState(null, '', 'Gallery' + (t ? '?t=' + encodeURIComponent(t) : ''));
    }

    function paint() {
        var list = tiles();
        if (!list.length) {
            return;
        }
        var t = list[at];
        el('gl-lbimg').src = t.getAttribute('data-full');
        el('gl-lbimg').alt = t.getAttribute('data-alt') || '';
        el('gl-lbcap').textContent = (t.getAttribute('data-alt') || '') + '  ·  ' + (at + 1) + ' of ' + list.length;
    }

    function view(i) {
        back = document.activeElement;
        at = i;
        paint();
        var lb = el('gl-lb');
        lb.classList.add('gl-open');
        lb.setAttribute('aria-hidden', 'false');
        lb.querySelector('.gl-lbx').focus();
    }

    function step(d) {
        var n = tiles().length;
        if (!n) {
            return;
        }
        at = (at + d + n) % n;
        paint();
    }

    function close() {
        var lb = el('gl-lb');
        lb.classList.remove('gl-open');
        lb.setAttribute('aria-hidden', 'true');
        if (back && back.focus) {
            back.focus();
        }
    }

    return {
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        reveal: reveal,
        filter: filter,
        filtered: filtered,
        view: view,
        step: step,
        close: close
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', GalleryJs.reveal);
} else {
    GalleryJs.reveal();
}
