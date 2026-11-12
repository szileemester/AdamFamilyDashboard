// Háttéranimáció egy teljes képernyős canvasra.
// Takarékos: ~30 fps, legfeljebb 1.5x pixelsűrűség, a háttérbe tett lapon megáll,
// a rendszer "kevesebb mozgás" beállításánál csak egy állóképet rajzol.

let state = null;

const palettes = {
    csendes: { stars: '#cfd0ff', blobs: ['#534AB7', '#3C3489', '#1D9E75'], embers: '#AFA9EC' },
    bento:   { stars: '#b5d4f4', blobs: ['#7F77DD', '#D4537E', '#1D9E75'], embers: '#AFA9EC' },
    meleg:   { stars: '#fac775', blobs: ['#D85A30', '#BA7517', '#993556'], embers: '#F0997B' },
    vilagos: { stars: '#378ADD', blobs: ['#85B7EB', '#AFA9EC', '#9FE1CB'], embers: '#378ADD' },
};

// WMO kód → időjárás-jelenet
function sceneFor(code) {
    if (code == null) return 'clear';
    if ([95, 96, 99].includes(code)) return 'storm';
    if ([71, 73, 75, 77, 85, 86].includes(code)) return 'snow';
    if ((code >= 51 && code <= 67) || (code >= 80 && code <= 82)) return 'rain';
    if ([45, 48].includes(code)) return 'fog';
    if (code === 3 || code === 2) return 'clouds';
    return 'clear';
}

const rand = (a, b) => a + Math.random() * (b - a);

function makeParticles(kind, w, h) {
    const n = { stars: 70, embers: 40, rain: 90, snow: 70, clouds: 6, fog: 5, blobs: 3 }[kind] ?? 0;
    return Array.from({ length: n }, () => ({
        x: rand(0, w), y: rand(0, h),
        r: kind === 'blobs' ? rand(Math.min(w, h) * .35, Math.min(w, h) * .6)
           : kind === 'clouds' || kind === 'fog' ? rand(w * .25, w * .5)
           : kind === 'snow' ? rand(1, 3) : rand(.5, 1.6),
        vx: kind === 'rain' ? -1.2 : kind === 'snow' ? rand(-.3, .3) : kind === 'clouds' || kind === 'fog' ? rand(.08, .25) : rand(-.12, .12),
        vy: kind === 'rain' ? rand(9, 14) : kind === 'snow' ? rand(.5, 1.3) : kind === 'embers' ? rand(-.6, -.2) : rand(-.08, .08),
        a: rand(.2, .9), t: rand(0, Math.PI * 2), c: Math.floor(rand(0, 3)),
    }));
}

function layersFor(opts) {
    if (opts.mode === 'Idojaras') {
        const s = sceneFor(opts.weatherCode);
        return { clear: ['blobs', 'stars'], clouds: ['clouds'], fog: ['fog'], rain: ['clouds', 'rain'],
                 snow: ['clouds', 'snow'], storm: ['clouds', 'rain'] }[s].concat(s === 'storm' ? ['flash'] : []);
    }
    return { csendes: ['stars'], bento: ['blobs'], meleg: ['blobs', 'embers'], vilagos: ['blobs'] }[opts.theme] ?? ['stars'];
}

