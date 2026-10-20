var BookJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Book/' + method, JSON.stringify(list || []));
    }

    function toast(msg) {
        var t = el('bk-toast');
        t.textContent = msg;
        t.classList.add('bk-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('bk-open');
        }, 3200);
    }

    function openNav() {
        el('bk-nav').classList.add('bk-open');
        el('bk-scrim').classList.add('bk-open');
    }

    function closeNav() {
        el('bk-nav').classList.remove('bk-open');
        el('bk-scrim').classList.remove('bk-open');
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
        ready();
    }

    var st = { svc: '', sty: '', day: '', at: '' };
    var next = '';

    function ready() {
        var b = el('bk-book');
        if (!b) {
            return;
        }
        st.svc = b.getAttribute('data-svc') || '';
        st.sty = b.getAttribute('data-sty') || '';
        if (st.svc) {
            plan('');
        }
    }

    function plan(go) {
        next = go;
        call('Plan', [{ key: 'svc', vlu: st.svc }, { key: 'sty', vlu: st.sty }, { key: 'day', vlu: st.day }, { key: 'at', vlu: st.at }]);
    }

    function svc(id) {
        st.svc = id;
        st.at = '';
        plan('bk-s2');
    }

    function sty(id) {
        st.sty = id;
        st.at = '';
        plan('bk-s3');
    }

    function day(d) {
        if (!d) {
            return;
        }
        st.day = d;
        st.at = '';
        plan('bk-s4');
    }

    function time(t) {
        st.at = t;
        plan('bk-s5');
    }

    function planned(a, b, c, d) {
        st.svc = a;
        st.sty = b;
        st.day = c;
        st.at = d;
        var q = new URLSearchParams();
        if (a) {
            q.set('s', a);
        }
        if (b && b !== 'any') {
            q.set('p', b);
        }
        var qs = q.toString();
        history.replaceState(null, '', 'Book' + (qs ? '?' + qs : ''));
        if (next && el(next) && window.innerWidth < 981) {
            el(next).scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
        next = '';
    }

    function send() {
        call('Reserve', [
            { key: 'svc', vlu: st.svc },
            { key: 'sty', vlu: st.sty },
            { key: 'day', vlu: st.day },
            { key: 'at', vlu: st.at },
            { key: 'name', vlu: val('bk-name') },
            { key: 'phone', vlu: val('bk-phone') },
            { key: 'email', vlu: val('bk-email') },
            { key: 'note', vlu: val('bk-note') }
        ]);
        return false;
    }

    function invalid() {
        var e = document.querySelector('#bk-s5 .bk-err:not(:empty)');
        if (e) {
            e.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }

    function booked() {
        el('bk-book').hidden = true;
        var h = el('bk-okh');
        el('bk-done').scrollIntoView({ behavior: 'smooth', block: 'start' });
        if (h) {
            h.focus({ preventScroll: true });
        }
        toast('Booking confirmed');
    }

    return {
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        reveal: reveal,
        svc: svc,
        sty: sty,
        day: day,
        time: time,
        planned: planned,
        send: send,
        invalid: invalid,
        booked: booked
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', BookJs.reveal);
} else {
    BookJs.reveal();
}
