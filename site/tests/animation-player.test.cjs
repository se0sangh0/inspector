const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');

// Exercise the shipped controller with deferred image loads and a deterministic clock.
// Layout and actual image rendering are checked separately in the browser.
const html = fs.readFileSync(path.join(__dirname, '../preview/index.html'), 'utf8');
const source = html.slice(html.indexOf('      const spritePlayers ='), html.indexOf('      const formatCompanionText ='));
assert.ok(source.includes('loadSpriteSheet'), 'Locate the shipped animation controller');

function fixture() {
  function element(dataset = {}) {
    return { dataset, style: {}, attrs: {}, events: {},
      setAttribute(key, value) { this.attrs[key] = value; },
      addEventListener(key, fn) { this.events[key] = fn; }
    };
  }
  const play = element(), sheet = element(), poster = element();
  const modes = [element({ motion: 'idle' }), element({ motion: 'attack' })];
  const player = element({ character: 'caster', motion: 'idle', loaded: 'false' });
  player.querySelector = s => ({ '[data-sprite-play]': play, '[data-sprite-sheet]': sheet, '.inspector-sprite-poster': poster })[s];
  player.querySelectorAll = () => modes;
  const images = [], raf = [], events = {}, media = {};
  const document = { hidden: false, addEventListener: (k, fn) => { events[k] = fn; } };
  let observer, now = 10000;
  class Image { constructor() { images.push(this); } }
  class IntersectionObserver { constructor(fn) { observer = fn; } observe() {} }
  const context = {
    root: { querySelectorAll: () => [player] }, select: { value: 'ko' },
    copy: { ko: { playMotion: 'play', pauseMotion: 'pause', loadingMotion: 'loading', retryMotion: 'retry' } },
    window: { IntersectionObserver, matchMedia: () => ({ addEventListener: (k, fn) => { media[k] = fn; } }), requestAnimationFrame: fn => { raf.push(fn); return raf.length; } },
    Image, IntersectionObserver, document, performance: { now: () => now }
  };
  vm.runInNewContext(source, context);
  return { player, play, sheet, poster, images, document,
    click: () => play.events.click(),
    motion: name => modes.find(m => m.dataset.motion === name).events.click(),
    visibility: visible => observer([{ target: player, isIntersecting: visible }]),
    hide: () => { document.hidden = true; events.visibilitychange(); },
    reduce: () => media.change({ matches: true }),
    tick: elapsed => { now = 10000 + elapsed; assert.ok(raf.length); raf.shift()(now); }
  };
}

test('initial state is static and does not request an animation sheet', () => {
  const f = fixture();
  assert.equal(f.player.dataset.playing, 'false');
  assert.equal(f.images.length, 0);
});
test('explicit play loads the selected sheet and advances all eight frames', () => {
  const f = fixture(); f.click();
  assert.match(f.images[0].src, /caster-idle.webp$/);
  assert.equal(f.play.disabled, true);
  f.images[0].onload();
  assert.equal(f.player.dataset.playing, 'true');
  for (let i = 0; i < 8; i++) { f.tick(i * 200); assert.equal(f.player.dataset.frame, String(i)); }
  f.click();
  assert.equal(f.player.dataset.playing, 'false');
  assert.equal(f.player.dataset.frame, '7');
});
test('changing motion rejects the previous image even when it loads last', () => {
  const f = fixture(); f.click(); const old = f.images[0];
  f.motion('attack'); f.click(); const current = f.images[1];
  current.onload(); old.onload();
  assert.match(f.sheet.style.backgroundImage, /caster-attack.webp/);
  assert.equal(f.player.dataset.motion, 'attack');
  assert.equal(f.player.dataset.playing, 'true');
});
test('changing motion without pressing play keeps the new poster static', () => {
  const f = fixture(); f.click(); f.motion('attack'); f.images[0].onload();
  assert.equal(f.player.dataset.loaded, 'false');
  assert.equal(f.player.dataset.playing, 'false');
  assert.match(f.poster.src, /caster-attack-poster.webp$/);
});
test('leaving the viewport cancels pending playback and does not auto-resume', () => {
  const f = fixture(); f.click(); f.visibility(false); f.images[0].onload(); f.visibility(true);
  assert.equal(f.player.dataset.playing, 'false');
  assert.equal(f.player.dataset.loading, 'false');
  assert.equal(f.player.dataset.loaded, 'false');
});
test('a hidden document cancels a late image load', () => {
  const f = fixture(); f.click(); f.hide(); f.images[0].onload();
  assert.equal(f.player.dataset.playing, 'false');
  assert.equal(f.player.dataset.loading, 'false');
});
test('enabling reduced motion cancels pending and active playback', () => {
  const f = fixture(); f.click(); f.reduce(); f.images[0].onload();
  assert.equal(f.player.dataset.playing, 'false');
  f.click(); f.images[1].onload(); f.reduce();
  assert.equal(f.player.dataset.playing, 'false');
});
test('load failure preserves the poster and allows retry', () => {
  const f = fixture(); f.click(); f.images[0].onerror();
  assert.equal(f.player.dataset.loaded, 'false');
  assert.equal(f.player.dataset.playing, 'false');
  assert.equal(f.play.disabled, false);
  assert.equal(f.player.dataset.error, 'true');
  f.click(); f.images[1].onload();
  assert.equal(f.player.dataset.error, 'false');
  assert.equal(f.player.dataset.playing, 'true');
});
test('a stale failure cannot stop a successfully loaded new motion', () => {
  const f = fixture(); f.click(); f.motion('attack'); f.click();
  f.images[1].onload(); f.images[0].onerror();
  assert.equal(f.player.dataset.playing, 'true');
  assert.equal(f.player.dataset.error, 'false');
});
test('switching away from a failed motion restores the play label', () => {
  const f = fixture(); f.click(); f.images[0].onerror(); f.motion('attack');
  assert.equal(f.play.textContent, 'play');
  assert.equal(f.player.dataset.error, 'false');
});
