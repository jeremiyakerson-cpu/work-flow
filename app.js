const STORAGE_KEY = "cen-study-hub-domain-status";

const DOMAINS = [
  { id: 1, title: "Cardiovascular Emergencies", desc: "18 items · rhythms & ACS", status: "COMPLETE" },
  { id: 2, title: "Environmental & Toxicological Emergencies", desc: "envenomation, OD, heat/cold", status: "PRIORITY" },
  { id: 3, title: "Trauma & Multisystem Shock", desc: "wound care, hemorrhage control", status: "PRIORITY" },
  { id: 4, title: "Professional Issues", desc: "triage, throughput, legal/ethical", status: "PRIORITY" },
  { id: 5, title: "Respiratory Emergencies", desc: "17 items", status: "QUEUED" },
  { id: 6, title: "Neurological Emergencies", desc: "17 items", status: "QUEUED" },
  { id: 7, title: "Gastrointestinal Emergencies", desc: "now a standalone section", status: "QUEUED" },
  { id: 8, title: "Genitourinary, Gyn & Obstetrical", desc: "GU / GYN / OB emergencies", status: "QUEUED" },
  { id: 9, title: "Mental Health Emergencies", desc: "13 items", status: "QUEUED" },
  { id: 10, title: "Medical Emergencies", desc: "15 items · sepsis, endocrine, shock", status: "QUEUED" },
  { id: 11, title: "Head, Eye, Ear, Nose & Throat", desc: "renamed from Maxillofacial/Ocular", status: "QUEUED" },
];

const PRIORITY_WATCH = [
  { title: "Toxicology & environmental emergencies", desc: "Ingestions, envenomation, heat/cold illness, antidote recall.", tag: "NEXT UP", tagClass: "next" },
  { title: "Trauma & wound care", desc: "Hemorrhage control, musculoskeletal injury, wound management.", tag: "QUEUED", tagClass: "queued" },
  { title: "Professional issues", desc: "Triage & mass casualty, legal and ethical scope-of-practice items.", tag: "QUEUED", tagClass: "queued" },
];

const RESOURCES = [
  { kind: "CLINICAL CALC", name: "MDCalc", desc: "Validated scoring tools referenced throughout each section." },
  { kind: "GOVERNING BODY", name: "BCEN", desc: "Source for the July 2026 content outline and exam handbook." },
  { kind: "GUIDELINES", name: "AHA", desc: "Current cardiac and resuscitation guideline updates." },
  { kind: "GUIDELINES", name: "ENA", desc: "Emergency Nurses Association practice standards, incl. ESI v5." },
];

const STATS = [
  { value: "175", label: "TOTAL ITEMS" },
  { value: "150", label: "SCORED ITEMS" },
  { value: "106", label: "PASSING STANDARD" },
  { value: "3 hrs", label: "TEST TIME" },
  { value: "07/06/26", label: "OUTLINE EFFECTIVE" },
];

const STATUS_CYCLE = ["QUEUED", "PRIORITY", "COMPLETE"];

function loadStatuses() {
  try {
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY));
    if (!saved) return null;
    return saved;
  } catch {
    return null;
  }
}

function saveStatuses(statuses) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(statuses));
}

function initStatuses() {
  const saved = loadStatuses();
  if (saved) {
    DOMAINS.forEach((d) => {
      if (saved[d.id]) d.status = saved[d.id];
    });
  }
}

function persist() {
  const map = {};
  DOMAINS.forEach((d) => (map[d.id] = d.status));
  saveStatuses(map);
}

function renderDomainBoard() {
  const grid = document.getElementById("domain-grid");
  grid.innerHTML = "";
  DOMAINS.forEach((domain) => {
    const card = document.createElement("div");
    card.className = "domain-card";

    const top = document.createElement("div");
    top.className = "domain-card-top";

    const index = document.createElement("span");
    index.className = "domain-index";
    index.textContent = String(domain.id).padStart(2, "0");

    const pill = document.createElement("button");
    pill.className = `status-pill ${domain.status}`;
    pill.textContent = domain.status;
    pill.title = "Click to change status";
    pill.addEventListener("click", () => {
      const next = STATUS_CYCLE[(STATUS_CYCLE.indexOf(domain.status) + 1) % STATUS_CYCLE.length];
      domain.status = next;
      persist();
      renderDomainBoard();
      renderProgress();
    });

    top.appendChild(index);
    top.appendChild(pill);

    const title = document.createElement("h3");
    title.className = "domain-title";
    title.textContent = domain.title;

    const desc = document.createElement("p");
    desc.className = "domain-desc";
    desc.textContent = domain.desc;

    card.appendChild(top);
    card.appendChild(title);
    card.appendChild(desc);
    grid.appendChild(card);
  });
}

function renderProgress() {
  const complete = DOMAINS.filter((d) => d.status === "COMPLETE").length;
  document.getElementById("progress-count").textContent = complete;
  document.getElementById("progress-total").textContent = DOMAINS.length;
  const pct = (complete / DOMAINS.length) * 100;
  document.getElementById("progress-fill").style.width = `${pct}%`;
}

function renderPriorityWatch() {
  const grid = document.getElementById("priority-grid");
  grid.innerHTML = "";
  PRIORITY_WATCH.forEach((item) => {
    const card = document.createElement("div");
    card.className = "priority-card";
    card.innerHTML = `
      <h3>${item.title}</h3>
      <p>${item.desc}</p>
      <span class="priority-tag ${item.tagClass}">${item.tag}</span>
    `;
    grid.appendChild(card);
  });
}

function renderResources() {
  const grid = document.getElementById("resource-grid");
  grid.innerHTML = "";
  RESOURCES.forEach((r) => {
    const card = document.createElement("div");
    card.className = "resource-card";
    card.innerHTML = `
      <span class="resource-kind">${r.kind}</span>
      <h3>${r.name}</h3>
      <p>${r.desc}</p>
    `;
    grid.appendChild(card);
  });

  const strip = document.getElementById("stat-strip");
  strip.innerHTML = "";
  STATS.forEach((s) => {
    const cell = document.createElement("div");
    cell.className = "stat-cell";
    cell.innerHTML = `
      <div class="stat-value">${s.value}</div>
      <div class="stat-label">${s.label}</div>
    `;
    strip.appendChild(cell);
  });
}

initStatuses();
renderDomainBoard();
renderProgress();
renderPriorityWatch();
renderResources();
