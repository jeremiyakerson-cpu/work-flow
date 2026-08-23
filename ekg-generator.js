/*
 * Adaptive scenario generator. Composes new megacode scenarios from
 * clinically-vetted building blocks: arc templates (the algorithm being
 * drilled), rotating cause modules (H's & T's), branch twists, patient/
 * setting variation, and vitals jitter — seeded, so each generated case
 * is reproducible. Arc selection is weighted by the user's tracked
 * performance (EkgStats): the weaker an area, the more the generator
 * targets it. Generated cases persist in a local library.
 *
 * Every stem, choice set, and rationale here is fixed vetted text —
 * generation recombines and parameterizes; it never invents clinical
 * content. Educational use only.
 */

(function () {
  const GEN_KEY = "ekg-generated-scenarios-v1";
  const LAST_ARC_KEY = "ekg-generated-last-arc";
  const GEN_CAP = 20;

  // ---------- seeded RNG ----------

  function mulberry32(a) {
    return function () {
      a |= 0;
      a = (a + 0x6d2b79f5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  const pick = (rng, arr) => arr[Math.floor(rng() * arr.length)];
  const jit = (rng, base, spread) => base + Math.floor(rng() * (2 * spread + 1)) - spread;

  // ---------- variation pools ----------

  const SETTINGS = [
    "on the telemetry floor",
    "in the emergency department",
    "on the step-down unit",
    "in the post-anesthesia care unit",
    "on the med-surg floor during night shift",
  ];

  const AGES = { adult: [38, 84], older: [58, 88], younger: [24, 46] };

  function makePatient(rng, band = "adult") {
    const [lo, hi] = AGES[band] || AGES.adult;
    return `${lo + Math.floor(rng() * (hi - lo + 1))}-year-old patient`;
  }

  // ---------- shared vetted stage builders ----------

  function stEpiShockable(ctx) {
    return {
      rhythm: ctx.rhythm,
      vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: jit(ctx.rng, 14, 3) },
      scene: { cpr: true, pads: true, bvm: true, iv: true, meds: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
      narrative: "CPR continues and IV access is established. The rhythm has persisted through defibrillation.",
      question: "Which medication comes first, and how is it dosed?",
      choices: [
        "Epinephrine 1 mg IV/IO, repeated every 3–5 minutes",
        "Amiodarone 150 mg slow infusion over 10 minutes",
        "Atropine 1 mg IV push",
        "Adenosine 6 mg rapid IV push",
      ],
      answer: 0,
      rationale:
        "In a shockable arrest, epinephrine 1 mg IV/IO (repeated every 3–5 minutes) is the first drug, typically after the second shock. The 150 mg amiodarone infusion is the dose for stable VT — not arrest.",
      outcome: "Epi is in. Another cycle, another shock — the rhythm persists. This is now refractory.",
      intervention: "Epinephrine 1 mg IV",
    };
  }

  function stEpiNonShockable(ctx) {
    return {
      rhythm: ctx.rhythm,
      vitals: { hr: ctx.hrShown ?? null, spo2: null, nibp: "--/--", rr: 0, etco2: jit(ctx.rng, 13, 3) },
      scene: { cpr: true, pads: true, bvm: true, iv: true, meds: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
      narrative: "Compressions are running and an IV is available.",
      question: "What is the first-line drug in a non-shockable arrest, and when?",
      choices: [
        "Epinephrine 1 mg IV/IO as soon as possible, repeated every 3–5 minutes",
        "Amiodarone 300 mg IV push",
        "Atropine 1 mg IV",
        "Defibrillation replaces drugs here",
      ],
      answer: 0,
      rationale:
        "In non-shockable arrest (PEA/asystole), early epinephrine matters — 1 mg IV/IO as soon as access is available, then every 3–5 minutes. Atropine is no longer in the arrest algorithm, and antiarrhythmics have no role without a shockable rhythm.",
      outcome: "Epi is in. The team leader calls for the H's and T's — why is this patient in arrest?",
      intervention: "Epinephrine 1 mg IV",
    };
  }

  function stPulseCheckOrganized(ctx) {
    return {
      rhythm: "nsr",
      vitals: { hr: jit(ctx.rng, 78, 8), spo2: null, nibp: "--/--", rr: 0, etco2: jit(ctx.rng, 24, 3) },
      scene: { cpr: true, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
      narrative: "At the 2-minute check, an organized narrow-complex rhythm appears on the screen, and the ETCO2 has climbed.",
      question: "What does the team do with this rhythm check?",
      choices: [
        "Pulse check for no more than 10 seconds — an organized rhythm may still be PEA; if no pulse, resume compressions instantly",
        "Declare ROSC from the monitor alone and stop compressions",
        "Shock the organized rhythm to be safe",
        "Take a full minute to be very sure about the pulse",
      ],
      answer: 0,
      rationale:
        "An organized rhythm triggers a pulse check capped at 10 seconds. The monitor alone never declares ROSC — that's exactly how PEA fools teams — though a rising ETCO2 strongly suggests it. No pulse means back on the chest immediately.",
      outcome: "There's a strong carotid pulse. ROSC.",
      intervention: "Pulse check ≤10 s → ROSC",
    };
  }

  function stPostRosc(ctx, extraChoice, extraRationale, extraOutcome) {
    const bp = `${jit(ctx.rng, 92, 8)}/${jit(ctx.rng, 56, 6)}`;
    return {
      rhythm: "nsr",
      vitals: { hr: jit(ctx.rng, 92, 8), spo2: jit(ctx.rng, 94, 2), nibp: bp, rr: 0, etco2: jit(ctx.rng, 37, 3) },
      scene: { cpr: false, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "WEAK", breathing: "ASSISTED" },
      narrative: `ROSC achieved — organized rhythm with a weak pulse, BP ${bp}. The patient remains unresponsive and ventilated.`,
      question: "What are the immediate post-ROSC priorities?",
      choices: [
        extraChoice ||
          "Titrate oxygen (SpO2 92–98%), keep SBP above 90 with fluids/pressors, obtain a 12-lead EKG, and consider targeted temperature management",
        "Stop all monitoring — the code is over",
        "Give another code-dose epinephrine 1 mg bolus to raise the BP",
        "Extubate now that a pulse is back",
      ],
      answer: 0,
      rationale:
        extraRationale ||
        "Post-cardiac-arrest care: avoid hypoxia and hyperoxia (SpO2 92–98%), keep SBP > 90 (fluids, then an infusion — not another code-dose epi bolus), obtain a 12-lead to look for STEMI, and consider TTM for the comatose patient. Anticipate re-arrest.",
      outcome: extraOutcome || "The post-ROSC bundle is underway and the patient heads to the ICU. Well run.",
      intervention: "Post-ROSC bundle",
    };
  }

  // ---------- PEA / asystole cause modules ----------

  const PEA_CAUSES = {
    hemorrhage: {
      label: "hemorrhagic hypovolemia",
      history: "post-op day 1 after abdominal surgery",
      clue(ctx) {
        return {
          rhythm: "sinus_tach",
          vitals: { hr: jit(ctx.rng, 128, 8), spo2: null, nibp: "--/--", rr: 0, etco2: jit(ctx.rng, 15, 2) },
          scene: { cpr: true, pads: true, bvm: true, iv: true, meds: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
          narrative:
            "Clues assemble: fresh blood in the surgical drains, pale and cool skin, FLAT neck veins — and the underlying rhythm has sped up to a narrow tachycardia.",
          question: "Which reversible cause fits best, and what's the treatment?",
          choices: [
            "Hypovolemia from hemorrhage — rapid volume and blood resuscitation while CPR continues, and call surgery",
            "Tension pneumothorax — needle decompression",
            "Hypothermia — active rewarming",
            "Toxins — give naloxone",
          ],
          answer: 0,
          rationale:
            "A narrow, fast PEA with bloody drains, pallor, and flat neck veins is hemorrhagic hypovolemia. Treatment is volume — crystalloid then blood — plus surgical source control. (Distended neck veins would point at tension pneumo or tamponade instead.)",
          outcome: "Two large-bore IVs run wide open and uncrossmatched blood is hung. The next check finds a femoral pulse.",
          intervention: "Fluids + emergency blood · surgery paged",
        };
      },
      fix(ctx) {
        return {
          rhythm: "sinus_tach",
          vitals: { hr: jit(ctx.rng, 124, 6), spo2: 92, nibp: "78/44", rr: 0, etco2: jit(ctx.rng, 33, 2) },
          scene: { cpr: false, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "WEAK", breathing: "ASSISTED" },
          narrative: "ROSC with a thready pulse — but the bleeding hasn't stopped.",
          question: "What is the priority now?",
          choices: [
            "Continue blood resuscitation and move to definitive hemorrhage control in the OR — the arrest recurs if the cause isn't fixed",
            "Targeted temperature management before anything else",
            "Extubate and observe",
            "Give furosemide for the tachycardia",
          ],
          answer: 0,
          rationale:
            "PEA from hypovolemia re-arrests unless the cause is fixed: keep transfusing and get to surgical control. Other post-ROSC elements matter, but hemorrhage control is THE priority here.",
          outcome: "The OR is ready in fifteen minutes. A bleeding vessel is ligated.",
          intervention: "To OR for hemorrhage control",
        };
      },
    },

    tensionPneumo: {
      label: "tension pneumothorax",
      history: "admitted with rib fractures after a fall",
      clue(ctx) {
        return {
          rhythm: "sinus_tach",
          vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: jit(ctx.rng, 10, 2) },
          scene: { cpr: true, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
          narrative:
            "Before arresting, the patient had absent breath sounds on one side, a deviated trachea, and distended neck veins. The ETCO2 is barely 10 despite good compressions.",
          question: "What must happen alongside CPR and epinephrine?",
          choices: [
            "Needle decompression NOW — this is an obstructive arrest; compressions can't generate output past an obstructed circulation",
            "Standard PEA care alone will fix it",
            "Defibrillate the fast rhythm",
            "Pause CPR for a portable chest X-ray to confirm",
          ],
          answer: 0,
          rationale:
            "Obstructive causes (tension pneumothorax, tamponade, massive PE) are mechanical problems — the decompression IS the resuscitation; drugs and compressions merely bridge to it. And nothing pauses CPR for imaging.",
          outcome: "The needle goes in — a hiss of air, and the ETCO2 leaps. The next check finds a bounding pulse.",
          intervention: "Needle decompression → ROSC",
        };
      },
      fix(ctx) {
        return {
          rhythm: "sinus_tach",
          vitals: { hr: jit(ctx.rng, 122, 6), spo2: 91, nibp: "88/54", rr: 0, etco2: jit(ctx.rng, 34, 2) },
          scene: { cpr: false, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "WEAK", breathing: "ASSISTED" },
          narrative: "ROSC after decompression. The needle catheter is taped in place.",
          question: "The needle worked — what does the patient still need, and why?",
          choices: [
            "A chest tube — needle decompression is temporizing; the catheter can kink or clot and the tension can rebuild",
            "Nothing further — the needle is definitive",
            "Immediate removal of the needle now that there's a pulse",
            "Bilateral prophylactic needles",
          ],
          answer: 0,
          rationale:
            "The needle converts a tension pneumothorax into an open one and buys minutes — never definitive. A chest tube follows as soon as possible; until then, watch for re-tension (falling sats, rising airway pressures, dropping BP).",
          outcome: "The chest tube goes in with a rush of air, and the lung re-expands.",
          intervention: "Chest tube placed",
        };
      },
    },

    hyperK: {
      label: "hyperkalemia",
      history: "on dialysis and missed the last two sessions",
      clue(ctx) {
        return {
          rhythm: "junctional",
          vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: jit(ctx.rng, 13, 2) },
          scene: { cpr: true, pads: true, bvm: true, iv: true, meds: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
          narrative:
            "The rhythm is slow and wide, and the pre-arrest labs just posted: potassium 7.9. The missed dialysis suddenly explains everything.",
          question: "How does the known hyperkalemia change the code?",
          choices: [
            "Run standard care (CPR + epinephrine) AND treat the cause: IV calcium to stabilize the membrane, insulin/D50 and albuterol to shift K+, and push for emergent dialysis",
            "Hyperkalemia doesn't matter during the arrest",
            "Defibrillate the wide slow rhythm",
            "Kayexalate is the emergency drug of choice",
          ],
          answer: 0,
          rationale:
            "The triad: calcium stabilizes the myocardium (but doesn't lower K+), insulin/glucose and albuterol shift potassium into cells, and only dialysis removes it. A wide, slow PEA is a classic hyperkalemic pattern — and it is not shockable.",
          outcome: "Calcium and epi are in, insulin/D50 running… the next check finds an organized rhythm with a pulse.",
          intervention: "Calcium + insulin/D50 · epi given",
        };
      },
      fix(ctx) {
        return {
          rhythm: "nsr",
          vitals: { hr: jit(ctx.rng, 88, 6), spo2: 94, nibp: "92/56", rr: 0, etco2: jit(ctx.rng, 36, 2) },
          scene: { cpr: false, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "WEAK", breathing: "ASSISTED" },
          narrative: "ROSC — but the repeat potassium is still 7.2. The drugs bought time, nothing more.",
          question: "What definitive treatment does this patient need now?",
          choices: [
            "Emergent hemodialysis — the only therapy that actually removes potassium",
            "Another round of calcium is definitive",
            "A 24-hour insulin infusion will fix it permanently",
            "Observation — potassium self-corrects after ROSC",
          ],
          answer: 0,
          rationale:
            "Shifted potassium rebounds as the drugs wear off. In a dialysis patient after a hyperkalemic arrest, emergent hemodialysis is the definitive move, with serial potassium checks and telemetry until it's done.",
          outcome: "The dialysis machine arrives at the bedside. Two hours later the potassium is 5.1.",
          intervention: "Emergent hemodialysis",
        };
      },
    },

    tamponade: {
      label: "cardiac tamponade",
      history: "one day out from a pacemaker implant",
      clue(ctx) {
        return {
          rhythm: "sinus_tach",
          vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: jit(ctx.rng, 11, 2) },
          scene: { cpr: true, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
          narrative:
            "Before the arrest: muffled heart sounds, DISTENDED neck veins, and a narrowing pulse pressure — one day after a cardiac procedure. The ETCO2 stays low despite excellent compressions.",
          question: "Which reversible cause fits, and what is the treatment?",
          choices: [
            "Cardiac tamponade — emergency pericardiocentesis (ideally ultrasound-guided) while CPR continues",
            "Hypovolemia — blood transfusion alone",
            "Toxins — naloxone",
            "Hypothermia — rewarming",
          ],
          answer: 0,
          rationale:
            "JVD + muffled heart sounds + narrowing pulse pressure after a cardiac procedure is tamponade (Beck's triad) — blood in the pericardium is squeezing the heart. It's an obstructive arrest: draining the pericardium IS the resuscitation.",
          outcome: "The provider aspirates 60 mL of blood from the pericardium — and the next rhythm check finds a pulse.",
          intervention: "Pericardiocentesis → ROSC",
        };
      },
      fix(ctx) {
        return {
          rhythm: "sinus_tach",
          vitals: { hr: jit(ctx.rng, 118, 6), spo2: 93, nibp: "90/58", rr: 0, etco2: jit(ctx.rng, 34, 2) },
          scene: { cpr: false, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "WEAK", breathing: "ASSISTED" },
          narrative: "ROSC after pericardiocentesis. The drainage catheter is secured.",
          question: "What comes next for this patient?",
          choices: [
            "Emergent cardiology/cardiac surgery involvement — the bleeding source needs definitive repair, and the effusion can re-accumulate",
            "Remove the pericardial catheter immediately",
            "No follow-up needed once drained",
            "Routine discharge planning",
          ],
          answer: 0,
          rationale:
            "Pericardiocentesis is temporizing when the heart or a vessel is actively bleeding — the effusion re-accumulates. Definitive care means finding and fixing the source, with the drain monitored continuously until then.",
          outcome: "Echo shows a small residual effusion; cardiac surgery takes the patient for exploration.",
          intervention: "To definitive repair",
        };
      },
    },

    pe: {
      label: "massive pulmonary embolism",
      history: "post-op day 2 and reluctant to ambulate",
      clue(ctx) {
        return {
          rhythm: "sinus_tach",
          vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: jit(ctx.rng, 9, 2) },
          scene: { cpr: true, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
          narrative:
            "Minutes before arresting the patient had sudden severe dyspnea and crushing hypoxia. Immobile since surgery, no prophylaxis charted, and the ETCO2 is strikingly low for good CPR — little blood is reaching the lungs.",
          question: "Which reversible cause fits, and what can be done during the arrest?",
          choices: [
            "Massive pulmonary embolism — consider emergency thrombolytics during CPR, and commit to prolonged resuscitation after giving them",
            "Tension pneumothorax — needle decompression",
            "Hyperkalemia — calcium and insulin",
            "Nothing — PE in arrest is untreatable",
          ],
          answer: 0,
          rationale:
            "Sudden dyspnea and collapse in an immobile post-op patient is massive PE — an obstructive arrest with a very low ETCO2 (blood can't reach the lungs). Thrombolytics during CPR are the recognized rescue; once given, resuscitation continues substantially longer to let them work.",
          outcome: "Thrombolytics are pushed and the team keeps going… two cycles later, the ETCO2 climbs and a pulse returns.",
          intervention: "Thrombolytics during CPR → ROSC",
        };
      },
      fix(ctx) {
        return {
          rhythm: "sinus_tach",
          vitals: { hr: jit(ctx.rng, 116, 6), spo2: 90, nibp: "86/52", rr: 0, etco2: jit(ctx.rng, 32, 2) },
          scene: { cpr: false, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "WEAK", breathing: "ASSISTED" },
          narrative: "ROSC after thrombolysis. The patient is fragile, ventilated, and freshly thrombolysed.",
          question: "What is a key nursing priority after thrombolytics?",
          choices: [
            "Bleeding surveillance — neuro checks, puncture sites, drains, and hemoglobin — alongside standard post-ROSC care",
            "Immediate intramuscular injections for pain",
            "Early ambulation within the hour",
            "No special monitoring is required",
          ],
          answer: 0,
          rationale:
            "Thrombolytics dissolve every clot, not just the PE — bleeding (especially intracranial) is the feared complication. Serial neuro checks, site surveillance, and avoiding punctures/IM injections are core nursing care, on top of the post-ROSC bundle.",
          outcome: "The ICU takes over with bleeding precautions in place. A heparin bridge and IVC-filter discussion follow.",
          intervention: "Bleeding precautions · ICU handoff",
        };
      },
    },
  };

  // ---------- arc builders (each returns a 5-stage scenario body) ----------

  function arcShockable(rng) {
    const pt = makePatient(rng, "older");
    const setting = pick(rng, SETTINGS);
    const witnessed = rng() < 0.6;
    const startRhythm = rng() < 0.7 ? "vf_coarse" : "vt";
    const startLabel = startRhythm === "vf_coarse" ? "coarse VF" : "a wide, regular tachycardia with no pulse — pulseless VT";
    const lidoTwist = rng() < 0.35;
    const ctx = { rng, rhythm: startRhythm };

    const s1 = {
      rhythm: startRhythm,
      vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: null },
      scene: { cpr: false, pads: false, bvm: false, iv: false, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "NONE" },
      narrative: witnessed
        ? `You're at the bedside ${setting} when your ${pt} suddenly slumps over — unresponsive, pulseless, not breathing. The monitor shows ${startLabel}.`
        : `Your ${pt} ${setting} is found unresponsive — no pulse, no breathing, downtime unknown. The monitor shows ${startLabel}.`,
      question: "What is your immediate first action?",
      choices: [
        "Start high-quality chest compressions and call a code — get the defibrillator to the bedside",
        "Give amiodarone 300 mg IV push first",
        "Perform a 12-lead EKG to confirm the rhythm",
        "Give 2 rescue breaths and wait for a response",
      ],
      answer: 0,
      rationale:
        "Unresponsive + pulseless = compressions now. Nothing — drugs, airway, or a 12-lead — comes before CPR and the defibrillator when the rhythm is shockable.",
      outcome: "The code team arrives; pads go on with compressions running. The rhythm persists.",
      intervention: "CPR started · code called",
    };

    const s2 = {
      rhythm: startRhythm,
      vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: jit(rng, 15, 3) },
      scene: { cpr: true, pads: true, bvm: true, iv: false, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
      narrative: "Pads are on and the defibrillator is charged. The team pauses briefly: the rhythm is still shockable.",
      question: "What do you do now?",
      choices: [
        "Deliver one unsynchronized shock (biphasic ~200 J per device), then immediately resume compressions for 2 minutes",
        "Deliver three stacked shocks in a row before resuming CPR",
        "Use synchronized cardioversion at 100 J",
        "Hold the shock and give epinephrine first",
      ],
      answer: 0,
      rationale:
        "VF and pulseless VT get a single unsynchronized shock at the device's recommended biphasic energy, followed by immediate compressions for a full 2-minute cycle — no post-shock pulse check, no stacked shocks.",
      outcome: "Shock delivered, compressions resume instantly. At the next check the rhythm persists.",
      intervention: "Defibrillated 200 J",
    };

    const s4 = lidoTwist
      ? {
          rhythm: startRhythm,
          vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: jit(rng, 14, 2) },
          scene: { cpr: true, pads: true, bvm: true, iv: true, meds: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
          narrative:
            "The rhythm has survived three shocks and epinephrine. The team leader calls for an antiarrhythmic — and pharmacy reports the amiodarone is unavailable.",
          question: "What is the alternative antiarrhythmic and its dose?",
          choices: [
            "Lidocaine 1–1.5 mg/kg IV/IO (may repeat 0.5–0.75 mg/kg)",
            "Diltiazem 20 mg IV",
            "Magnesium 4 g IV push for all refractory VF",
            "Procainamide 100 mg/min until conversion",
          ],
          answer: 0,
          rationale:
            "Lidocaine 1–1.5 mg/kg IV/IO is the accepted alternative to amiodarone in VF/pVT arrest. Magnesium in arrest is reserved for torsades; diltiazem has no role.",
          outcome: "Lidocaine in, another shock — and the next check shows an organized rhythm with a pulse. ROSC.",
          intervention: "Lidocaine 1.5 mg/kg · shock → ROSC",
        }
      : {
          rhythm: startRhythm,
          vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: jit(rng, 14, 2) },
          scene: { cpr: true, pads: true, bvm: true, iv: true, meds: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
          narrative: "The rhythm has survived three shocks and a round of epinephrine. The team leader asks for an antiarrhythmic.",
          question: "What do you draw up?",
          choices: [
            "Amiodarone 300 mg IV/IO push",
            "Amiodarone 150 mg over 10 minutes",
            "Diltiazem 20 mg IV",
            "Magnesium 2 g IV as first-line for all refractory VF",
          ],
          answer: 0,
          rationale:
            "Refractory VF/pVT gets amiodarone 300 mg IV/IO push (one repeat of 150 mg allowed). Lidocaine is the alternative; magnesium is reserved for torsades.",
          outcome: "Amiodarone in, another shock — and the next check shows an organized rhythm with a pulse. ROSC.",
          intervention: "Amiodarone 300 mg · shock → ROSC",
        };

    return {
      title: witnessed ? "Witnessed shockable arrest" : "Found down — shockable rhythm",
      blurb: `A ${pt} arrests ${setting} in ${startRhythm === "vf_coarse" ? "coarse VF" : "pulseless VT"}. Run the shockable algorithm to ROSC.`,
      stages: [s1, s2, stEpiShockable(ctx), s4, stPostRosc({ rng })],
    };
  }

  function arcNonShockable(rng, style) {
    const causeKeys = Object.keys(PEA_CAUSES);
    const seenRaw = localStorage.getItem("ekg-gen-seen-causes") || "";
    const seen = seenRaw.split(",").filter(Boolean);
    const fresh = causeKeys.filter((k) => !seen.includes(k));
    const causeKey = pick(rng, fresh.length ? fresh : causeKeys);
    const nextSeen = [...seen, causeKey].slice(-(causeKeys.length - 1));
    try {
      localStorage.setItem("ekg-gen-seen-causes", nextSeen.join(","));
    } catch {}
    const cause = PEA_CAUSES[causeKey];
    const pt = makePatient(rng);
    const setting = pick(rng, SETTINGS);
    const asystole = style === "asystole";
    const shownRhythm = asystole ? "asystole" : "nsr";
    const ctx = { rng, rhythm: shownRhythm, hrShown: asystole ? 0 : jit(rng, 84, 8) };

    const s1 = asystole
      ? {
          rhythm: "asystole",
          vitals: { hr: 0, spo2: null, nibp: "--/--", rr: 0, etco2: null },
          scene: { cpr: false, pads: false, bvm: false, iv: false, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "NONE" },
          narrative: `Your ${pt} (${cause.history}) is found unresponsive ${setting} — no pulse, no breathing. The monitor shows a flat line.`,
          question: "What is the correct response to an apparent flatline?",
          choices: [
            "Start CPR immediately while confirming true asystole — check lead connections, turn up the gain, check a second lead. Never shock a flatline",
            "Defibrillate at 200 J in case it's fine VF",
            "Wait for the provider before starting anything",
            "Give atropine 1 mg and reassess",
          ],
          answer: 0,
          rationale:
            "Compressions start immediately; while they run, confirm the flatline is real — fine VF can masquerade as asystole. Asystole itself is never shocked, and atropine is no longer in the arrest algorithm.",
          outcome: "True asystole, confirmed in two leads. CPR continues and pads go on in case the rhythm changes.",
          intervention: "Asystole confirmed · CPR started",
        }
      : {
          rhythm: "nsr",
          vitals: { hr: ctx.hrShown, spo2: null, nibp: "--/--", rr: 0, etco2: null },
          scene: { cpr: false, pads: false, bvm: false, iv: false, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "NONE" },
          narrative: `Your ${pt} (${cause.history}) is found unresponsive ${setting}. The monitor shows an organized narrow-complex rhythm — but there is no carotid pulse and no breathing.`,
          question: "What is this, and what do you do first?",
          choices: [
            "Pulseless electrical activity — start CPR immediately; an organized tracing does not mean perfusion",
            "A perfusing rhythm — recheck vitals in 15 minutes",
            "A shockable arrest — defibrillate at 200 J",
            "Lead artifact — reposition the electrodes first",
          ],
          answer: 0,
          rationale:
            "Electrical activity without a pulse is PEA — a full arrest, treated with immediate compressions, and never shocked. The pulse check outranks the screen, always.",
          outcome: "CPR starts, the code is called, pads go on.",
          intervention: "PEA recognized · CPR started",
        };

    const lesson = {
      rhythm: "nsr",
      vitals: { hr: jit(rng, 96, 6), spo2: 96, nibp: "104/62", rr: 0, etco2: null },
      scene: { cpr: false, pads: true, bvm: true, iv: true, meds: false, loc: "ALTERED", pulse: "PRESENT", breathing: "ASSISTED" },
      narrative: "Stabilizing. The team debriefs the arrest.",
      question: "Which statement about non-shockable arrest is TRUE?",
      choices: [
        "PEA and asystole are never shocked — survival depends on high-quality CPR, early epinephrine, and finding the reversible cause",
        "One maximum-energy shock is standard for PEA",
        "The H's and T's only apply to asystole",
        "A narrow, fast PEA usually means a primary cardiac cause",
      ],
      answer: 0,
      rationale:
        "Non-shockable rhythms live or die on CPR quality, epinephrine timing, and cause-hunting. The rhythm's own shape is a clue: narrow and fast suggests mechanical/volume causes; wide and slow suggests metabolic ones (hyperkalemia, toxins).",
      outcome: `Cause found — ${cause.label} — and fixed. This one goes in the win column.`,
      intervention: "Debrief · case closed",
    };

    return {
      title: asystole ? `Flatline — ${cause.label}` : `PEA — ${cause.label}`,
      blurb: `A ${pt}, ${cause.history}, arrests ${setting}. Find the cause: ${cause.label}.`,
      stages: [s1, stEpiNonShockable(ctx), cause.clue({ rng }), cause.fix({ rng }), lesson],
    };
  }

  function arcBradycardia(rng) {
    const pt = makePatient(rng, "older");
    const setting = pick(rng, SETTINGS);
    const entry = pick(rng, ["sinus_brady", "mobitz2", "avb3"]);
    const entryDesc = {
      sinus_brady: "a slow, regular rhythm with a P before every QRS",
      mobitz2: "a constant PR interval with intermittently dropped QRS complexes — Mobitz II",
      avb3: "P waves and QRS complexes marching independently — complete heart block",
    }[entry];
    const hr1 = jit(rng, 36, 4);
    const bp1 = `${jit(rng, 80, 6)}/${jit(rng, 48, 6)}`;

    return {
      title: `Unstable bradycardia (${entry === "sinus_brady" ? "sinus" : entry === "mobitz2" ? "Mobitz II" : "complete block"})`,
      blurb: `A ${pt} ${setting} decompensates at ${hr1} bpm. Atropine, pacing, and the escalation ladder.`,
      stages: [
        {
          rhythm: entry,
          vitals: { hr: hr1, spo2: jit(rng, 92, 2), nibp: bp1, rr: 16, etco2: null },
          scene: { cpr: false, pads: false, bvm: false, iv: true, meds: false, loc: "ALTERED", pulse: "PRESENT", breathing: "SPONTANEOUS" },
          narrative: `Your ${pt} ${setting} becomes pale, diaphoretic, and confused. The monitor shows ${entryDesc} at ${hr1}, BP ${bp1}.`,
          question: "This bradycardia is symptomatic. What is the first-line treatment?",
          choices: [
            "Atropine 1 mg IV push, repeatable every 3–5 minutes to a total of 3 mg",
            "Adenosine 6 mg rapid IV push",
            "Immediate defibrillation",
            "Amiodarone 300 mg IV push",
          ],
          answer: 0,
          rationale:
            "Symptomatic bradycardia with a pulse gets atropine 1 mg IV first (max 3 mg) — while recognizing that high-degree AV block often won't respond, so pacing is prepared simultaneously.",
          outcome: "Two doses of atropine change nothing. The patient is becoming harder to rouse.",
          intervention: "Atropine ×2 — no effect",
        },
        {
          rhythm: entry === "sinus_brady" ? "avb3" : entry,
          vitals: { hr: jit(rng, 32, 3), spo2: 90, nibp: "74/42", rr: 14, etco2: null },
          scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
          narrative:
            entry === "sinus_brady"
              ? "The rhythm has degenerated into complete heart block at 32, and the pressure keeps falling."
              : "The block persists and the pressure keeps falling.",
          question: "Atropine has failed and the patient is deteriorating. What's next?",
          choices: [
            "Begin transcutaneous pacing without delay",
            "Give more atropine beyond 3 mg total",
            "Synchronized cardioversion at 100 J",
            "Observe for 30 minutes and recheck",
          ],
          answer: 0,
          rationale:
            "Atropine-refractory, unstable bradycardia — especially infranodal block — needs immediate transcutaneous pacing (or a chronotropic infusion as bridge/alternative).",
          outcome: "Pacing starts at 70/min… but the spikes on the monitor aren't followed by QRS complexes.",
          intervention: "Transcutaneous pacing started",
        },
        {
          rhythm: entry === "sinus_brady" ? "avb3" : entry,
          vitals: { hr: jit(rng, 32, 3), spo2: 90, nibp: "72/40", rr: 14, etco2: null },
          scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
          narrative: "Pacer spikes march across the screen without capturing.",
          question: "What is the problem and the fix?",
          choices: [
            "Failure to capture — increase the output (mA) until every spike produces a QRS, then confirm a matching pulse",
            "Failure to sense — turn the rate down",
            "Normal early pacing behavior — wait 10 minutes",
            "Give more atropine instead",
          ],
          answer: 0,
          rationale:
            "Spikes without QRS complexes = failure to capture. Increase current until electrical capture appears, then always verify mechanical capture with a pulse. Analgesia/sedation as tolerated — pacing hurts.",
          outcome: "At a higher output: consistent capture with a matching pulse at 70. BP creeps up, but the patient is still marginal.",
          intervention: "Output ↑ until capture confirmed",
        },
        {
          rhythm: "junctional",
          vitals: { hr: 70, spo2: 95, nibp: "88/56", rr: 16, etco2: null },
          scene: { cpr: false, pads: true, bvm: false, iv: true, meds: true, loc: "ALTERED", pulse: "PRESENT", breathing: "SPONTANEOUS" },
          narrative: "Paced with capture, but still hypotensive and pacing-dependent.",
          question: "Which infusion supports the patient while definitive care is arranged?",
          choices: [
            "Dopamine 5–20 mcg/kg/min or epinephrine 2–10 mcg/min",
            "Diltiazem 10 mg/hr",
            "Nitroglycerin 20 mcg/min",
            "Metoprolol 5 mg IV",
          ],
          answer: 0,
          rationale:
            "The bradycardia algorithm's infusions are dopamine 5–20 mcg/kg/min or epinephrine 2–10 mcg/min. Diltiazem, metoprolol, and nitroglycerin would each make things worse.",
          outcome: "On the infusion the BP holds above 100. Cardiology is en route.",
          intervention: "Chronotropic infusion titrated",
        },
        {
          rhythm: "junctional",
          vitals: { hr: 70, spo2: 96, nibp: "102/64", rr: 16, etco2: null },
          scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
          narrative: "Awake again, paced and stable. Cardiology asks what you'd anticipate next.",
          question: "What is the definitive plan for atropine-refractory high-degree block?",
          choices: [
            "Transvenous pacing as a bridge, then evaluation for a permanent pacemaker",
            "Discharge home with a Holter monitor",
            "Long-term dopamine infusion at home",
            "Elective cardioversion next week",
          ],
          answer: 0,
          rationale:
            "Transcutaneous pacing is a painful, unreliable bridge. The pathway is transvenous pacing, then permanent pacemaker evaluation — with AV-nodal-blocking home medications reviewed and held.",
          outcome: "Off to the unit with a transvenous wire planned. Textbook escalation.",
          intervention: "Transvenous pacing arranged",
        },
      ],
    };
  }

  function arcTachy(rng) {
    const path = rng() < 0.5 ? "svt" : "afib";
    const pt = makePatient(rng, path === "svt" ? "younger" : "older");
    const setting = pick(rng, SETTINGS);
    if (path === "svt") {
      const hr = jit(rng, 188, 6);
      return {
        title: "Narrow-complex tachycardia decompensates",
        blurb: `A ${pt} in SVT at ${hr} stops being stable mid-treatment. Vagal, adenosine, then electricity.`,
        stages: [
          {
            rhythm: "svt",
            vitals: { hr, spo2: 97, nibp: "112/70", rr: 18, etco2: null },
            scene: { cpr: false, pads: false, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
            narrative: `A ${pt} ${setting} reports sudden palpitations. Alert and warm, BP 112/70; the monitor shows a regular narrow-complex tachycardia at ${hr} with no visible P waves.`,
            question: "The patient is stable. What do you try first?",
            choices: [
              "Vagal maneuvers (e.g., modified Valsalva)",
              "Synchronized cardioversion at 50 J",
              "Amiodarone 300 mg IV push",
              "Defibrillation at 200 J",
            ],
            answer: 0,
            rationale:
              "Stable SVT starts with vagal maneuvers — a properly performed modified Valsalva converts a meaningful fraction of SVT at zero cost. Electricity is for unstable patients.",
            outcome: "Two attempts don't break it. Still stable — for now.",
            intervention: "Vagal maneuvers attempted",
          },
          {
            rhythm: "svt",
            vitals: { hr, spo2: 97, nibp: "108/68", rr: 18, etco2: null },
            scene: { cpr: false, pads: false, bvm: false, iv: true, meds: true, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
            narrative: "Vagal maneuvers failed. An IV is in the right AC.",
            question: "What's the correct next step?",
            choices: [
              "Adenosine 6 mg rapid IV push through the closest large vein, followed immediately by a 20 mL saline flush — and warn the patient about the awful transient feeling",
              "Adenosine 6 mg as a slow infusion over 5 minutes",
              "Diltiazem 20 mg IV push over 10 seconds",
              "Epinephrine 1 mg IV",
            ],
            answer: 0,
            rationale:
              "Adenosine must be slammed — its half-life is seconds. 6 mg rapid push + immediate flush (12 mg next if needed). Warn the patient about the brief chest pressure and sense of doom.",
            outcome: "A moment of pause… then the SVT resumes — and now the patient is gray, diaphoretic, and confused. BP 76/40.",
            intervention: "Adenosine 6 mg — transient effect",
          },
          {
            rhythm: "svt",
            vitals: { hr: hr + 2, spo2: 94, nibp: "76/40", rr: 22, etco2: null },
            scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
            narrative: "The patient is now unstable: hypotensive, altered, poorly perfused — still in SVT. Pads are on.",
            question: "What is indicated NOW?",
            choices: [
              "Synchronized cardioversion (sedate first if it won't delay the shock)",
              "Adenosine 12 mg and reassess in 10 minutes",
              "Unsynchronized defibrillation",
              "Oral metoprolol",
            ],
            answer: 0,
            rationale:
              "Instability ends the medication pathway: an unstable tachyarrhythmia with a pulse gets synchronized cardioversion. An unsynchronized shock risks R-on-T.",
            outcome: "The defibrillator is in SYNC mode — markers align on each R wave.",
            intervention: "Synchronized cardioversion prepared",
          },
          {
            rhythm: "svt",
            vitals: { hr: hr + 2, spo2: 94, nibp: "76/40", rr: 22, etco2: null },
            scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
            narrative: "Everyone is clear.",
            question: "What initial energy is appropriate for a regular narrow-complex tachycardia?",
            choices: ["50–100 J synchronized", "200 J unsynchronized", "360 J synchronized", "5 J synchronized"],
            answer: 0,
            rationale:
              "Regular narrow-complex SVT converts at low energy: 50–100 J synchronized initially, escalating if needed. (Afib typically needs 120–200 J biphasic.)",
            outcome: "One synchronized shock — and sinus rhythm marches out. Color returns.",
            intervention: "Synchronized cardioversion 75 J",
          },
          {
            rhythm: "nsr",
            vitals: { hr: 88, spo2: 98, nibp: "108/66", rr: 16, etco2: null },
            scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
            narrative: "Converted and awake.",
            question: "What completes the post-conversion care?",
            choices: [
              "Continuous monitoring with pads in place, a 12-lead EKG (looking for pre-excitation/WPW), electrolytes, and cardiology follow-up",
              "Remove the monitor — the event is over",
              "Amiodarone 300 mg to prevent recurrence",
              "Immediate discharge, no follow-up",
            ],
            answer: 0,
            rationale:
              "After conversion: monitoring (recurrence is common), a 12-lead to look for WPW or ischemia, electrolytes, and referral — recurrent SVT may warrant an EP study.",
            outcome: "The 12-lead is clean this time. EP referral placed for the recurrent episodes.",
            intervention: "12-lead · EP referral",
          },
        ],
      };
    }
    // afib path
    const hr = jit(rng, 146, 8);
    return {
      title: "Afib with RVR decompensates",
      blurb: `A ${pt} ${setting} in rapid afib at ${hr}. Rate control, hypotension, and the switch to electricity.`,
      stages: [
        {
          rhythm: "afib",
          vitals: { hr, spo2: 95, nibp: "116/72", rr: 18, etco2: null },
          scene: { cpr: false, pads: false, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
          narrative: `Your ${pt} ${setting} notices a "fluttering" heart. The monitor shows an irregularly irregular narrow-complex rhythm at ${hr} with no P waves.`,
          question: "What is the rhythm?",
          choices: ["Atrial fibrillation with rapid ventricular response", "Sinus tachycardia", "SVT", "Ventricular tachycardia"],
          answer: 0,
          rationale: "Irregularly irregular + no P waves + narrow QRS = afib; at this rate, 'with RVR.' SVT and sinus tach are regular; VT is wide.",
          outcome: "Still perfusing well — stable, for the moment.",
          intervention: "Afib with RVR identified",
        },
        {
          rhythm: "afib",
          vitals: { hr: hr - 2, spo2: 95, nibp: "112/70", rr: 18, etco2: null },
          scene: { cpr: false, pads: false, bvm: false, iv: true, meds: true, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
          narrative: "The provider wants rate control for this stable afib with RVR.",
          question: "Which order is appropriate?",
          choices: [
            "Diltiazem IV — about 0.25 mg/kg over 2 minutes, watching the blood pressure closely",
            "Adenosine 6 mg rapid push to convert it",
            "Amiodarone 300 mg IV push",
            "Epinephrine 1 mg IV",
          ],
          answer: 0,
          rationale:
            "Stable afib with RVR gets AV-nodal rate control — IV diltiazem or a beta blocker. Adenosine can't hold afib, and 300 mg amiodarone push is an arrest dose.",
          outcome: "Ten minutes in, the rate is better… then the BP reads 70/40. Pale, clammy, confused — still in afib.",
          intervention: "Diltiazem started — hypotension follows",
        },
        {
          rhythm: "afib",
          vitals: { hr: hr - 10, spo2: 93, nibp: "70/40", rr: 22, etco2: null },
          scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
          narrative: "Unstable now: hypotensive and altered, still in rapid afib. The diltiazem is stopped and pads are on.",
          question: "What is indicated now?",
          choices: [
            "Synchronized cardioversion — for afib, typically 120–200 J biphasic — with sedation if it won't delay the shock",
            "Another dose of diltiazem to finish the job",
            "Unsynchronized defibrillation at maximum energy",
            "A fluid bolus and 4 hours of observation",
          ],
          answer: 0,
          rationale:
            "Unstable tachyarrhythmia with a pulse = synchronized cardioversion; afib needs more energy than SVT (120–200 J biphasic). More AV-nodal blocker deepens the hypotension.",
          outcome: "One synchronized shock at 150 J — sinus rhythm appears and the pressure climbs.",
          intervention: "Synchronized cardioversion 150 J",
        },
        {
          rhythm: "nsr",
          vitals: { hr: 92, spo2: 96, nibp: "104/62", rr: 18, etco2: null },
          scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
          narrative: "Converted. A colleague asks: 'Duration was unknown — weren't we supposed to worry about a clot before cardioverting?'",
          question: "What's the correct teaching point?",
          choices: [
            "Instability overrides the anticoagulation rule — unstable patients are cardioverted immediately; with unknown duration, anticoagulation and stroke-risk evaluation follow the emergency",
            "The cardioversion was a mistake — a TEE was mandatory first",
            "Afib never requires anticoagulation considerations",
            "Clot risk only matters for atrial flutter",
          ],
          answer: 0,
          rationale:
            "Elective cardioversion of afib >48 h (or unknown) requires anticoagulation or a TEE first — but an UNSTABLE patient is cardioverted without delay, with anticoagulation addressed immediately after.",
          outcome: "Anticoagulation is started and a TEE scheduled.",
          intervention: "Anticoagulation initiated",
        },
        {
          rhythm: "nsr",
          vitals: { hr: 88, spo2: 97, nibp: "110/68", rr: 16, etco2: null },
          scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
          narrative: "Stable in sinus rhythm. Why did this happen at all?",
          question: "What completes the workup?",
          choices: [
            "Hunt the trigger: infection/sepsis, hypoxia, electrolytes (K+, Mg2+), thyroid function, ischemia — and keep the patient on telemetry for recurrence",
            "No workup — afib is always idiopathic",
            "Discharge now; the problem is fixed",
            "Daily prophylactic cardioversions",
          ],
          answer: 0,
          rationale:
            "New afib usually has a driver — infection, hypoxia, electrolytes, thyroid, ischemia. Treating the trigger matters as much as the rhythm, and recurrence is common enough to warrant telemetry.",
          outcome: "Magnesium comes back low and is replaced; the underlying illness gets treated. No recurrence overnight.",
          intervention: "Trigger workup · telemetry",
        },
      ],
    };
  }

  function arcTorsades(rng) {
    const pt = makePatient(rng);
    const drugPair = pick(rng, [
      "methadone and ondansetron",
      "haloperidol and levofloxacin",
      "amiodarone and fluconazole",
      "citalopram and azithromycin",
    ]);
    const hr = jit(rng, 214, 8);
    return {
      title: "Torsades on QT-prolonging drugs",
      blurb: `A ${pt} on ${drugPair} fires off torsades, then arrests. Magnesium, defibrillation, and cause-hunting.`,
      stages: [
        {
          rhythm: "torsades",
          vitals: { hr, spo2: 90, nibp: "82/48", rr: 22, etco2: null },
          scene: { cpr: false, pads: true, bvm: false, iv: true, meds: true, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
          narrative: `A ${pt} on ${drugPair} suddenly has runs of a wide-complex tachycardia that twists around the baseline. A weak pulse remains — for now.`,
          question: "Which medication is first-line for torsades de pointes?",
          choices: [
            "Magnesium sulfate 1–2 g IV",
            "Amiodarone 300 mg IV push",
            "Adenosine 6 mg rapid push",
            "Calcium chloride 1 g IV",
          ],
          answer: 0,
          rationale:
            "Torsades gets magnesium 1–2 g IV regardless of the serum level. Amiodarone prolongs QT and can make torsades worse — a dangerous reflex choice here.",
          outcome: "Mag is infusing… then the alarm tone changes. Unresponsive. No pulse.",
          intervention: "Magnesium 2 g IV started",
        },
        {
          rhythm: "torsades",
          vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: jit(rng, 14, 2) },
          scene: { cpr: true, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
          narrative: "Pulseless polymorphic VT. Compressions have started.",
          question: "Pulseless polymorphic VT is treated with…",
          choices: [
            "Immediate unsynchronized defibrillation at high energy, with ongoing CPR",
            "Synchronized cardioversion at 50 J",
            "Magnesium alone — no electricity needed",
            "Observation until the rhythm organizes",
          ],
          answer: 0,
          rationale:
            "Pulseless = arrest algorithm. Polymorphic VT is defibrillated — unsynchronized, since the machine can't reliably sync on a twisting QRS. The magnesium continues, but electricity comes first.",
          outcome: "One shock — the rhythm coarsens into VF. CPR continues without a break.",
          intervention: "Defibrillated 200 J",
        },
        {
          rhythm: "vf_coarse",
          vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: jit(rng, 14, 2) },
          scene: { cpr: true, pads: true, bvm: true, iv: true, meds: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
          narrative: "Now in VF, two minutes into the cycle with an IV running.",
          question: "Alongside continued shocks and CPR, what drug therapy is correct?",
          choices: [
            "Epinephrine 1 mg IV every 3–5 minutes, and complete the magnesium — avoiding QT-prolonging antiarrhythmics",
            "Stop the magnesium and give amiodarone 300 mg — it's always first",
            "Atropine 1 mg every 3 minutes",
            "No drugs — shocks only",
          ],
          answer: 0,
          rationale:
            "The arrest gets standard epinephrine dosing, and the magnesium is completed. Amiodarone prolongs QT further — lidocaine is the safer antiarrhythmic in torsades-driven arrest if one is needed.",
          outcome: "Epi in, mag complete, another shock… the next check shows sinus rhythm with a pulse. ROSC.",
          intervention: "Epi 1 mg · magnesium completed",
        },
        {
          rhythm: "nsr",
          vitals: { hr: jit(rng, 96, 5), spo2: 94, nibp: "92/58", rr: 0, etco2: jit(rng, 36, 2) },
          scene: { cpr: false, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "WEAK", breathing: "ASSISTED" },
          narrative: "ROSC. The team debriefs on why this happened.",
          question: "Which combination most likely set up this torsades arrest?",
          choices: [
            `QT-prolonging drugs (${drugPair}) on top of low potassium and magnesium`,
            "Too much IV fluid",
            "Beta blocker use",
            "An elevated troponin alone",
          ],
          answer: 0,
          rationale:
            "Torsades is a prolonged-QT arrhythmia: QT-prolonging drug combinations plus hypokalemia/hypomagnesemia are the classic setup. Replacement to high-normal potassium is part of the fix.",
          outcome: "The potassium returns at 2.8 — replacement starts and both offending drugs are flagged.",
          intervention: "K+ replacement · QT drugs flagged",
        },
        {
          rhythm: "nsr",
          vitals: { hr: 90, spo2: 96, nibp: "104/62", rr: 0, etco2: null },
          scene: { cpr: false, pads: true, bvm: true, iv: true, meds: false, loc: "ALTERED", pulse: "PRESENT", breathing: "ASSISTED" },
          narrative: "Stabilizing. The provider writes prevention orders.",
          question: "Which order set correctly prevents recurrent torsades?",
          choices: [
            "Discontinue all QT-prolonging medications, keep K+ ~4.5–5 and Mg replaced, continuous QT monitoring; overdrive pacing or isoproterenol if runs recur",
            "Start an amiodarone maintenance infusion",
            "Haloperidol PRN for agitation",
            "No specific precautions are required after ROSC",
          ],
          answer: 0,
          rationale:
            "Prevention: remove every QT-prolonging drug (haloperidol is one), optimize potassium and magnesium, monitor the QT. Refractory recurrent torsades responds to raising the heart rate — overdrive pacing or isoproterenol shortens the QT.",
          outcome: "No further runs overnight. A QT-interaction flag goes on the chart.",
          intervention: "QT precautions ordered",
        },
      ],
    };
  }

  function arcStableVt(rng) {
    const pt = makePatient(rng, "older");
    const hr = jit(rng, 174, 6);
    return {
      title: "STEMI into VT",
      blurb: `A ${pt} with chest pain declares a STEMI, then the ventricle starts firing at ${hr}.`,
      stages: [
        {
          rhythm: "sinus_tach",
          vitals: { hr: jit(rng, 116, 5), spo2: 95, nibp: "142/88", rr: 20, etco2: null },
          scene: { cpr: false, pads: false, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
          narrative: `A ${pt} develops crushing substernal chest pain with diaphoresis and nausea. The 12-lead shows ST elevation in II, III, and aVF.`,
          question: "What happens now?",
          choices: [
            "Aspirin 162–325 mg chewed and activate the cath lab — an inferior STEMI; check right-sided leads and the BP before any nitroglycerin",
            "Serial troponins over 12 hours before deciding",
            "Nitroglycerin immediately, no other assessment",
            "A GI cocktail to rule out reflux first",
          ],
          answer: 0,
          rationale:
            "ST elevation in II, III, aVF is an inferior STEMI — chewed aspirin and immediate reperfusion are the priorities. Inferior MIs often involve the right ventricle, where nitroglycerin can crash the pressure.",
          outcome: "Cath lab activated. Then the alarm fires: the rhythm is now wide and regular — but the patient is still talking.",
          intervention: "ASA · cath lab activated",
        },
        {
          rhythm: "vt",
          vitals: { hr, spo2: 93, nibp: "96/60", rr: 20, etco2: null },
          scene: { cpr: false, pads: true, bvm: false, iv: true, meds: true, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
          narrative: `Monomorphic VT at ${hr} — with a pulse, awake, BP 96/60. Pads are on as a precaution.`,
          question: "How is stable VT with a pulse treated?",
          choices: [
            "Amiodarone 150 mg IV over 10 minutes (not the 300 mg arrest push), with cardioversion ready if the patient deteriorates",
            "Amiodarone 300 mg IV push — same as the arrest dose",
            "Immediate unsynchronized defibrillation",
            "Adenosine is first-line for all wide rhythms",
          ],
          answer: 0,
          rationale:
            "Stable VT with a pulse is treated pharmacologically first: amiodarone 150 mg over 10 minutes (or procainamide/sotalol). The 300 mg push is for pulseless arrest. Stay ready for electricity.",
          outcome: "The infusion is running… then the patient's eyes roll back. Still VT on the screen — but no pulse.",
          intervention: "Amiodarone 150 mg infusion",
        },
        {
          rhythm: "vt",
          vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: jit(rng, 15, 2) },
          scene: { cpr: true, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
          narrative: "Pulseless VT. Compressions have started; the defibrillator is charging.",
          question: "Pulseless VT is managed with…",
          choices: [
            "Immediate unsynchronized defibrillation (~200 J biphasic) with high-quality CPR between shocks — exactly like VF",
            "Synchronized cardioversion at 100 J",
            "Finishing the amiodarone infusion before shocking",
            "Vagal maneuvers",
          ],
          answer: 0,
          rationale:
            "The moment VT loses the pulse it becomes a shockable arrest rhythm — same algorithm as VF: unsynchronized shocks, 2-minute cycles, epinephrine, antiarrhythmic per the arrest pathway.",
          outcome: "One shock — and the next check finds sinus rhythm with a pulse. ROSC.",
          intervention: "Defibrillated 200 J → ROSC",
        },
        stPostRosc(
          { rng },
          "Go to the cath lab emergently — reperfusion treats the cause of the arrest, with post-ROSC care en route",
          "A STEMI with arrest and ROSC goes to the cath lab emergently: the occluded artery caused the VT, and reperfusion is the definitive treatment. The post-ROSC bundle travels with the patient.",
          "Wheeled to the lab within minutes — the culprit artery is opened and stented."
        ),
        {
          rhythm: "nsr",
          vitals: { hr: 84, spo2: 97, nibp: "112/70", rr: 14, etco2: null },
          scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
          narrative: "Post-PCI and awake. The monitor shows occasional short runs of a slow, wide rhythm around 70 that come and go.",
          question: "How do you interpret these runs after reperfusion?",
          choices: [
            "Likely accelerated idioventricular rhythm (AIVR) — a common, usually benign reperfusion rhythm; observe, don't suppress, but stay alert for true VT",
            "Recurrent VT — defibrillate immediately",
            "Artifact — ignore all wide rhythms now",
            "Complete heart block — start pacing",
          ],
          answer: 0,
          rationale:
            "AIVR — a wide rhythm at roughly 40–120 after reperfusion — is a classic, usually benign reperfusion arrhythmia that resolves on its own. The job is telling it apart from fast, sustained VT.",
          outcome: "The runs fade over the next hour. Case closed.",
          intervention: "AIVR observed · resolved",
        },
      ],
    };
  }

  // ---------- arc registry + adaptive selection ----------

  const ARCS = [
    { key: "shockable", focus: "shockable-arrest", categories: ["code"], build: arcShockable },
    { key: "pea", focus: "nonshockable-arrest", categories: ["code", "condition"], build: (rng) => arcNonShockable(rng, "pea") },
    { key: "asystole", focus: "nonshockable-arrest", categories: ["code", "condition"], build: (rng) => arcNonShockable(rng, "asystole") },
    { key: "brady", focus: "bradycardia", categories: ["meds", "rhythm"], build: arcBradycardia },
    { key: "tachy", focus: "tachycardia", categories: ["meds", "rhythm"], build: arcTachy },
    { key: "torsades", focus: "torsades-qt", categories: ["meds"], build: arcTorsades },
    { key: "stablevt", focus: "stable-vt-acs", categories: ["condition", "code"], build: arcStableVt },
  ];

  function arcScores() {
    const p = window.EkgStats ? EkgStats.profile() : { byFocus: {}, byCategory: {} };
    return ARCS.map((arc) => {
      let score = 0.5;
      const f = p.byFocus[arc.focus];
      if (f && f.total > 0) {
        const evidence = Math.min(1, f.total / 6);
        score = 0.5 * (1 - evidence) + (1 - f.accuracy) * evidence;
      }
      // blend in quiz-category performance for the arc's related categories
      let catMiss = 0;
      let catN = 0;
      for (const c of arc.categories) {
        const b = p.byCategory[c];
        if (b && b.total >= 3) {
          catMiss += 1 - b.accuracy;
          catN++;
        }
      }
      if (catN) score = 0.6 * score + 0.4 * (catMiss / catN);
      return { arc, score };
    });
  }

  function pickArc(rng) {
    let scores = arcScores();
    let lastArc = null;
    try {
      lastArc = localStorage.getItem(LAST_ARC_KEY);
    } catch {}
    const filtered = scores.filter((s) => s.arc.key !== lastArc);
    if (filtered.length) scores = filtered;
    // softmax-ish weighted pick with exploration
    const weights = scores.map((s) => 0.15 + s.score * s.score);
    const total = weights.reduce((a, b) => a + b, 0);
    let r = rng() * total;
    for (let i = 0; i < scores.length; i++) {
      r -= weights[i];
      if (r <= 0) return scores[i];
    }
    return scores[scores.length - 1];
  }

  function focusReason(picked) {
    const p = window.EkgStats ? EkgStats.profile() : { total: 0 };
    if (p.total < 5) return "Exploring the case library — answer more questions and generation will target your weak areas.";
    const f = p.byFocus[picked.arc.focus];
    if (f && f.total > 0 && f.accuracy < 0.8) {
      return `Targeting a weak area: you've answered ${Math.round(f.accuracy * 100)}% correctly in ${picked.arc.focus.replace(/-/g, " ")} cases.`;
    }
    return "Chosen from your performance profile, with some variety mixed in.";
  }

  // ---------- generation + library ----------

  function loadLibrary() {
    try {
      const raw = JSON.parse(localStorage.getItem(GEN_KEY));
      if (Array.isArray(raw)) return raw;
    } catch {}
    return [];
  }

  function saveLibrary(list) {
    try {
      localStorage.setItem(GEN_KEY, JSON.stringify(list.slice(0, GEN_CAP)));
    } catch {}
  }

  function generate() {
    const seed = (Date.now() ^ (Math.random() * 0xffffffff)) >>> 0;
    const rng = mulberry32(seed);
    const picked = pickArc(rng);
    try {
      localStorage.setItem(LAST_ARC_KEY, picked.arc.key);
    } catch {}
    const body = picked.arc.build(rng);
    const shortId = seed.toString(36).slice(-4).toUpperCase();
    const scenario = {
      id: `gen-${seed.toString(36)}`,
      generated: true,
      arc: picked.arc.key,
      focus: picked.arc.focus,
      seed,
      createdAt: Date.now(),
      title: `${body.title} · #${shortId}`,
      blurb: body.blurb,
      reason: focusReason(picked),
      stages: body.stages,
    };
    const lib = loadLibrary();
    lib.unshift(scenario);
    saveLibrary(lib);
    return scenario;
  }

  function listGenerated() {
    return loadLibrary();
  }

  function removeGenerated(id) {
    saveLibrary(loadLibrary().filter((s) => s.id !== id));
  }

  window.EkgGenerator = { generate, listGenerated, removeGenerated };
})();
