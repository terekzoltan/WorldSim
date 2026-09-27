// Minimal browser-event harness for the existing read-only Lab UI; no browser package or simulation.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const vm = require('node:vm');

class Element {
  constructor(name) { this.name = name; this.children = []; this.handlers = {}; this.hidden = false; this.href = ''; this.value = ''; }
  set textContent(value) { this.text = value; this.children = []; }
  get textContent() { return this.text; }
  append(child) { this.children.push(child); }
  replaceChildren() { this.children = []; }
  addEventListener(event, handler) { this.handlers[event] = handler; }
  removeAttribute(name) { if (name === 'href') this.href = ''; }
  setAttribute(name, value) { this[name] = value; }
  click() { return this.handlers.click(); }
}

const flush = () => new Promise(resolve => setImmediate(resolve));
test('report link tracks visible pair and ignores an obsolete compare response', async () => {
  const ids = ['roots', 'bundles', 'runs', 'detail', 'chart', 'events', 'selections', 'comparison', 'report', 'sort', 'metric', 'compare'];
  const nodes = Object.fromEntries(ids.map(id => [id, new Element(id)]));
  nodes.report.hidden = true;
  nodes.sort.value = 'failures';
  nodes.metric.value = 'people';
  const bundle = { id: 'fixture', root: 'fixture', status: 'COMPLETE', format: 'smr/v1', declaredRuns: 2,
    exitCode: 0, provenance: 'UNKNOWN', source: 'fixture/summary.json', limitations: [], runs: [
      { id: '0', config: 'a', planner: 'Simple', seed: '1', assertionFailures: 0, assertionSkips: 0, timeline: 'not retained', fields: { people: '2' } },
      { id: '1', config: 'b', planner: 'Simple', seed: '2', assertionFailures: 1, assertionSkips: 0, timeline: 'not retained', fields: { people: '3' } }
    ] };
  let deferNextCompare = false;
  let completeDelayed;
  const fetch = path => {
    if (path === '/api/roots') return Promise.resolve({ ok: true, json: async () => [{ name: 'fixture' }] });
    if (path === '/api/bundles') return Promise.resolve({ ok: true, json: async () => [bundle] });
    if (path.startsWith('/api/compare?')) {
      if (deferNextCompare) {
        deferNextCompare = false;
        return new Promise(resolve => { completeDelayed = () => resolve({ ok: true, json: async () => ({ inference: 'old', differences: [], limitations: [] }) }); });
      }
      return Promise.resolve({ ok: true, json: async () => ({ inference: 'current', differences: [], limitations: [] }) });
    }
    throw Error(`unexpected fetch: ${path}`);
  };
  const context = { document: { getElementById: id => nodes[id], createElement: name => new Element(name) }, fetch,
    URLSearchParams, console };
  vm.runInNewContext(readFileSync(resolve(__dirname, '../WorldSim.SmrLab/wwwroot/lab.js'), 'utf8'), context);
  await flush(); await flush();
  assert.equal(nodes.runs.children.length, 2);
  // The sort is failures-descending: first row is run 1, second row run 0.
  nodes.runs.children[0].children[1].click(); // Left = run 1
  nodes.runs.children[1].children[2].click(); // Right = run 0
  await nodes.compare.click();
  assert.equal(nodes.report.hidden, false);
  assert.match(nodes.report.href, /leftRun=1.*rightRun=0/);

  nodes.runs.children[1].children[1].click(); // Left = run 0, invalidates old report
  assert.equal(nodes.report.hidden, true);
  assert.equal(nodes.report.href, '');
  deferNextCompare = true;
  const pending = nodes.compare.click();
  nodes.runs.children[0].children[2].click(); // Right = run 1 before old compare returns
  completeDelayed();
  await pending;
  assert.equal(nodes.report.hidden, true);
  assert.equal(nodes.report.href, '');
  await nodes.compare.click();
  assert.equal(nodes.report.hidden, false);
  assert.match(nodes.report.href, /leftRun=0.*rightRun=1/);
});
