// NCLEX-RN Practice Test — client-side quiz app.
// Loads bank.json (full question bank) and blueprint.json (default category mix),
// both exported from the Python generator. No build step, no dependencies.

const CANONICAL_CATEGORIES = [
  "Management of Care",
  "Safety and Infection Control",
  "Health Promotion and Maintenance",
  "Psychosocial Integrity",
  "Basic Care and Comfort",
  "Pharmacological and Parenteral Therapies",
  "Reduction of Risk Potential",
  "Physiological Adaptation",
];

const state = {
  bank: [],
  byCategory: {},
  blueprint: null,
  testQuestions: [],
  answers: [],     // parallel array to testQuestions
  flagged: new Set(),
  currentIndex: 0,
};

const el = (id) => document.getElementById(id);

async function init() {
  try {
    const [bankRes, blueprintRes] = await Promise.all([
      fetch("bank.json"),
      fetch("blueprint.json"),
    ]);
    state.bank = await bankRes.json();
    state.blueprint = await blueprintRes.json();
  } catch (e) {
    el("bankStatus").textContent =
      "Failed to load question bank. Serve this folder with a local web server (see README.md) rather than opening index.html directly.";
    return;
  }

  for (const cat of CANONICAL_CATEGORIES) {
    state.byCategory[cat] = state.bank.filter((q) => q.category === cat);
  }

  el("bankStatus").textContent = `${state.bank.length} questions loaded across ${CANONICAL_CATEGORIES.length} categories`;

  populateCategorySelect();
  populateCustomCategoryList();
  wireSetupScreen();
  el("startBtn").disabled = false;
}

function populateCategorySelect() {
  const sel = el("categorySelect");
  sel.innerHTML = "";
  for (const cat of CANONICAL_CATEGORIES) {
    const opt = document.createElement("option");
    opt.value = cat;
    opt.textContent = `${cat} (${state.byCategory[cat].length} available)`;
    sel.appendChild(opt);
  }
}

function populateCustomCategoryList() {
  const container = el("customCategoryList");
  container.innerHTML = "";
  for (const cat of CANONICAL_CATEGORIES) {
    const label = document.createElement("span");
    label.textContent = `${cat} (max ${state.byCategory[cat].length})`;
    const input = document.createElement("input");
    input.type = "number";
    input.min = "0";
    input.max = String(state.byCategory[cat].length);
    input.value = String(state.blueprint.categories[cat] ?? 0);
    input.dataset.category = cat;
    input.addEventListener("input", updateCustomTotal);
    container.appendChild(label);
    container.appendChild(input);
  }
  updateCustomTotal();
}

function updateCustomTotal() {
  const inputs = el("customCategoryList").querySelectorAll("input");
  let total = 0;
  for (const input of inputs) total += parseInt(input.value || "0", 10);
  el("customTotal").textContent = String(total);
}

function wireSetupScreen() {
  const modeRadios = document.querySelectorAll('input[name="mode"]');
  for (const radio of modeRadios) {
    radio.addEventListener("change", () => {
      el("blueprintOptions").classList.toggle("hidden", radio.value !== "blueprint" || !radio.checked);
      document.querySelectorAll('input[name="mode"]').forEach((r) => {
        if (r.value === "blueprint") el("blueprintOptions").classList.toggle("hidden", !r.checked);
        if (r.value === "category") el("categoryOptions").classList.toggle("hidden", !r.checked);
        if (r.value === "custom") el("customOptions").classList.toggle("hidden", !r.checked);
      });
    });
  }

  el("startBtn").addEventListener("click", startTest);
  el("retakeBtn").addEventListener("click", () => {
    showScreen("setup");
  });
  el("prevBtn").addEventListener("click", () => goTo(state.currentIndex - 1));
  el("nextBtn").addEventListener("click", () => goTo(state.currentIndex + 1));
  el("submitBtn").addEventListener("click", submitTest);
  el("flagBtn").addEventListener("click", toggleFlag);
}

function getMode() {
  return document.querySelector('input[name="mode"]:checked').value;
}

