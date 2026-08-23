/*
 * Study Guide: interactive rhythm library (live waveform + recognition
 * criteria + treatment + pearls) and ACLS medication cards.
 */

(function () {
  const { RHYTHM_GUIDE, RHYTHM_GROUPS, MED_GUIDE } = EkgEducation;
  let monitor = null;
  let currentKey = "nsr";
  let tab = "rhythms";

  function open() {
    window.showScreen("study-screen");
    renderTabs();
    if (tab === "rhythms") renderRhythms();
    else renderMeds();
  }

  function renderTabs() {
    document.getElementById("study-tab-rhythms").classList.toggle("active", tab === "rhythms");
    document.getElementById("study-tab-meds").classList.toggle("active", tab === "meds");
    document.getElementById("study-rhythms").hidden = tab !== "rhythms";
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
        btn.textContent = RHYTHM_GUIDE[key].name;
        btn.addEventListener("click", () => {
          currentKey = key;
          renderRhythms();
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
  }

  function renderMeds() {
    const grid = document.getElementById("study-med-grid");
    grid.innerHTML = MED_GUIDE.map(
      (m) => `
      <div class="med-card">
        <div class="med-head">
          <h4>${m.name}</h4>
          <span class="med-cls">${m.cls}</span>
        </div>
        <div class="med-row"><span class="med-label">DOSE</span><span>${m.dose}</span></div>
        <div class="med-row"><span class="med-label">USE</span><span>${m.use}</span></div>
        <div class="med-row"><span class="med-label">CAUTION</span><span>${m.caution}</span></div>
        <p class="med-pearl">💡 ${m.pearl}</p>
      </div>`
    ).join("");
  }

  function init() {
    document.getElementById("mode-study").addEventListener("click", open);
    document.getElementById("study-back").addEventListener("click", () => window.showScreen("start-screen"));
    document.getElementById("study-tab-rhythms").addEventListener("click", () => {
      tab = "rhythms";
      renderTabs();
      renderRhythms();
    });
    document.getElementById("study-tab-meds").addEventListener("click", () => {
      tab = "meds";
      renderTabs();
      renderMeds();
    });
  }

  init();
  window.EkgStudy = { open };
})();
