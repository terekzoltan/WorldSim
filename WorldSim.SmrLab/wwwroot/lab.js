"use strict";
const $ = id => document.getElementById(id);
const label = value => value === null || value === undefined ? "UNKNOWN" : String(value);
const el = (name, text, parent, className) => {
  const node = document.createElement(name);
  node.textContent = label(text);
  if (className) node.className = className;
  if (parent) parent.append(node);
  return node;
};
const api = async path => {
  const response = await fetch(path);
  if (!response.ok) throw Error(`Read unavailable (${response.status})`);
  return response.json();
};
let bundles = [], active, selected, left, right;
let selectionRevision = 0;
function invalidateComparison() {
  selectionRevision++;
  const report = $("report");
  report.hidden = true;
  report.removeAttribute("href");
  $("comparison").replaceChildren();
}
function limitations(parent, values) { for (const value of values || []) el("p", value, parent, "warning"); }
function rows(parent, entries) {
  const table = document.createElement("table"); parent.append(table);
  for (const [key, value] of entries) {
    const tr = document.createElement("tr"); table.append(tr);
    el("th", key, tr); el("td", value, tr, value === "UNKNOWN" ? "unknown" : "");
  }
}
function params(pair) { return { bundle: pair.bundle.id, run: pair.run.id }; }
function updateSelections() {
  $("selections").textContent = `Left: ${left ? left.run.config + "/" + left.run.planner + "/" + left.run.seed : "none"} | Right: ${right ? right.run.config + "/" + right.run.planner + "/" + right.run.seed : "none"}`;
}
async function openRun(bundle, run) {
  selected = await api(`/api/bundles/${encodeURIComponent(bundle.id)}/runs/${encodeURIComponent(run.id)}`);
  $("detail").replaceChildren();
  el("h3", `${selected.config} / ${selected.planner} / seed ${selected.seed}`, $("detail"));
  rows($("detail"), [["Source file", selected.source], ["Assertion failures", label(selected.assertionFailures)],
    ["Assertion skips", label(selected.assertionSkips)], ["Timeline", selected.timeline],
    ...Object.entries(selected.fields)]);
  limitations($("detail"), selected.limitations);
  for (const item of selected.assertions.filter(a => a.kind !== "PASS")) el("p", `${item.kind} ${item.id}: ${item.message}`, $("detail"), item.kind === "FAIL" ? "error" : "warning");
  for (const item of selected.anomalies) el("p", `Anomaly ${item.kind} ${item.id}: ${item.message}`, $("detail"), "warning");
  drawChart();
  $("events").replaceChildren();
  el("h3", "Retained events", $("events"));
  if (!selected.events.length) el("p", selected.timeline === "available" ? "No retained events for this selection." : "Events not retained for this run.", $("events"));
  for (const item of selected.events) el("p", `${item.kind} ${item.id}: ${item.message}`, $("events"));
}
function drawChart() {
  const parent = $("chart"); parent.replaceChildren();
  if (!selected || selected.timeline !== "available" || !selected.samples.length) {
    el("p", "Timeline not retained for this run.", parent, "warning"); return;
  }
  const metric = $("metric").value;
  const points = selected.samples.filter(s => typeof s[metric] === "number" && Number.isFinite(s[metric]));
  if (!points.length) { el("p", `${metric}: series not retained.`, parent, "warning"); return; }
  el("p", `${metric} — actual retained ticks; sampling interval ${label(selected.sampleEvery)} ticks (first/final may be irregular). Source: ${selected.source.replace(/runs\/[^/]+\.json$/, "drilldown selected timeline")}`, parent);
  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("viewBox", "0 0 640 180"); svg.setAttribute("role", "img");
  svg.setAttribute("aria-label", `${metric} retained samples by tick`);
  parent.append(svg);
  const minX = Math.min(...points.map(p => p.tick)), maxX = Math.max(...points.map(p => p.tick));
  const minY = Math.min(...points.map(p => p[metric])), maxY = Math.max(...points.map(p => p[metric]));
  const line = document.createElementNS("http://www.w3.org/2000/svg", "polyline");
  line.setAttribute("fill", "none"); line.setAttribute("stroke", "#59d1b5"); line.setAttribute("stroke-width", "2");
  line.setAttribute("points", points.map(p => `${20 + 600 * (p.tick - minX) / (maxX - minX || 1)},${160 - 140 * (p[metric] - minY) / (maxY - minY || 1)}`).join(" "));
  svg.append(line);
  const summary = el("p", `First retained: tick ${points[0].tick}, ${metric}=${points[0][metric]}; last: tick ${points.at(-1).tick}, ${metric}=${points.at(-1)[metric]}.`, parent, "subtle");
  summary.setAttribute("aria-live", "polite");
  const table = document.createElement("details"); parent.append(table);
  el("summary", `Inspect all ${points.length} source tick/value pairs`, table);
  rows(table, points.map(p => [`tick ${p.tick}`, p[metric]]));
}
function renderRuns() {
  const box = $("runs"); box.replaceChildren();
  if (!active) return;
  const metric = $("sort").value;
  const sorted = [...active.runs].sort((a, b) => {
    const number = r => metric === "failures" ? r.assertionFailures : Number(r.fields[metric]);
    const x = number(a), y = number(b);
    return (x === null || !Number.isFinite(x) ? 1 : y === null || !Number.isFinite(y) ? -1 : y - x) || a.id - b.id;
  });
  for (const run of sorted) {
    const row = document.createElement("div"); row.className = "run"; box.append(row);
    const open = el("button", "Inspect", row); open.addEventListener("click", () => openRun(active, run).catch(showError));
    for (const [side, name] of [["left", "Left"], ["right", "Right"]]) {
      const button = el("button", name, row);
      button.addEventListener("click", () => {
        if (side === "left") left = { bundle: active, run }; else right = { bundle: active, run };
        invalidateComparison();
        updateSelections();
      });
    }
    el("span", `${run.config} / ${run.planner} / seed ${run.seed} | failures ${label(run.assertionFailures)} | skips ${label(run.assertionSkips)} | ${run.timeline}`, row);
  }
}
function showError(error) { el("p", error.message, $("comparison"), "error"); }
async function load() {
  const roots = await api("/api/roots");
  $("roots").textContent = roots.map(r => r.name).join(" · ");
  bundles = await api("/api/bundles");
  $("bundles").replaceChildren();
  for (const bundle of bundles) {
    const line = document.createElement("div"); line.className = "bundle"; $("bundles").append(line);
    const button = el("button", `Open ${bundle.root}`, line);
    button.addEventListener("click", () => { active = bundle; renderRuns(); });
    el("span", ` ${bundle.status} ${bundle.format} | declared runs ${label(bundle.declaredRuns)} | displayed ${bundle.runs.length} | exit ${label(bundle.exitCode)} | source/build ${bundle.provenance} | ${bundle.source}`, line);
    limitations(line, bundle.limitations);
  }
  if (bundles.length) { active = bundles[0]; renderRuns(); }
}
$("sort").addEventListener("change", renderRuns);
$("metric").addEventListener("change", drawChart);
$("compare").addEventListener("click", async () => {
  if (!left || !right) { el("p", "Select both runs first.", $("comparison"), "warning"); return; }
  const revision = selectionRevision;
  const qs = new URLSearchParams({ leftBundle: left.bundle.id, leftRun: left.run.id, rightBundle: right.bundle.id, rightRun: right.run.id });
  const report = $("report"); report.hidden = true; report.removeAttribute("href");
  try {
    const result = await api(`/api/compare?${qs}`);
    if (revision !== selectionRevision) return;
    $("comparison").replaceChildren();
    el("p", result.inference, $("comparison"), "warning");
    rows($("comparison"), result.differences.map(d => [d.field, `${d.left} → ${d.right}`]));
    limitations($("comparison"), result.limitations);
    report.href = `/api/report?${qs}`; report.hidden = false;
  } catch (error) { if (revision === selectionRevision) showError(error); }
});
load().catch(showError);