function buildMix() {
  const mode = getMode();
  if (mode === "blueprint") {
    const count = parseInt(el("blueprintCount").value, 10) || 25;
    const scale = count / state.blueprint.total_items;
    const mix = {};
    let runningTotal = 0;
    const keys = Object.keys(state.blueprint.categories);
    keys.forEach((cat, i) => {
      let n = Math.round(state.blueprint.categories[cat] * scale);
      mix[cat] = n;
      runningTotal += n;
    });
    let diff = count - runningTotal;
    let i = 0;
    while (diff !== 0 && keys.length) {
      const cat = keys[i % keys.length];
      const step = diff > 0 ? 1 : -1;
      mix[cat] = Math.max(0, mix[cat] + step);
      diff -= step;
      i++;
    }
    return mix;
  }
  if (mode === "category") {
    const cat = el("categorySelect").value;
    const count = parseInt(el("categoryCount").value, 10) || 25;
    return { [cat]: count };
  }
  // custom
  const mix = {};
  el("customCategoryList").querySelectorAll("input").forEach((input) => {
    const n = parseInt(input.value || "0", 10);
    if (n > 0) mix[input.dataset.category] = n;
  });
  return mix;
}

function sampleWithoutReplacement(arr, n) {
  const copy = arr.slice();
  for (let i = copy.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [copy[i], copy[j]] = [copy[j], copy[i]];
  }
  return copy.slice(0, n);
}

function startTest() {
  const mix = buildMix();
  const errors = [];
  let selected = [];

  for (const [cat, count] of Object.entries(mix)) {
    const pool = state.byCategory[cat] || [];
    if (count > pool.length) {
      errors.push(`${cat}: requested ${count}, only ${pool.length} available.`);
      continue;
    }
    selected = selected.concat(sampleWithoutReplacement(pool, count));
  }

  if (Object.keys(mix).length === 0 || selected.length === 0) {
    errors.push("No questions selected. Choose at least one category with a count above 0.");
  }

  if (errors.length) {
    el("setupError").textContent = errors.join(" ");
    el("setupError").classList.remove("hidden");
    return;
  }
  el("setupError").classList.add("hidden");

  if (el("shuffleToggle").checked) {
    selected = sampleWithoutReplacement(selected, selected.length);
  }

  state.testQuestions = selected;
  state.answers = selected.map((q) => emptyAnswer(q));
  state.flagged = new Set();
  state.currentIndex = 0;

  renderQuestionNav();
  renderQuestion(0);
  showScreen("quiz");
}

function emptyAnswer(q) {
  if (q.item_type === "sata") return [];
  if (q.item_type === "ordered_response") return q.options.map((_, i) => i); // displayed order == answer order
  if (q.item_type === "fill_in_blank") return "";
  return null; // multiple_choice
}

function hasAnswer(q, ans) {
  if (q.item_type === "multiple_choice") return ans !== null;
  if (q.item_type === "sata") return ans.length > 0;
  if (q.item_type === "fill_in_blank") return ans.trim().length > 0;
  return true; // ordered_response always has a default order
}

function showScreen(name) {
  document.querySelectorAll(".screen").forEach((s) => s.classList.add("hidden"));
  el(`screen-${name}`).classList.remove("hidden");
}

const LETTERS = "ABCDEFG";

function renderQuestion(idx) {
  state.currentIndex = idx;
  const q = state.testQuestions[idx];
  const ans = state.answers[idx];
  const card = el("questionCard");
  card.innerHTML = "";

  const meta = document.createElement("div");
  meta.className = "question-meta";
  meta.textContent = `${q.category} — ${q.subcategory}`;
  card.appendChild(meta);

  const stem = document.createElement("div");
  stem.className = "question-stem";
  stem.textContent = q.stem;
  card.appendChild(stem);

  if (q.item_type === "multiple_choice") {
    renderChoiceList(card, q, ans, false);
  } else if (q.item_type === "sata") {
    const note = document.createElement("p");
    note.className = "hint";
    note.textContent = "Select all that apply.";
    card.appendChild(note);
    renderChoiceList(card, q, ans, true);
  } else if (q.item_type === "ordered_response") {
    const note = document.createElement("p");
    note.className = "hint";
    note.textContent = "Use the arrows to place these in the correct order (first action/step at top).";
    card.appendChild(note);
    renderOrderedList(card, q, ans);
  } else if (q.item_type === "fill_in_blank") {
    const input = document.createElement("input");
    input.type = "text";
    input.className = "fill-blank-input";
    input.placeholder = "Type your answer";
    input.value = ans;
    input.addEventListener("input", () => {
      state.answers[idx] = input.value;
      updateNavButtonState(idx);
    });
    card.appendChild(input);
  }

  updateProgress();
  updateNavButtonState(idx);
  renderQuestionNavActiveState();

  el("prevBtn").disabled = idx === 0;
  const isLast = idx === state.testQuestions.length - 1;
  el("nextBtn").classList.toggle("hidden", isLast);
  el("submitBtn").classList.toggle("hidden", !isLast);

  el("flagBtn").classList.toggle("flagged", state.flagged.has(idx));
  el("flagBtn").textContent = state.flagged.has(idx) ? "Unflag" : "Flag for review";
}

