// Synthetic DOM and API responses only: no browser observation or ScenarioRunner process.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const vm = require('node:vm');

class Element {
  constructor() { this.children = []; this.handlers = {}; this.hidden = true; this.value = ''; this.disabled = false; }
  set textContent(value) { this.text = value; this.children = []; }
  get textContent() { return this.text; }
  append(child) { this.children.push(child); }
  replaceChildren() { this.children = []; }
  addEventListener(event, handler) { this.handlers[event] = handler; }
  querySelectorAll() { return []; }
}

const flush = () => new Promise(resolve => setImmediate(resolve));
function harness(states) {
  const ids = ['manual-section', 'manual-status', 'manual-preview', 'manual-start', 'manual-new',
    'manual-blocker', 'manual-preset', 'manual-seed', 'manual-planner', 'manual-ticks', 'manual-mode'];
  const nodes = Object.fromEntries(ids.map(id => [id, new Element()]));
  const intent = { intentId: 'synthetic-intent', request: { preset: 'lab_small', seed: 101,
    planner: 'Simple', ticks: 40, mode: 'standard' } };
  let tick, intervalCount = 0, index = 0;
  const storage = { getItem: () => JSON.stringify(intent), removeItem: () => {} };
  const base = { id: intent.intentId, createdAt: '2026-09-30T00:00:00Z',
    processStatus: 'RUNNING', artifactStatus: 'UNKNOWN', runnerHash: 'synthetic', diagnostic: 'synthetic' };
  const fetch = async path => path === '/api/manual/options'
    ? { ok: true, json: async () => ({ enabled: true, token: 'synthetic' }) }
    : { ok: true, json: async () => ({ ...base, ...states[Math.min(index++, states.length - 1)] }) };
  const context = { document: { getElementById: id => nodes[id], createElement: () => new Element() },
    fetch, crypto: { randomUUID: () => 'unused' }, sessionStorage: storage,
    setInterval: callback => { tick = callback; intervalCount++; return intervalCount; }, clearInterval: () => {},
    window: { dispatchEvent: () => {} }, CustomEvent: class { constructor(name, detail) { this.name = name; this.detail = detail; } } };
  vm.runInNewContext(readFileSync(resolve(__dirname, '../WorldSim.SmrLab/wwwroot/manual.js'), 'utf8'), context);
  return { nodes, get tick() { return tick; }, get intervalCount() { return intervalCount; } };
}

test('synthetic preview, running elapsed, pending and terminal axes expose one Open link only after publication', async () => {
  const ui = harness([
    { processStatus: 'RUNNING' },
    { processStatus: 'EXITED', exitCode: 0, finishedAt: '2026-09-30T00:00:10Z',
      publicationStatus: 'PENDING', artifactStatus: 'COMPLETE', assertionFailures: 0, assertionSkipped: 3, anomalyCount: 0 },
    { processStatus: 'EXITED', exitCode: 0, finishedAt: '2026-09-30T00:00:10Z',
      publicationStatus: 'PUBLISHED', bundleId: 'verified', artifactStatus: 'COMPLETE',
      assertionFailures: 0, assertionSkipped: 3, anomalyCount: 0 }
  ]);
  await flush(); await flush();
  assert.match(ui.nodes['manual-preview'].textContent, /1 combination; total requested ticks 40/);
  assert.match(ui.nodes['manual-status'].children[0].textContent, /process RUNNING.*elapsed \d+s/);
  assert.equal(ui.nodes['manual-new'].hidden, true);
  assert.equal(ui.intervalCount, 1);
  await ui.tick();
  assert.match(ui.nodes['manual-status'].children[0].textContent, /process EXITED.*artifacts COMPLETE.*failures 0, skips 3, anomalies 0.*elapsed 10s/);
  assert.match(ui.nodes['manual-status'].children.at(-1).textContent, /publication pending/i);
  assert.equal(ui.nodes['manual-new'].hidden, true);
  assert.equal(ui.nodes['manual-status'].children.some(child => child.textContent?.includes('Open reconciled')), false);
  await ui.tick();
  assert.equal(ui.nodes['manual-new'].hidden, false);
  assert.equal(ui.nodes['manual-status'].children.filter(child => child.textContent === 'Open reconciled bundle in artifact browser').length, 1);
});

test('settled no-bundle permits another intent without a link; uncertainty never does', async () => {
  for (const [state, mayStart] of [['NO_BUNDLE', true], ['UNCERTAIN', false]]) {
    const ui = harness([{ processStatus: 'EXITED', finishedAt: '2026-09-30T00:00:10Z',
      publicationStatus: state, bundleId: 'unverified', artifactStatus: 'PARTIAL' }]);
    await flush(); await flush();
    assert.equal(ui.nodes['manual-new'].hidden, !mayStart);
    assert.equal(ui.intervalCount, 0);
    assert.equal(ui.nodes['manual-status'].children.some(child => child.textContent?.includes('Open reconciled')), false);
    assert.match(ui.nodes['manual-status'].children.at(-1).textContent,
      mayStart ? /without a readable bundle/ : /uncertain/);
  }
});
