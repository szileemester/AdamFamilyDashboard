// Mozgó háttér a dinamikus témákhoz (Piros, Kék) egy teljes képernyős canvasra:
// lassan úszó fényfoltok, felszálló parázs és a kurzort lágyan követő „reflektor”.
// Takarékos: ~30 fps, legfeljebb 1.5x pixelsűrűség, a háttérbe tett lapon megáll,
// a rendszer "kevesebb mozgás" beállításánál csak egy állóképet rajzol.

let state = null;

const palettes = {
    piros: { blobs: ['#C1121F', '#6A2BB8', '#8C1C3A'], embers: '#F0464F', spot: '255,236,226' },
    kek:   { blobs: ['#1257C9', '#0E8FB3', '#3B2BB8'], embers: '#4FA3FF', spot: '226,238,255' },
};

const rand = (a, b) => a + Math.random() * (b - a);

function makeParticles(kind, w, h) {
    const n = { embers: 40, blobs: 3 }[kind];
    return Array.from({ length: n }, () => ({
        x: rand(0, w), y: rand(0, h),
        r: kind === 'blobs' ? rand(Math.min(w, h) * .35, Math.min(w, h) * .6) : rand(.5, 1.6),
        vx: rand(-.12, .12),
        vy: kind === 'embers' ? rand(-.6, -.2) : rand(-.08, .08),
        a: rand(.2, .9), t: rand(0, Math.PI * 2), c: Math.floor(rand(0, 3)),
    }));
}

// A reflektor lágyan, késleltetve követi az egeret.
function onPointer(e) {
    if (!state) return;
    state.tx = e.clientX; state.ty = e.clientY;
}

function draw(ts) {
    const s = state;
    if (!s) return;
    if (!s.reduced) s.raf = requestAnimationFrame(draw);
    if (document.hidden || ts - s.last < 33) return; // ~30 fps
    s.last = ts;

    const { ctx, w, h, pal } = s;
    ctx.clearRect(0, 0, w, h);

    // fényfoltok a fix CSS-gradiens fölött
    for (const p of s.particles.blobs) {
        p.t += .004; p.x += Math.cos(p.t) * .35; p.y += Math.sin(p.t * 1.3) * .35;
        const g = ctx.createRadialGradient(p.x, p.y, 0, p.x, p.y, p.r);
        g.addColorStop(0, pal.blobs[p.c]); g.addColorStop(1, 'transparent');
        ctx.globalAlpha = .22;
        ctx.fillStyle = g; ctx.fillRect(0, 0, w, h);
    }

    // reflektor
    s.sx += (s.tx - s.sx) * .08; s.sy += (s.ty - s.sy) * .08;
    const r = Math.max(w, h) * .45;
    const g = ctx.createRadialGradient(s.sx, s.sy, 0, s.sx, s.sy, r);
    g.addColorStop(0, `rgba(${pal.spot},.13)`); g.addColorStop(.4, `rgba(${pal.spot},.04)`); g.addColorStop(1, 'transparent');
    ctx.globalAlpha = 1; ctx.fillStyle = g; ctx.fillRect(0, 0, w, h);

    // parázs
    ctx.fillStyle = pal.embers;
    for (const p of s.particles.embers) {
        p.t += .03; p.x += p.vx + Math.sin(p.t) * .3; p.y += p.vy;
        if (p.y < -10) { p.y = h + 10; p.x = rand(0, w); }
        ctx.globalAlpha = p.a * .7;
        ctx.beginPath(); ctx.arc(p.x, p.y, p.r + .6, 0, Math.PI * 2); ctx.fill();
    }
    ctx.globalAlpha = 1;
}

function resize() {
    const s = state;
    if (!s) return;
    const dpr = Math.min(window.devicePixelRatio || 1, 1.5);
    s.w = window.innerWidth; s.h = window.innerHeight;
    s.canvas.width = s.w * dpr; s.canvas.height = s.h * dpr;
    s.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    s.particles = { blobs: makeParticles('blobs', s.w, s.h), embers: makeParticles('embers', s.w, s.h) };
    if (s.reduced) { s.last = -1e9; draw(performance.now()); }
}

/** @param {HTMLCanvasElement} canvas @param {string} theme „piros” vagy „kek” */
export function start(canvas, theme) {
    stop();
    if (!canvas) return;
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    state = {
        canvas, ctx: canvas.getContext('2d'), reduced, last: -1e9,
        pal: palettes[theme] ?? palettes.piros,
        // reflektor: kezdetben jobb felül, onnan követi a kurzort
        tx: window.innerWidth * .7, ty: window.innerHeight * .25, sx: window.innerWidth * .7, sy: window.innerHeight * .25,
    };
    window.addEventListener('resize', resize);
    window.addEventListener('pointermove', onPointer, { passive: true });
    resize();
    if (!reduced) state.raf = requestAnimationFrame(draw);
}

export function stop() {
    if (!state) return;
    cancelAnimationFrame(state.raf);
    window.removeEventListener('resize', resize);
    window.removeEventListener('pointermove', onPointer);
    state.ctx.clearRect(0, 0, state.w, state.h);
    state = null;
}