function renderChoiceList(card, q, ans, multi) {
  const list = document.createElement("div");
  q.options.forEach((opt, i) => {
    const row = document.createElement("label");
    row.className = "option-row";
    const input = document.createElement("input");
    input.type = multi ? "checkbox" : "radio";
    input.name = "option";
    if (multi) {
      input.checked = ans.includes(i);
    } else {
      input.checked = ans === i;
    }
    input.addEventListener("change", () => {
      if (multi) {
        const current = state.answers[state.currentIndex];
        if (input.checked) {
          if (!current.includes(i)) current.push(i);
        } else {
          state.answers[state.currentIndex] = current.filter((x) => x !== i);
        }
      } else {
        state.answers[state.currentIndex] = i;
      }
      updateNavButtonState(state.currentIndex);
    });
    const text = document.createElement("span");
    text.textContent = `${LETTERS[i]}. ${opt}`;
    row.appendChild(input);
    row.appendChild(text);
    list.appendChild(row);
  });
  card.appendChild(list);
}

function renderOrderedList(card, q, order) {
  const ul = document.createElement("ul");
  ul.className = "ordered-list";

  function draw() {
    ul.innerHTML = "";
    order.forEach((optIdx, position) => {
      const li = document.createElement("li");
      li.className = "ordered-item";
      const rank = document.createElement("span");
      rank.className = "rank";
      rank.textContent = `${position + 1}.`;
      const text = document.createElement("span");
      text.textContent = q.options[optIdx];
      const btns = document.createElement("span");
      btns.className = "order-btns";
      const up = document.createElement("button");
      up.textContent = "▲";
      up.type = "button";
      up.disabled = position === 0;
      up.addEventListener("click", () => {
        [order[position - 1], order[position]] = [order[position], order[position - 1]];
        draw();
        updateNavButtonState(state.currentIndex);
      });
      const down = document.createElement("button");
      down.textContent = "▼";
      down.type = "button";
      down.disabled = position === order.length - 1;
      down.addEventListener("click", () => {
        [order[position + 1], order[position]] = [order[position], order[position + 1]];
        draw();
        updateNavButtonState(state.currentIndex);
      });
      btns.appendChild(up);
      btns.appendChild(down);
      li.appendChild(rank);
      li.appendChild(text);
      li.appendChild(btns);
      ul.appendChild(li);
    });
  }
  draw();
  card.appendChild(ul);
}

function goTo(idx) {
  if (idx < 0 || idx >= state.testQuestions.length) return;
  renderQuestion(idx);
}

function toggleFlag() {
  const idx = state.currentIndex;
  if (state.flagged.has(idx)) state.flagged.delete(idx);
  else state.flagged.add(idx);
  renderQuestion(idx);
}

function updateProgress() {
  el("progressText").textContent = `Question ${state.currentIndex + 1} of ${state.testQuestions.length}`;
  const minutes = Math.round(state.testQuestions.length * 1.15);
  el("suggestedTime").textContent = `Suggested time: ~${minutes} min`;
}

function renderQuestionNav() {
  const nav = el("questionNav");
  nav.innerHTML = "";
  state.testQuestions.forEach((_, i) => {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.textContent = String(i + 1);
    btn.addEventListener("click", () => goTo(i));
    nav.appendChild(btn);
  });
}

function renderQuestionNavActiveState() {
  const buttons = el("questionNav").querySelectorAll("button");
  buttons.forEach((btn, i) => {
    btn.classList.toggle("current", i === state.currentIndex);
  });
}

function updateNavButtonState(idx) {
  const buttons = el("questionNav").querySelectorAll("button");
  const btn = buttons[idx];
  if (!btn) return;
  const q = state.testQuestions[idx];
  btn.classList.toggle("answered", hasAnswer(q, state.answers[idx]));
  btn.classList.toggle("flagged", state.flagged.has(idx));
}

function normalizeText(s) {
  return String(s).trim().toLowerCase().replace(/\s+/g, " ");
}

