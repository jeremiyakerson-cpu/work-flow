/*
 * Study Guide: interactive rhythm library. The live strip stays on screen
 * in both tabs; RHYTHMS puts the recognition criteria, treatment and pearls
 * under it, MEDICATIONS puts the drugs for that rhythm under it (followed
 * by the rest of the ACLS medication cards).
 */

(function () {
  const { RHYTHM_GUIDE, RHYTHM_GROUPS, MED_GUIDE, RHYTHM_MEDS } = EkgEducation;
  let monitor = null;
  let currentKey = "nsr";
  let tab = "rhythms";

  function open() {
    window.showScreen("study-screen");
    renderTabs();
    renderRhythms();
  }

  function renderTabs() {
    document.getElementById("study-tab-rhythms").classList.toggle("active", tab === "rhythms");
    document.getElementById("study-tab-meds").classList.toggle("active", tab === "meds");
    document.getElementById("study-tab-rhythms").setAttribute("aria-pressed", String(tab === "rhythms"));
    document.getElementById("study-tab-meds").setAttribute("aria-pressed", String(tab === "meds"));
    document.getElementById("study-rhythm-info").hidden = tab !== "rhythms";
    document.getElementById("study-meds").hidden = tab !== "meds";
  }

  function renderRhythms() {
    const listEl = document.getElementById("study-rhythm-list");
    listEl.innerHTML = "";
    for (const group of RHYTHM_GROUPS) {
      const keys = Object.keys(RHYTHM_GUIDE).filter((k) => RHYTHM_GUIDE[k].group === group);
      if (!keys.length) continue;
      const h = document.createElement("div");
      h.className = "study-group-label";
      h.textContent = group.toUpperCase();
      listEl.appendChild(h);
      for (const key of keys) {
        const btn = document.createElement("button");
        btn.className = `study-rhythm-btn ${key === currentKey ? "active" : ""}`;
        btn.type = "button";
        if (key === currentKey) btn.setAttribute("aria-current", "true");
        btn.textContent = RHYTHM_GUIDE[key].name;
        btn.addEventListener("click", () => {
          currentKey = key;
          renderRhythms();
          // The list is rebuilt, so put keyboard focus back on the pick; on
          // phones the detail sits below the list, so bring it into view.
          const active = listEl.querySelector(".study-rhythm-btn.active");
          if (active) active.focus({ preventScroll: true });
          if (window.matchMedia && window.matchMedia("(max-width: 760px)").matches) {
            document.getElementById("study-rhythm-name").scrollIntoView({ block: "start" });
          }
        });
        listEl.appendChild(btn);
      }
    }
    renderDetail();
  }

  function renderDetail() {
    const g = RHYTHM_GUIDE[currentKey];
    document.getElementById("study-rhythm-name").textContent = g.name;

    if (!monitor) monitor = EkgMonitor.attach("study-canvas");
    monitor.setRhythm(currentKey, { hr: g.demo.hr, perfusing: g.demo.perfusing, lethal: false });

    document.getElementById("study-criteria").innerHTML = `
      <div class="crit-cell"><span class="crit-label">RATE</span><span>${g.criteria.rate}</span></div>
      <div class="crit-cell"><span class="crit-label">REGULARITY</span><span>${g.criteria.regular}</span></div>
      <div class="crit-cell"><span class="crit-label">P WAVES</span><span>${g.criteria.p}</span></div>
      <div class="crit-cell"><span class="crit-label">PR</span><span>${g.criteria.pr}</span></div>
      <div class="crit-cell"><span class="crit-label">QRS</span><span>${g.criteria.qrs}</span></div>
    `;

    document.getElementById("study-body").innerHTML = `
      <p class="study-look">${g.look}</p>
      <h4>Common causes</h4>
      <ul>${g.causes.map((c) => `<li>${c}</li>`).join("")}</ul>
      <h4>Treatment</h4>
      <p>${g.tx}</p>
      <h4>Nursing pearl</h4>
      <p class="study-pearl">💡 ${g.pearls}</p>
      <h4>Don't confuse it with…</h4>
      ${g.confuse.map((c) => `<p class="study-confuse"><strong>${c.with}:</strong> ${c.how}</p>`).join("")}
    `;

    renderMeds();
  }

  function medCard(m, match) {
    return `
      <div class="med-card${match ? " med-card-match" : ""}">
        <div class="med-head">
          <h4>${m.name}</h4>
          <span class="med-cls">${m.cls}</span>
        </div>
        <div class="med-row"><span class="med-label">DOSE</span><span>${m.dose}</span></div>
        <div class="med-row"><span class="med-label">USE</span><span>${m.use}</span></div>
        <div class="med-row"><span class="med-label">CAUTION</span><span>${m.caution}</span></div>
        <p class="med-pearl">💡 ${m.pearl}</p>
      </div>`;
  }

  // Medication info for the rhythm on the strip: its treatment line, the
  // drugs used for it, then every other card for reference.
  function renderMeds() {
    const g = RHYTHM_GUIDE[currentKey];
    const names = RHYTHM_MEDS[currentKey] || [];
    const match = names.map((n) => MED_GUIDE.find((m) => m.name === n)).filter(Boolean);
    const rest = MED_GUIDE.filter((m) => !match.includes(m));

    document.getElementById("study-med-tx").innerHTML = `<span class="med-label">TREATMENT</span><span>${g.tx}</span>`;
    document.getElementById("study-med-match-title").textContent = match.length
      ? `Medications for ${g.name}`
      : `No medication is indicated for ${g.name}`;
    document.getElementById("study-med-match").innerHTML = match.map((m) => medCard(m, true)).join("");
    document.getElementById("study-med-match").hidden = !match.length;
    document.getElementById("study-med-rest-title").textContent = match.length ? "Other medications" : "All medications";
    document.getElementById("study-med-grid").innerHTML = rest.map((m) => medCard(m, false)).join("");
  }

  function init() {
    document.getElementById("mode-study").addEventListener("click", open);
    document.getElementById("study-back").addEventListener("click", () => window.showScreen("start-screen"));
    // Switching tabs only swaps what sits under the strip — the strip
    // itself is left running.
    const showTab = (next) => {
      tab = next;
      renderTabs();
      if (window.matchMedia && window.matchMedia("(max-width: 760px)").matches) {
        document.getElementById("study-rhythm-name").scrollIntoView({ block: "start" });
      }
    };
    document.getElementById("study-tab-rhythms").addEventListener("click", () => showTab("rhythms"));
    document.getElementById("study-tab-meds").addEventListener("click", () => showTab("meds"));
  }

  init();
  window.EkgStudy = { open };
})();