function draw(ts) {
    const s = state;
    if (!s) return;
    if (!s.reduced) s.raf = requestAnimationFrame(draw);
    if (document.hidden || ts - s.last < 33) return; // ~30 fps
    s.last = ts;

    const { ctx, w, h, pal } = s;
    ctx.clearRect(0, 0, w, h);

    for (const layer of s.layers) {
        const ps = s.particles[layer];
        switch (layer) {
            case 'stars':
                for (const p of ps) {
                    p.t += .02; p.x = (p.x + p.vx + w) % w; p.y = (p.y + p.vy + h) % h;
                    ctx.globalAlpha = p.a * (.55 + .45 * Math.sin(p.t));
                    ctx.fillStyle = pal.stars;
                    ctx.beginPath(); ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2); ctx.fill();
                }
                break;
            case 'embers':
                for (const p of ps) {
                    p.t += .03; p.x += p.vx + Math.sin(p.t) * .3; p.y += p.vy;
                    if (p.y < -10) { p.y = h + 10; p.x = rand(0, w); }
                    ctx.globalAlpha = p.a * .7;
                    ctx.fillStyle = pal.embers;
                    ctx.beginPath(); ctx.arc(p.x, p.y, p.r + .6, 0, Math.PI * 2); ctx.fill();
                }
                break;
            case 'blobs':
                for (const p of ps) {
                    p.t += .004; p.x += Math.cos(p.t) * .35; p.y += Math.sin(p.t * 1.3) * .35;
                    const g = ctx.createRadialGradient(p.x, p.y, 0, p.x, p.y, p.r);
                    g.addColorStop(0, pal.blobs[p.c]); g.addColorStop(1, 'transparent');
                    ctx.globalAlpha = s.light ? .22 : .16;
                    ctx.fillStyle = g; ctx.fillRect(0, 0, w, h);
                }
                break;
            case 'clouds':
            case 'fog':
                for (const p of ps) {
                    p.x += p.vx; if (p.x - p.r > w) p.x = -p.r;
                    const y = layer === 'fog' ? h * .55 + Math.sin(p.t) * h * .3 : p.y * .5;
                    const g = ctx.createRadialGradient(p.x, y, 0, p.x, y, p.r);
                    g.addColorStop(0, s.light ? '#9aa4b5' : '#8a90a0'); g.addColorStop(1, 'transparent');
                    ctx.globalAlpha = layer === 'fog' ? .12 : .09;
                    ctx.fillStyle = g; ctx.fillRect(0, 0, w, h);
                }
                break;
            case 'rain':
                ctx.globalAlpha = .35; ctx.strokeStyle = s.light ? '#378ADD' : '#85B7EB'; ctx.lineWidth = 1;
                ctx.beginPath();
                for (const p of ps) {
                    p.x += p.vx; p.y += p.vy;
                    if (p.y > h) { p.y = rand(-40, 0); p.x = rand(0, w + 60); }
                    ctx.moveTo(p.x, p.y); ctx.lineTo(p.x + p.vx * 2, p.y + p.vy * 1.4);
                }
                ctx.stroke();
                break;
            case 'snow':
                ctx.fillStyle = s.light ? '#85B7EB' : '#ffffff';
                for (const p of ps) {
                    p.t += .02; p.x += p.vx + Math.sin(p.t) * .4; p.y += p.vy;
                    if (p.y > h + 5) { p.y = -5; p.x = rand(0, w); }
                    ctx.globalAlpha = p.a * .8;
                    ctx.beginPath(); ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2); ctx.fill();
                }
                break;
            case 'flash':
                if (Math.random() < .004) s.flash = 1;
                if (s.flash > 0) { ctx.globalAlpha = s.flash * .18; ctx.fillStyle = '#ffffff'; ctx.fillRect(0, 0, w, h); s.flash -= .08; }
                break;
        }
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
    s.particles = Object.fromEntries(['stars', 'embers', 'blobs', 'clouds', 'fog', 'rain', 'snow'].map(k => [k, makeParticles(k, s.w, s.h)]));
    if (s.reduced) { s.last = -1e9; draw(performance.now()); }
}

export function start(canvas, opts) {
    stop();
    if (!canvas || opts.mode === 'Ki') return;
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    state = {
        canvas, ctx: canvas.getContext('2d'), opts, reduced, last: -1e9, flash: 0,
        pal: palettes[opts.theme] ?? palettes.csendes, light: opts.theme === 'vilagos', layers: layersFor(opts),
    };
    window.addEventListener('resize', resize);
    resize();
    if (!reduced) state.raf = requestAnimationFrame(draw);
}

export function stop() {
    if (!state) return;
    cancelAnimationFrame(state.raf);
    window.removeEventListener('resize', resize);
    state.ctx.clearRect(0, 0, state.w, state.h);
    state = null;
}