function isCorrect(q, ans) {
  if (q.item_type === "multiple_choice") {
    return ans === q.answer[0];
  }
  if (q.item_type === "sata") {
    const a = [...ans].sort((x, y) => x - y);
    const b = [...q.answer].sort((x, y) => x - y);
    return a.length === b.length && a.every((v, i) => v === b[i]);
  }
  if (q.item_type === "ordered_response") {
    return ans.length === q.answer.length && ans.every((v, i) => v === q.answer[i]);
  }
  if (q.item_type === "fill_in_blank") {
    return normalizeText(ans) === normalizeText(q.answer[0]);
  }
  return false;
}

function formatAnswerText(q, ans) {
  if (q.item_type === "multiple_choice") {
    return ans === null ? "(no answer)" : `${LETTERS[ans]}. ${q.options[ans]}`;
  }
  if (q.item_type === "sata") {
    if (!ans.length) return "(no answer)";
    return [...ans].sort((a, b) => a - b).map((i) => `${LETTERS[i]}. ${q.options[i]}`).join("; ");
  }
  if (q.item_type === "ordered_response") {
    return ans.map((i) => `${LETTERS[i]}. ${q.options[i]}`).join(" → ");
  }
  if (q.item_type === "fill_in_blank") {
    return ans.trim() ? ans : "(no answer)";
  }
  return "";
}

function formatCorrectAnswerText(q) {
  if (q.item_type === "multiple_choice") return `${LETTERS[q.answer[0]]}. ${q.options[q.answer[0]]}`;
  if (q.item_type === "sata") return q.answer.map((i) => `${LETTERS[i]}. ${q.options[i]}`).join("; ");
  if (q.item_type === "ordered_response") return q.answer.map((i) => `${LETTERS[i]}. ${q.options[i]}`).join(" → ");
  if (q.item_type === "fill_in_blank") return q.answer[0];
  return "";
}

function submitTest() {
  const results = state.testQuestions.map((q, i) => ({
    q,
    ans: state.answers[i],
    correct: isCorrect(q, state.answers[i]),
  }));

  const totalCorrect = results.filter((r) => r.correct).length;
  const pct = Math.round((totalCorrect / results.length) * 100);

  el("scoreHeadline").textContent = `${totalCorrect} / ${results.length} correct (${pct}%)`;

  const byCat = {};
  for (const r of results) {
    const cat = r.q.category;
    byCat[cat] = byCat[cat] || { correct: 0, total: 0 };
    byCat[cat].total++;
    if (r.correct) byCat[cat].correct++;
  }
  const breakdown = el("categoryBreakdown");
  breakdown.innerHTML = "";
  for (const cat of CANONICAL_CATEGORIES) {
    if (!byCat[cat]) continue;
    const row = document.createElement("div");
    row.className = "cat-row";
    row.innerHTML = `<span>${cat}</span><span>${byCat[cat].correct}/${byCat[cat].total}</span>`;
    breakdown.appendChild(row);
  }

  const reviewList = el("reviewList");
  reviewList.innerHTML = "";
  results.forEach((r, i) => {
    const card = document.createElement("div");
    card.className = `card review-item ${r.correct ? "correct" : "incorrect"}`;

    const meta = document.createElement("div");
    meta.className = "question-meta";
    meta.textContent = `Q${i + 1} · ${r.q.category} — ${r.q.subcategory} · ${r.q.clinical_judgment_step} · ${r.q.difficulty}`;
    card.appendChild(meta);

    const stem = document.createElement("div");
    stem.className = "question-stem";
    stem.textContent = r.q.stem;
    card.appendChild(stem);

    const yourAns = document.createElement("div");
    yourAns.className = `review-answer ${r.correct ? "correct" : "incorrect"}`;
    yourAns.textContent = `Your answer: ${formatAnswerText(r.q, r.ans)}`;
    card.appendChild(yourAns);

    if (!r.correct) {
      const correctAns = document.createElement("div");
      correctAns.className = "review-answer correct";
      correctAns.textContent = `Correct answer: ${formatCorrectAnswerText(r.q)}`;
      card.appendChild(correctAns);
    }

    const rationale = document.createElement("div");
    rationale.className = "rationale";
    rationale.textContent = `Rationale: ${r.q.rationale}`;
    card.appendChild(rationale);

    reviewList.appendChild(card);
  });

  showScreen("results");
}

init();
