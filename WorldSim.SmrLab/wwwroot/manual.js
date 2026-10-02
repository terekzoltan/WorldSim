"use strict";
(() => {
  const node = id => document.getElementById(id);
  const text = (parent, value) => { const p = document.createElement("p"); p.textContent = String(value); parent.append(p); return p; };
  const storageKey = "worldsim-smr-lab-b-intent";
  let options, activeIntent, timer, polling = false;
  const statusBox = node("manual-status");
  const selection = () => ({ profile: "core", preset: node("manual-preset").value,
    seed: Number(node("manual-seed").value), planner: node("manual-planner").value,
    ticks: Number(node("manual-ticks").value), mode: node("manual-mode").value });
  const preview = () => {
    const request = selection();
    const small = request.preset === "lab_small";
    node("manual-preview").textContent = `core/${request.preset}: ${small ? "32×20, 12" : "64×40, 24"} initial people; ` +
      `seed ${request.seed}, ${request.planner}, ${request.mode}, ${request.ticks} ticks at Dt 0.25. ` +
      `1 combination; total requested ticks ${request.ticks}. Combat/diplomacy/stone/predator-human OFF; siege ON; birth/movement ×1; Headless.`;
    node("manual-start").disabled = !!activeIntent || !Number.isInteger(request.seed) || request.seed < 1 ||
      request.seed > 1000000 || !Number.isInteger(request.ticks) || request.ticks < 10 || request.ticks > 120 ||
      !!options?.blocker;
  };
  async function poll() {
    if (!activeIntent) return;
    if (polling) return true;
    polling = true;
    try {
      const response = await fetch(`/api/manual/jobs/${encodeURIComponent(activeIntent.intentId)}`, { cache: "no-store" });
      if (!response.ok) throw Error(`Status unavailable (${response.status}); do not start again.`);
      const job = await response.json();
      statusBox.replaceChildren();
      node("manual-new").hidden = true;
      const elapsed = ((new Date(job.finishedAt || Date.now()) - new Date(job.createdAt)) / 1000).toFixed(0);
      text(statusBox, `Job ${job.id} | process ${job.processStatus} (exit ${job.exitCode ?? "UNKNOWN"}) | ` +
        `artifacts ${job.artifactStatus} | failures ${job.assertionFailures ?? "UNKNOWN"}, ` +
        `skips ${job.assertionSkipped ?? "UNKNOWN"}, anomalies ${job.anomalyCount ?? "UNKNOWN"} | elapsed ${elapsed}s.`);
      text(statusBox, job.diagnostic);
      text(statusBox, `Runner binary SHA-256: ${job.runnerHash}; binary-to-source provenance UNKNOWN unless independently established.`);
      if (job.publicationStatus === "PUBLISHED" && job.bundleId) {
        const link = document.createElement("button"); link.type = "button";
        link.textContent = "Open reconciled bundle in artifact browser";
        link.addEventListener("click", () => window.dispatchEvent(new CustomEvent("lab:published", { detail: job.bundleId })));
        statusBox.append(link);
      }
      if (job.publicationStatus === "PENDING") {
        text(statusBox, "Process finished; publication pending. Retain this intent while evidence is reconciled.");
        return true;
      }
      if (job.publicationStatus === "PUBLISHED" && job.bundleId || job.publicationStatus === "NO_BUNDLE") {
        if (job.publicationStatus === "NO_BUNDLE")
          text(statusBox, "Publication settled without a readable bundle; private evidence retained. No Open link.");
        node("manual-new").hidden = false;
      } else if (job.publicationStatus === "UNCERTAIN" || job.finishedAt || job.processStatus === "UNKNOWN") {
        text(statusBox, "Publication or recovery uncertain; no new Start. Inspect retained evidence.");
      } else {
        return true;
      }
      clearInterval(timer);
      return false;
    } catch (error) { text(statusBox, error.message); clearInterval(timer); return false; }
    finally { polling = false; }
  }
  async function start() {
    if (activeIntent) return;
    const request = selection();
    const intentId = crypto.randomUUID();
    activeIntent = { intentId, request };
    sessionStorage.setItem(storageKey, JSON.stringify(activeIntent));
    preview();
    try {
      const response = await fetch("/api/manual/jobs", {
        method: "POST", cache: "no-store", headers: { "Content-Type": "application/json", "X-Lab-Token": options.token },
        body: JSON.stringify({ intentId, ...request })
      });
      if (!response.ok) throw Error(`Start rejected (${response.status}); retain this intent, inspect status before another Start.`);
      if (await poll()) timer = setInterval(poll, 1000);
    } catch (error) { text(statusBox, error.message); }
  }
  async function init() {
    const response = await fetch("/api/manual/options", { cache: "no-store" });
    options = await response.json();
    if (!options.enabled) return;
    node("manual-section").hidden = false;
    node("manual-blocker").textContent = options.blocker || "";
    try {
      activeIntent = JSON.parse(sessionStorage.getItem(storageKey) || "null");
      if (activeIntent) {
        const request = activeIntent.request;
        for (const [key, id] of [["preset", "manual-preset"], ["seed", "manual-seed"],
          ["planner", "manual-planner"], ["ticks", "manual-ticks"], ["mode", "manual-mode"]])
          node(id).value = request[key];
      }
    } catch { activeIntent = null; }
    for (const element of node("manual-section").querySelectorAll("input, select"))
      element.addEventListener("change", preview);
    node("manual-start").addEventListener("click", start);
    node("manual-new").addEventListener("click", () => {
      sessionStorage.removeItem(storageKey); activeIntent = null; node("manual-new").hidden = true;
      statusBox.replaceChildren(); preview();
    });
    preview();
    if (activeIntent && await poll()) timer = setInterval(poll, 1000);
  }
  init().catch(error => text(statusBox, error.message));
})();
