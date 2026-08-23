/*
 * Multi-stage code scenarios ("megacodes") for the Zoll trainer.
 * Each stage: the monitor rhythm at that moment, the patient's state for
 * the code scene (CPR in progress, pads on, pulse/LOC/breathing), a
 * narrative bridge, one decision question, and the intervention that gets
 * logged when the stage resolves. Educational use only.
 *
 * scene keys:
 *   cpr        — compressions in progress (animates the compressor)
 *   pads       — defib pads applied to the chest
 *   bvm        — bag-valve-mask ventilation at the head
 *   loc        — "ALERT" | "ALTERED" | "UNRESPONSIVE"
 *   pulse      — "PRESENT" | "WEAK" | "ABSENT"
 *   breathing  — "SPONTANEOUS" | "ASSISTED" | "NONE"
 */

const EKG_SCENARIOS = [
  {
    id: "vf-arrest",
    title: "Witnessed VF arrest",
    blurb: "A telemetry patient collapses in front of you. Run the full shockable-rhythm algorithm from first compression to ROSC.",
    stages: [
      {
        rhythm: "vf_coarse",
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
        scene: { cpr: false, pads: false, bvm: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "NONE" },
        narrative:
          "You're at the bedside when your 61-year-old telemetry patient suddenly slumps over. He is unresponsive with no pulse and no breathing. The monitor shows the rhythm above.",
        question: "What is your immediate first action?",
        choices: [
          "Start high-quality chest compressions and call a code — get the defibrillator to the bedside",
          "Give amiodarone 300 mg IV push first",
          "Perform a 12-lead EKG to confirm the rhythm",
          "Give 2 rescue breaths and wait to see if he responds",
        ],
        answer: 0,
        rationale:
          "Unresponsive + pulseless = compressions now. Nothing — not drugs, not airway, not a 12-lead — comes before starting CPR and getting the defibrillator for a shockable rhythm.",
        outcome: "The code team arrives. Compressions are in progress and pads go on the chest. The rhythm is still coarse VF.",
        intervention: "CPR started · code called",
      },
      {
        rhythm: "vf_coarse",
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
        scene: { cpr: true, pads: true, bvm: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative:
          "Pads are on and the defibrillator is charged. The team pauses briefly: the monitor still shows coarse VF.",
        question: "What do you do now?",
        choices: [
          "Deliver one unsynchronized shock (biphasic ~200 J per device), then immediately resume compressions for 2 minutes",
          "Deliver three stacked shocks in a row before resuming CPR",
          "Use synchronized cardioversion at 100 J",
          "Hold the shock and give epinephrine first",
        ],
        answer: 0,
        rationale:
          "VF gets a single unsynchronized shock at the device's recommended biphasic energy, followed by immediate resumption of compressions for a full 2-minute cycle — no post-shock pulse check, no stacked shocks.",
        outcome: "Shock delivered. Compressions resume instantly. At the next 2-minute rhythm check: still VF.",
        intervention: "Defibrillated 200 J (1st shock)",
      },
      {
        rhythm: "vf_coarse",
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
        scene: { cpr: true, pads: true, bvm: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative:
          "A second shock is delivered and CPR continues. IV access is established. VF persists on the monitor.",
        question: "Which medication comes first, and how is it dosed?",
        choices: [
          "Epinephrine 1 mg IV/IO, repeated every 3–5 minutes",
          "Amiodarone 150 mg slow infusion over 10 minutes",
          "Atropine 1 mg IV push",
          "Adenosine 6 mg rapid IV push",
        ],
        answer: 0,
        rationale:
          "In a shockable arrest, epinephrine 1 mg IV/IO (repeated every 3–5 minutes) is the first drug, typically after the second shock. Amiodarone comes later for refractory VF — and at 300 mg push, not the 150 mg infusion used for stable VT.",
        outcome: "Epi is in. Another cycle of CPR, a third shock — the monitor still shows VF. This is now refractory VF.",
        intervention: "Epinephrine 1 mg IV",
      },
      {
        rhythm: "vf_coarse",
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
        scene: { cpr: true, pads: true, bvm: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative: "VF has survived three shocks and a round of epinephrine. The team leader asks for an antiarrhythmic.",
        question: "What do you draw up?",
        choices: [
          "Amiodarone 300 mg IV/IO push",
          "Amiodarone 150 mg over 10 minutes",
          "Diltiazem 20 mg IV",
          "Magnesium 2 g IV as first-line for all VF",
        ],
        answer: 0,
        rationale:
          "Refractory VF/pVT gets amiodarone 300 mg IV/IO push (with a possible one-time repeat of 150 mg). Lidocaine 1–1.5 mg/kg is the alternative. Magnesium is reserved for torsades, and diltiazem has no role in arrest.",
        outcome: "Amiodarone in, fourth shock delivered, CPR resumes… at the next check the monitor shows an organized rhythm — and there's a carotid pulse. ROSC!",
        intervention: "Amiodarone 300 mg IV · 4th shock",
      },
      {
        rhythm: "nsr",
        vitals: { hr: 92, spo2: 93, nibp: "86/52", rr: 0 },
        scene: { cpr: false, pads: true, bvm: true, loc: "UNRESPONSIVE", pulse: "WEAK", breathing: "ASSISTED" },
        narrative:
          "ROSC achieved: sinus rhythm at 92 with a weak pulse, BP 86/52. He remains unresponsive and is being ventilated.",
        question: "What are the immediate post-ROSC priorities?",
        choices: [
          "Titrate oxygen (SpO2 92–98%), support BP above SBP 90 with fluids/pressors, get a 12-lead EKG, and consider targeted temperature management",
          "Stop all monitoring — the code is over",
          "Give another epinephrine 1 mg bolus to raise the BP",
          "Extubate now that a pulse is back",
        ],
        answer: 0,
        rationale:
          "Post-cardiac-arrest care: avoid both hypoxia and hyperoxia (SpO2 92–98%), keep SBP > 90 (fluids, then an infusion — not another code-dose epi bolus), obtain a 12-lead to look for STEMI, and consider TTM for the comatose patient. Anticipate re-arrest.",
        outcome: "He's on an epinephrine infusion, the 12-lead shows an inferior STEMI, and cath lab is activated. Well run.",
        intervention: "Post-ROSC bundle · cath lab activated",
      },
    ],
  },

  {
    id: "unstable-brady",
    title: "Bradycardia that keeps falling",
    blurb: "A symptomatic bradycardia stops responding to first-line therapy and degenerates into complete heart block.",
    stages: [
      {
        rhythm: "sinus_brady",
        vitals: { hr: 38, spo2: 93, nibp: "82/50", rr: 16 },
        scene: { cpr: false, pads: false, bvm: false, loc: "ALTERED", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative:
          "Your 74-year-old patient becomes pale, diaphoretic, and confused. The monitor shows the rhythm above at 38, BP 82/50.",
        question: "This bradycardia is symptomatic. What is the first-line treatment?",
        choices: [
          "Atropine 1 mg IV push, repeatable every 3–5 minutes to a total of 3 mg",
          "Adenosine 6 mg rapid IV push",
          "Immediate defibrillation",
          "Amiodarone 300 mg IV push",
        ],
        answer: 0,
        rationale:
          "Symptomatic bradycardia with a pulse gets atropine 1 mg IV first (max 3 mg total). Defibrillation and adenosine treat entirely different problems.",
        outcome: "Two doses of atropine produce no change. He's now barely responsive… and the rhythm on the monitor has changed.",
        intervention: "Atropine 1 mg IV ×2 — no effect",
      },
      {
        rhythm: "avb3",
        vitals: { hr: 32, spo2: 90, nibp: "74/42", rr: 14 },
        scene: { cpr: false, pads: true, bvm: false, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
        narrative:
          "The rhythm is now regular P waves and slow, wide QRS complexes marching independently of each other — complete heart block at 32.",
        question: "Atropine has failed and he's deteriorating. What's next?",
        choices: [
          "Begin transcutaneous pacing without delay",
          "Give more atropine beyond 3 mg total",
          "Synchronized cardioversion at 100 J",
          "Observe for 30 minutes and recheck",
        ],
        answer: 0,
        rationale:
          "Atropine-refractory, unstable bradycardia — especially high-degree AV block — needs immediate transcutaneous pacing (a chronotropic infusion is the alternative or bridge). Atropine also tends to be ineffective in infranodal block.",
        outcome: "Pacing starts at 70/min. But looking at the monitor, the pacer spikes aren't followed by QRS complexes.",
        intervention: "Transcutaneous pacing started",
      },
      {
        rhythm: "avb3",
        vitals: { hr: 32, spo2: 90, nibp: "72/40", rr: 14 },
        scene: { cpr: false, pads: true, bvm: false, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
        narrative: "Spikes march across the screen without capturing a QRS. His pressure is still 72/40.",
        question: "What is the problem and the fix?",
        choices: [
          "Failure to capture — increase the output (mA) until every spike is followed by a QRS, then confirm a matching femoral pulse",
          "Failure to sense — turn the rate down",
          "Normal early pacing behavior — wait 10 minutes",
          "The pads are fine; give more atropine instead",
        ],
        answer: 0,
        rationale:
          "Spikes without QRS complexes = failure to capture. Turn the current up until electrical capture appears, then always confirm mechanical capture with a pulse. Sedate/analgese as tolerated — pacing hurts.",
        outcome: "At a higher output you get consistent capture with a matching pulse at 70. BP creeps to 88/56, but he's still marginal.",
        intervention: "Output ↑ until capture confirmed",
      },
      {
        rhythm: "junctional",
        vitals: { hr: 70, spo2: 95, nibp: "88/56", rr: 16 },
        scene: { cpr: false, pads: true, bvm: false, loc: "ALTERED", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "Paced at 70 with capture, he's perfusing but still hypotensive and pacing-dependent.",
        question: "Which infusion supports him while definitive care is arranged?",
        choices: [
          "Dopamine 5–20 mcg/kg/min or epinephrine 2–10 mcg/min",
          "Diltiazem 10 mg/hr",
          "Nitroglycerin 20 mcg/min",
          "Metoprolol 5 mg IV",
        ],
        answer: 0,
        rationale:
          "The bradycardia algorithm's infusions are dopamine 5–20 mcg/kg/min or epinephrine 2–10 mcg/min. Diltiazem, metoprolol, and nitroglycerin would all worsen the rate or the pressure.",
        outcome: "On dopamine his BP holds at 102/64. Cardiology is on the way.",
        intervention: "Dopamine infusion titrated",
      },
      {
        rhythm: "junctional",
        vitals: { hr: 70, spo2: 96, nibp: "102/64", rr: 16 },
        scene: { cpr: false, pads: true, bvm: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "He's awake again, paced and stable on the infusion. Cardiology asks what you'd anticipate next.",
        question: "What is the definitive plan for atropine-refractory complete heart block?",
        choices: [
          "Transvenous pacing as a bridge, then evaluation for a permanent pacemaker",
          "Discharge home with a Holter monitor",
          "Long-term dopamine infusion at home",
          "Elective cardioversion next week",
        ],
        answer: 0,
        rationale:
          "Transcutaneous pacing is only a bridge — it's painful and unreliable over time. The pathway is transvenous pacing, then permanent pacemaker evaluation. Meanwhile: continuous monitoring, pads on, and stop any AV-nodal-blocking home meds.",
        outcome: "He goes to the unit with a transvenous wire planned. Textbook escalation.",
        intervention: "Transvenous pacing arranged",
      },
    ],
  },

  {
    id: "svt-crash",
    title: "SVT that turns unstable",
    blurb: "A stable narrow-complex tachycardia stops being stable halfway through your treatment.",
    stages: [
      {
        rhythm: "svt",
        vitals: { hr: 189, spo2: 97, nibp: "112/70", rr: 18 },
        scene: { cpr: false, pads: false, bvm: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative:
          "A 34-year-old reports sudden palpitations. She's alert, warm, BP 112/70. The monitor shows a regular narrow-complex tachycardia at 189 with no visible P waves.",
        question: "She is stable. What do you try first?",
        choices: [
          "Vagal maneuvers (e.g., modified Valsalva)",
          "Synchronized cardioversion at 50 J",
          "Amiodarone 300 mg IV push",
          "Defibrillation at 200 J",
        ],
        answer: 0,
        rationale:
          "Stable SVT starts with vagal maneuvers — a properly performed (modified) Valsalva converts a meaningful fraction of SVT and costs nothing. Electricity is for unstable patients.",
        outcome: "Two attempts at a modified Valsalva don't break it. She remains at 189, still stable — for now.",
        intervention: "Vagal maneuvers attempted",
      },
      {
        rhythm: "svt",
        vitals: { hr: 189, spo2: 97, nibp: "108/68", rr: 18 },
        scene: { cpr: false, pads: false, bvm: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "Vagal maneuvers failed. An IV is in place in the right AC.",
        question: "What's the correct next step?",
        choices: [
          "Adenosine 6 mg rapid IV push through the closest large vein, followed immediately by a 20 mL saline flush; warn her about the awful transient feeling",
          "Adenosine 6 mg as a slow infusion over 5 minutes",
          "Diltiazem 20 mg IV push over 10 seconds",
          "Epinephrine 1 mg IV",
        ],
        answer: 0,
        rationale:
          "Adenosine must be slammed — its half-life is seconds. 6 mg rapid push + immediate flush, ideally with the arm raised. Warn the patient about the brief chest pressure and 'sense of doom'; it passes in seconds.",
        outcome: "A few seconds of pause… then the SVT resumes at 190. And now she's gray, diaphoretic, and confused — BP 76/40.",
        intervention: "Adenosine 6 mg — transient effect",
      },
      {
        rhythm: "svt",
        vitals: { hr: 190, spo2: 94, nibp: "76/40", rr: 22 },
        scene: { cpr: false, pads: true, bvm: false, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
        narrative:
          "She has become unstable: hypotensive, altered, poorly perfused — still in SVT at 190. Pads are going on.",
        question: "What is indicated NOW?",
        choices: [
          "Synchronized cardioversion (sedate first if it won't delay the shock)",
          "Adenosine 12 mg and reassess in 10 minutes",
          "Unsynchronized defibrillation",
          "Oral metoprolol",
        ],
        answer: 0,
        rationale:
          "Instability (hypotension, altered mental status, ischemic chest pain, shock) ends the medication pathway: unstable tachyarrhythmia with a pulse gets synchronized cardioversion. Sedation is given when it won't delay treatment. Unsynchronized shock risks R-on-T.",
        outcome: "You charge the defibrillator in sync mode — the sync markers align on each R wave.",
        intervention: "Prepared synchronized cardioversion",
      },
      {
        rhythm: "svt",
        vitals: { hr: 190, spo2: 94, nibp: "76/40", rr: 22 },
        scene: { cpr: false, pads: true, bvm: false, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
        narrative: "The defibrillator is in SYNC mode and everyone is clear.",
        question: "What initial energy is appropriate for cardioverting a regular narrow-complex tachycardia?",
        choices: [
          "50–100 J synchronized",
          "200 J unsynchronized",
          "360 J synchronized",
          "5 J synchronized",
        ],
        answer: 0,
        rationale:
          "Regular narrow-complex SVT converts at low energy: 50–100 J synchronized initially, escalating if needed. (Afib typically needs 120–200 J biphasic; the shock stays synchronized as long as there's an R wave to sync on.)",
        outcome: "One synchronized shock at 75 J — the monitor blinks, and a sinus rhythm at 88 marches out. Color returns to her face.",
        intervention: "Synchronized cardioversion 75 J",
      },
      {
        rhythm: "nsr",
        vitals: { hr: 88, spo2: 98, nibp: "108/66", rr: 16 },
        scene: { cpr: false, pads: true, bvm: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "She's back in sinus rhythm at 88, awake and embarrassed to have caused a fuss.",
        question: "What completes your post-conversion care?",
        choices: [
          "Keep her on the monitor with pads in place, obtain a 12-lead EKG, check electrolytes, and arrange cardiology follow-up for the recurrent SVT",
          "Remove the monitor — the event is over",
          "Give amiodarone 300 mg to prevent recurrence",
          "Discharge immediately with no follow-up",
        ],
        answer: 0,
        rationale:
          "After conversion: continuous monitoring (recurrence is common), a 12-lead to look for pre-excitation (WPW) or ischemia, electrolytes, and referral — recurrent SVT may warrant an EP study/ablation.",
        outcome: "Her 12-lead shows a short PR with a delta wave — WPW. Good thing it's documented; EP consult placed.",
        intervention: "12-lead → WPW identified · EP consult",
      },
    ],
  },

  {
    id: "torsades-arrest",
    title: "Torsades loses the pulse",
    blurb: "A prolonged-QT patient fires off torsades, then arrests. Magnesium, defibrillation, and cause-hunting.",
    stages: [
      {
        rhythm: "torsades",
        vitals: { hr: 214, spo2: 90, nibp: "82/48", rr: 22 },
        scene: { cpr: false, pads: true, bvm: false, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
        narrative:
          "A patient on methadone and ondansetron suddenly has runs of a wide-complex tachycardia that twists around the baseline. She still has a weak pulse but is fading.",
        question: "Which medication is first-line for torsades de pointes?",
        choices: [
          "Magnesium sulfate 1–2 g IV",
          "Amiodarone 300 mg IV push",
          "Adenosine 6 mg rapid push",
          "Calcium chloride 1 g IV",
        ],
        answer: 0,
        rationale:
          "Torsades gets magnesium 1–2 g IV regardless of the serum level. Notably, amiodarone prolongs QT and can make torsades worse — a dangerous reflex choice here.",
        outcome: "Mag is infusing… then the monitor alarm changes tone. She's unresponsive. No pulse.",
        intervention: "Magnesium 2 g IV started",
      },
      {
        rhythm: "torsades",
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
        scene: { cpr: true, pads: true, bvm: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative: "She is now pulseless with polymorphic VT on the monitor. Compressions have started.",
        question: "Pulseless polymorphic VT is treated with…",
        choices: [
          "Immediate unsynchronized defibrillation at high energy, with ongoing CPR",
          "Synchronized cardioversion at 50 J",
          "Magnesium alone — no electricity needed",
          "Observation until the rhythm organizes",
        ],
        answer: 0,
        rationale:
          "Pulseless = arrest algorithm. Polymorphic VT is defibrillated (unsynchronized — the machine can't reliably sync on a twisting QRS). Magnesium continues, but electricity comes first.",
        outcome: "One shock — the rhythm coarsens into VF. CPR continues without a break.",
        intervention: "Defibrillated 200 J",
      },
      {
        rhythm: "vf_coarse",
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
        scene: { cpr: true, pads: true, bvm: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative: "Now in VF. The team is two minutes into the cycle with an IV running.",
        question: "Alongside continued shocks and CPR, what drug therapy is correct?",
        choices: [
          "Epinephrine 1 mg IV every 3–5 minutes, and complete the magnesium — avoid QT-prolonging antiarrhythmics",
          "Stop the magnesium and give amiodarone 300 mg — it's always first",
          "Atropine 1 mg every 3 minutes",
          "No drugs — shocks only",
        ],
        answer: 0,
        rationale:
          "The arrest gets standard epinephrine dosing. In torsades-driven arrest, finish the magnesium; amiodarone is problematic because it prolongs QT further — lidocaine is the safer antiarrhythmic if one is needed.",
        outcome: "Epi in, mag complete, another shock… the next rhythm check shows sinus rhythm with a pulse. ROSC.",
        intervention: "Epinephrine 1 mg · magnesium completed",
      },
      {
        rhythm: "nsr",
        vitals: { hr: 96, spo2: 94, nibp: "92/58", rr: 0 },
        scene: { cpr: false, pads: true, bvm: true, loc: "UNRESPONSIVE", pulse: "WEAK", breathing: "ASSISTED" },
        narrative: "ROSC. She's ventilated, BP 92/58 on a norepinephrine infusion. The team debriefs on why this happened.",
        question: "Which combination most likely set up this torsades arrest?",
        choices: [
          "QT-prolonging drugs (methadone + ondansetron) on top of low potassium and magnesium",
          "Too much IV fluid",
          "Beta blocker use",
          "Elevated troponin alone",
        ],
        answer: 0,
        rationale:
          "Torsades is a prolonged-QT arrhythmia: QT-prolonging drug combinations plus hypokalemia/hypomagnesemia are the classic setup. Her K+ returns at 2.9 — replacement to high-normal is part of the fix.",
        outcome: "Potassium replacement is started, and both offending medications are flagged.",
        intervention: "K+ replacement · QT drugs identified",
      },
      {
        rhythm: "nsr",
        vitals: { hr: 90, spo2: 96, nibp: "104/62", rr: 0 },
        scene: { cpr: false, pads: true, bvm: true, loc: "ALTERED", pulse: "PRESENT", breathing: "ASSISTED" },
        narrative: "She's stabilizing. The provider is writing orders to prevent a recurrence.",
        question: "Which order set correctly prevents recurrent torsades?",
        choices: [
          "Discontinue all QT-prolonging medications, keep K+ ~4.5–5 and Mg replaced, continuous QT monitoring; overdrive pacing or isoproterenol if runs recur",
          "Start amiodarone maintenance infusion",
          "Give haloperidol for agitation as needed",
          "No specific precautions are required after ROSC",
        ],
        answer: 0,
        rationale:
          "Prevention: remove every QT-prolonging drug (note: haloperidol is one), optimize potassium and magnesium, and monitor the QT. Refractory recurrent torsades responds to increasing the heart rate — overdrive pacing or isoproterenol shortens the QT.",
        outcome: "No further runs overnight. The pharmacy adds a QT-drug interaction flag to her chart.",
        intervention: "QT precautions ordered",
      },
    ],
  },

  {
    id: "pea-cause",
    title: "PEA — find the cause",
    blurb: "An organized rhythm with no pulse. The monitor lies; the H's and T's tell the truth.",
    stages: [
      {
        rhythm: "nsr",
        vitals: { hr: 84, spo2: null, nibp: "--/--", rr: 0 },
        scene: { cpr: false, pads: false, bvm: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "NONE" },
        narrative:
          "A post-op patient (day 1 after abdominal surgery) is found unresponsive. The monitor shows an organized narrow-complex rhythm at 84 — but there is no carotid pulse and no breathing.",
        question: "What is this, and what do you do first?",
        choices: [
          "Pulseless electrical activity — start CPR immediately; the organized tracing does not mean perfusion",
          "A perfusing sinus rhythm — recheck vitals in 15 minutes",
          "Shockable arrest — defibrillate at 200 J",
          "Lead artifact — reposition the electrodes first",
        ],
        answer: 0,
        rationale:
          "Electrical activity without a pulse is PEA. It is treated as a full arrest — immediate compressions — and it is NOT shockable. Never trust an organized tracing over your pulse check.",
        outcome: "CPR starts, the code is called, and pads go on (in case the rhythm changes).",
        intervention: "PEA recognized · CPR started",
      },
      {
        rhythm: "nsr",
        vitals: { hr: 84, spo2: null, nibp: "--/--", rr: 0 },
        scene: { cpr: true, pads: true, bvm: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative: "Compressions are in progress and an IV is available.",
        question: "What is the first-line drug in PEA, and when?",
        choices: [
          "Epinephrine 1 mg IV/IO as soon as possible, repeated every 3–5 minutes",
          "Amiodarone 300 mg IV push",
          "Atropine 1 mg IV — PEA is a slow rhythm problem",
          "Defibrillation replaces drugs in PEA",
        ],
        answer: 0,
        rationale:
          "In non-shockable arrest (PEA/asystole), early epinephrine matters — 1 mg IV/IO as soon as access is available, then every 3–5 minutes. Atropine is no longer in the arrest algorithm and antiarrhythmics have no role in PEA.",
        outcome: "Epi is in. The team leader calls out: 'Somebody run the H's and T's — why is this person in PEA?'",
        intervention: "Epinephrine 1 mg IV",
      },
      {
        rhythm: "sinus_tach",
        vitals: { hr: 128, spo2: null, nibp: "--/--", rr: 0 },
        scene: { cpr: true, pads: true, bvm: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative:
          "Clues assemble: post-op abdomen day 1, drains with fresh blood, pale and cool, flat neck veins — and the underlying rhythm has sped up to a narrow tachycardia.",
        question: "Which reversible cause fits best, and what's the treatment?",
        choices: [
          "Hypovolemia from hemorrhage — rapid volume/blood resuscitation while CPR continues, and call surgery",
          "Tension pneumothorax — needle decompression",
          "Hypothermia — active rewarming",
          "Toxins — give naloxone",
        ],
        answer: 0,
        rationale:
          "A narrow-complex, fast PEA in a post-op patient with bloody drains, pallor, and flat neck veins screams hemorrhagic hypovolemia. Treatment is volume — crystalloid then blood — plus source control. (Distended neck veins with tracheal deviation would suggest tension pneumo instead.)",
        outcome: "Two large-bore IVs run wide open and uncrossmatched blood is hung. After the next cycle: a femoral pulse with the tachycardia. ROSC.",
        intervention: "Fluids + emergency blood · surgery paged",
      },
      {
        rhythm: "sinus_tach",
        vitals: { hr: 124, spo2: 92, nibp: "78/44", rr: 0 },
        scene: { cpr: false, pads: true, bvm: true, loc: "UNRESPONSIVE", pulse: "WEAK", breathing: "ASSISTED" },
        narrative: "ROSC with a thready pulse at 124 and BP 78/44. He's still bleeding somewhere.",
        question: "What is the priority now?",
        choices: [
          "Continue blood resuscitation and get him to definitive hemorrhage control (the OR) — the arrest will recur if the cause isn't fixed",
          "Targeted temperature management before anything else",
          "Extubate and observe",
          "Give furosemide for the tachycardia",
        ],
        answer: 0,
        rationale:
          "PEA from hypovolemia re-arrests unless the cause is fixed: keep transfusing and move to definitive surgical control. Other post-ROSC elements matter, but hemorrhage control is THE priority in this patient.",
        outcome: "He's in the OR within 20 minutes — a bleeding vessel at the surgical site is ligated.",
        intervention: "To OR for hemorrhage control",
      },
      {
        rhythm: "nsr",
        vitals: { hr: 98, spo2: 97, nibp: "106/64", rr: 0 },
        scene: { cpr: false, pads: true, bvm: true, loc: "ALTERED", pulse: "PRESENT", breathing: "ASSISTED" },
        narrative: "Post-op again, transfused, and stabilizing. The team debriefs the code.",
        question: "Which statement about PEA is TRUE?",
        choices: [
          "PEA is never shocked — survival depends on high-quality CPR, epinephrine, and finding the reversible cause",
          "PEA should be defibrillated once at maximum energy",
          "A narrow, fast PEA usually means a primary cardiac cause",
          "The H's and T's only apply to asystole",
        ],
        answer: 0,
        rationale:
          "PEA is non-shockable, full stop. And the rhythm's own morphology is a clue: narrow and fast often points to mechanical/volume problems (hypovolemia, tamponade, tension pneumo, PE), while wide and slow suggests metabolic causes (hyperkalemia, toxins).",
        outcome: "Debrief complete. This one goes in the win column.",
        intervention: "Debrief · case closed",
      },
    ],
  },
];

window.EKG_SCENARIOS = EKG_SCENARIOS;
