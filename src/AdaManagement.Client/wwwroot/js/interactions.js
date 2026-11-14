// Egér- és görgetés-interakciók, eseménydelegálással (a Blazor által később kirajzolt elemekre is működik).
// - kártyák: megdőlnek az egér felé, és egy fényfolt követi a kurzort (--mx/--my, --rx/--ry)
// - fejléc: görgetéskor átlátszóból tömörre vált (html.is-scrolled)
// - billentyűzet: role="button" elemek Enterre/szóközre kattintanak, role="radiogroup"-ban nyilakkal lehet lépni
(() => {
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const canHover = window.matchMedia('(hover: hover)').matches;
    let hoveredCard = null;
    let frame = 0;
    let lastEvent = null;

    function onPointerMove(e) {
        lastEvent = e;
        if (frame) return;
        frame = requestAnimationFrame(() => {
            frame = 0;
            const ev = lastEvent;

            const card = ev.target.closest?.('.m-card');
            if (hoveredCard && hoveredCard !== card) resetCard(hoveredCard);
            hoveredCard = card;
            if (card) {
                const r = card.getBoundingClientRect();
                const x = (ev.clientX - r.left) / r.width;
                const y = (ev.clientY - r.top) / r.height;
                card.style.setProperty('--mx', `${(x * 100).toFixed(1)}%`);
                card.style.setProperty('--my', `${(y * 100).toFixed(1)}%`);
                if (!reduced) {
                    card.style.setProperty('--ry', `${((x - .5) * 10).toFixed(2)}deg`);
                    card.style.setProperty('--rx', `${((.5 - y) * 8).toFixed(2)}deg`);
                }
            }
        });
    }

    function resetCard(card) {
        card.style.removeProperty('--rx');
        card.style.removeProperty('--ry');
    }

    if (canHover) document.addEventListener('pointermove', onPointerMove, { passive: true });

    // Billentyűzet. A nem-<button> „gombok” (role="button") Enterre és szóközre kattintanak – a szóköz ne görgessen.
    // Csak magára a fókuszált elemre hat, így egy benne lévő valódi gomb Entere nem nyitja meg a szülőt is.
    document.addEventListener('keydown', e => {
        const el = e.target;
        if (!(el instanceof HTMLElement) || e.altKey || e.ctrlKey || e.metaKey) return;

        if (el.getAttribute('role') === 'button' && el.tagName !== 'BUTTON' && (e.key === 'Enter' || e.key === ' ')) {
            e.preventDefault();
            el.click();
            return;
        }

        // Rádiócsoport: nyilakkal a következő/előző elemre lép és ki is választja (mint a natív rádiógomboknál).
        if (el.getAttribute('role') === 'radio') {
            const step = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[e.key];
            const group = el.closest('[role="radiogroup"]');
            if (!step || !group) return;
            const radios = [...group.querySelectorAll('[role="radio"]')].filter(r => !r.disabled);
            const next = radios[(radios.indexOf(el) + step + radios.length) % radios.length];
            if (!next) return;
            e.preventDefault();
            next.focus();
            next.click();
        }
    });

    // Drag & drop: a negyed, aminek a drop zónája fölött a húzott kártya van, .drag-over osztályt kap
    // (a MudBlazor natív HTML5 drag eseményeket használ, ezért itt delegálással figyelhetők).
    const QUAD = '.m-quad';
    let dragQuad = null;
    function setDragQuad(q) {
        if (dragQuad === q) return;
        dragQuad?.classList.remove('drag-over');
        dragQuad = q;
        q?.classList.add('drag-over');
    }
    document.addEventListener('dragstart', () => document.documentElement.classList.add('is-dragging'));
    document.addEventListener('dragover', e => {
        const zone = e.target.closest?.('.mud-drop-zone');
        setDragQuad(zone?.closest(QUAD) ?? null);
    });
    document.addEventListener('dragleave', e => {
        // Ha a mutató az ablakon kívülre vagy a negyeden kívülre ment.
        if (dragQuad && e.relatedTarget && !dragQuad.contains(e.relatedTarget)) setDragQuad(null);
    });
    const endDrag = () => {
        setDragQuad(null);
        document.documentElement.classList.remove('is-dragging');
    };
    document.addEventListener('drop', endDrag);
    document.addEventListener('dragend', endDrag);

    // ---------- Érintős húzás (telefon/tablet) ----------
    // A böngésző natív touch-drag-je megbízhatatlan (halvány szellemkép, nincs dragover), ezért érintőképernyőn
    // a Matrix kikapcsolja a natív húzást, és ez veszi át: hosszú nyomás → a kártya felemelkedik és követi az ujjat,
    // a negyed alatta kivilágosodik, elengedéskor a .NET oldali MoveTo(taskId, quadId) teszi át.
    const coarse = window.matchMedia('(pointer: coarse)').matches;
    let dotnet = null;
    window.adaDnd = {
        register(ref) { dotnet = ref; return coarse; },
        unregister(ref) { if (dotnet === ref) dotnet = null; },
    };

    const LONG_PRESS = 300;  // ms
    const SLOP = 8;          // px: ennél nagyobb mozgás a hosszú nyomás előtt = görgetés
    let touch = null;
    let suppressClickUntil = 0;

    document.addEventListener('touchstart', e => {
        if (!coarse || !dotnet || e.touches.length !== 1) return;
        const card = e.target.closest?.('.m-quad .m-card');
        if (!card || e.target.closest('button:not(.m-open)')) return;
        const p = e.touches[0];
        touch = { card, x: p.clientX, y: p.clientY, lx: p.clientX, ly: p.clientY, active: false };
        touch.timer = setTimeout(activateTouch, LONG_PRESS);
    }, { passive: true });

    function activateTouch() {
        if (!touch) return;
        const r = touch.card.getBoundingClientRect();
        const ghost = document.createElement('div');
        ghost.className = 'touch-ghost';
        ghost.appendChild(touch.card.cloneNode(true));
        Object.assign(ghost.style, { width: `${r.width}px`, height: `${r.height}px`, left: `${r.left}px`, top: `${r.top}px` });
        (document.querySelector('.app-root') ?? document.body).appendChild(ghost);
        Object.assign(touch, { ghost, dx: touch.x - r.left, dy: touch.y - r.top, active: true });
        touch.card.classList.add('is-drag-source');
        document.documentElement.classList.add('is-dragging');
        setDragQuad(touch.card.closest('.m-quad'));
        navigator.vibrate?.(12);
    }

    document.addEventListener('touchmove', e => {
        if (!touch) return;
        const p = e.touches[0];
        if (!touch.active) {
            if (Math.hypot(p.clientX - touch.x, p.clientY - touch.y) > SLOP) cancelTouch();
            return;
        }
        e.preventDefault(); // húzás közben ne görgessen az oldal
        touch.lx = p.clientX; touch.ly = p.clientY;
        touch.ghost.style.left = `${p.clientX - touch.dx}px`;
        touch.ghost.style.top = `${p.clientY - touch.dy}px`;
        setDragQuad(quadAt(p.clientX, p.clientY));
    }, { passive: false });

    document.addEventListener('touchend', e => {
        if (!touch) return;
        const t = touch;
        const target = t.active ? quadAt(t.lx, t.ly) : null;
        cancelTouch();
        if (!t.active) return;
        e.preventDefault();                      // ne nyíljon meg a szerkesztő ablak
        suppressClickUntil = Date.now() + 500;
        const from = t.card.closest('.m-quad');
        if (target && target !== from && dotnet)
            dotnet.invokeMethodAsync('MoveTo', Number(t.card.dataset.taskId), target.dataset.quad);
    }, { passive: false });
    document.addEventListener('touchcancel', () => touch && cancelTouch());
    // hosszú nyomásra ne jöjjön fel a rendszer helyi menüje, és a húzás utáni „kattintás” se nyisson ablakot
    document.addEventListener('contextmenu', e => { if (touch) e.preventDefault(); });
    window.addEventListener('click', e => {
        if (Date.now() < suppressClickUntil) { e.preventDefault(); e.stopPropagation(); }
    }, true);

    function quadAt(x, y) { return document.elementFromPoint(x, y)?.closest('.m-quad') ?? null; }

    function cancelTouch() {
        clearTimeout(touch.timer);
        if (touch.active) {
            touch.ghost.remove();
            touch.card.classList.remove('is-drag-source');
            endDrag();
        }
        touch = null;
    }

    // Mozi mátrix magassága: a rács tetejének helyét CSS-változóba írja (--grid-top), a CSS ebből számolja,
    // mennyi hely marad a képernyő aljáig. Blazor-renderelés, ablakméret és betűtöltés után újramér.
    // Saját <style> elembe kerül, mert a Blazor betöltő felülírja a <html> style attribútumát.
    let gridFrame = 0;
    const gridStyle = document.head.appendChild(document.createElement('style'));
    const syncGridTop = () => {
        gridFrame = 0;
        const g = document.querySelector('.m-grid');
        if (!g) return;
        const css = `:root{--grid-top:${Math.round(g.getBoundingClientRect().top + window.scrollY)}px}`;
        if (gridStyle.textContent !== css) gridStyle.textContent = css;
    };
    const queueGrid = () => { if (!gridFrame) gridFrame = requestAnimationFrame(syncGridTop); };
    new MutationObserver(queueGrid).observe(document.body, { childList: true, subtree: true });
    window.addEventListener('resize', queueGrid);
    document.fonts?.ready.then(queueGrid);

    // Beviteli mód: billentyűzetes navigációnál (Tab/nyilak) html.kbd – csak ekkor emeli ki a fókusz a kártyát.
    document.addEventListener('keydown', e => {
        if (e.key === 'Tab' || e.key.startsWith('Arrow')) document.documentElement.classList.add('kbd');
    }, true);
    document.addEventListener('pointerdown', () => document.documentElement.classList.remove('kbd'), true);

    const onScroll = () => document.documentElement.classList.toggle('is-scrolled', window.scrollY > 12);
    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();
})();
