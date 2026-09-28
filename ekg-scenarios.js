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
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: 14 },
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
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: 14 },
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
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: 14 },
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
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: 14 },
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
        vitals: { hr: 92, spo2: 93, nibp: "86/52", rr: 0, etco2: 38 },
        scene: { cpr: false, pads: true, bvm: true, loc: "UNRESPONSIVE", pulse: "WEAK", breathing: "ASSISTED" },
        narrative:
          "ROSC achieved: sinus rhythm at 92 with a weak pulse, BP 86/52. He remains unresponsive and is being ventilated.",
        question: "What are the immediate post-ROSC priorities?",
        choices: [
          "Titrate oxygen (SpO2 90–98%), keep MAP ≥ 65 mm Hg with fluids/pressors, obtain a 12-lead EKG, and plan temperature control if the patient isn't following commands",
          "Stop all monitoring — the code is over",
          "Give another epinephrine 1 mg bolus to raise the BP",
          "Extubate now that a pulse is back",
        ],
        answer: 0,
        rationale:
          "Post-cardiac-arrest care (2025 AHA): avoid both hypoxia and hyperoxia (SpO2 90–98%), keep MAP ≥ 65 mm Hg (fluids, then an infusion — not another code-dose epi bolus), obtain a 12-lead to look for STEMI, and use deliberate temperature control (32–37.5 °C for at least 36 hours) if the patient doesn't follow commands. Anticipate re-arrest.",
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
        rhythm: "pacer_noncapture",
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
        rhythm: "paced_v",
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
        rhythm: "paced_v",
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
          "Regular narrow-complex SVT converts at low energy: 50–100 J synchronized initially, escalating if needed. (Afib and flutter start higher — at least 200 J biphasic per the 2025 AHA guidelines; the shock stays synchronized as long as there's an R wave to sync on.)",
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
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: 14 },
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
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: 14 },
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
        vitals: { hr: 96, spo2: 94, nibp: "92/58", rr: 0, etco2: 36 },
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
        vitals: { hr: 128, spo2: null, nibp: "--/--", rr: 0, etco2: 15 },
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
        vitals: { hr: 124, spo2: 92, nibp: "78/44", rr: 0, etco2: 33 },
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

  {
    id: "found-down-asystole",
    title: "Found down — asystole",
    blurb: "An unwitnessed arrest with a flatline. Confirm it's real, run the algorithm, and find the toxic cause.",
    stages: [
      {
        rhythm: "asystole",
        vitals: { hr: 0, spo2: null, nibp: "--/--", rr: 0, etco2: null },
        scene: { cpr: false, pads: false, bvm: false, iv: false, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "NONE" },
        narrative:
          "A visitor finds your patient unresponsive in bed — unknown downtime. No pulse, no breathing. The monitor shows a flat line.",
        question: "What is the correct response to an apparent flatline?",
        choices: [
          "Start CPR immediately while confirming true asystole — check lead connections, turn up the gain, and look at a second lead. Never shock a flatline",
          "Defibrillate at 200 J immediately in case it's fine VF",
          "Wait for the provider to pronounce before starting anything",
          "Give atropine 1 mg and reassess in 3 minutes",
        ],
        answer: 0,
        rationale:
          "Compressions start immediately; while they run, confirm the flatline is real (leads attached, gain up, second lead) — fine VF can masquerade as asystole. Asystole itself is never shocked, and atropine is no longer in the arrest algorithm.",
        outcome: "Leads are secure and the flatline is confirmed in lead II and aVF. True asystole. CPR continues; pads go on anyway in case the rhythm changes.",
        intervention: "Asystole confirmed in 2 leads · CPR started",
      },
      {
        rhythm: "asystole",
        vitals: { hr: 0, spo2: null, nibp: "--/--", rr: 0, etco2: 12 },
        scene: { cpr: true, pads: true, bvm: true, iv: true, meds: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative: "CPR is running with an ETCO2 of 12. An IV is in place. The team leader turns to you for the first medication.",
        question: "What do you give?",
        choices: [
          "Epinephrine 1 mg IV/IO as soon as possible, repeated every 3–5 minutes",
          "Amiodarone 300 mg IV push",
          "Atropine 1 mg IV — asystole is the ultimate bradycardia",
          "Sodium bicarbonate 1 amp for everyone in arrest",
        ],
        answer: 0,
        rationale:
          "Non-shockable arrest gets epinephrine as early as possible — it's the only drug with a routine role in asystole. Antiarrhythmics treat shockable rhythms, atropine was removed from the arrest algorithm, and bicarbonate is reserved for specific causes (hyperkalemia, TCA overdose).",
        outcome: "Epi is in. While you push it, a teammate cuts away the gown — and finds two fentanyl patches on the chest. The pupils are pinpoint.",
        intervention: "Epinephrine 1 mg IV",
      },
      {
        rhythm: "asystole",
        vitals: { hr: 0, spo2: null, nibp: "--/--", rr: 0, etco2: 14 },
        scene: { cpr: true, pads: true, bvm: true, iv: true, meds: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative: "Pinpoint pupils, fentanyl patches, unknown downtime. The H's and T's hunt just got a prime suspect.",
        question: "Which reversible cause fits, and what changes in your management?",
        choices: [
          "Toxins (opioid) causing a hypoxic arrest — remove the patches, prioritize high-quality ventilation and oxygenation; naloxone may be given but never replaces CPR and epinephrine",
          "Tension pneumothorax — needle decompression now",
          "Hypothermia — begin active rewarming",
          "Naloxone alone will restart the heart — pause CPR and give it",
        ],
        answer: 0,
        rationale:
          "Opioid arrests are hypoxic arrests: the priority is oxygenation and ventilation with ongoing CPR and epinephrine. Remove the source (patches). Naloxone is reasonable but in a pulseless patient it does not replace the algorithm — circulation and ventilation do the work.",
        outcome: "Patches off, ventilation is dialed in… at the next rhythm check an organized rhythm marches across the screen. ETCO2 jumps to 24.",
        intervention: "Patches removed · ventilation optimized",
      },
      {
        rhythm: "nsr",
        vitals: { hr: 74, spo2: null, nibp: "--/--", rr: 0, etco2: 24 },
        scene: { cpr: true, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative: "An organized narrow-complex rhythm at 74 has appeared, and the ETCO2 is climbing.",
        question: "What does the team do with this rhythm check?",
        choices: [
          "Pulse check for no more than 10 seconds — an organized rhythm may still be PEA; if there's no pulse, resume compressions instantly",
          "Declare ROSC based on the monitor alone and stop all compressions",
          "Shock the organized rhythm to be safe",
          "Take a full minute to be very sure about the pulse",
        ],
        answer: 0,
        rationale:
          "An organized rhythm at a check triggers a pulse check — capped at 10 seconds. The monitor alone never declares ROSC (that's how PEA fools teams), though a rising ETCO2 is a strong supporting clue. If no pulse: back on the chest immediately.",
        outcome: "There's a strong carotid pulse. ROSC — ETCO2 settles at 38.",
        intervention: "Pulse check ≤10 s → ROSC",
      },
      {
        rhythm: "nsr",
        vitals: { hr: 78, spo2: 94, nibp: "96/58", rr: 0, etco2: 38 },
        scene: { cpr: false, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "WEAK", breathing: "ASSISTED" },
        narrative: "ROSC after an opioid-driven arrest. He's ventilated with a weak pulse and BP 96/58.",
        question: "What is special about post-ROSC care after an opioid arrest?",
        choices: [
          "Keep supporting ventilation and plan for re-sedation — fentanyl outlasts naloxone — plus the standard bundle: SpO2 90–98%, MAP ≥ 65, 12-lead, temperature control",
          "Extubate now that the naloxone is working",
          "Give flumazenil to complete the reversal",
          "No monitoring needed once ROSC is achieved",
        ],
        answer: 0,
        rationale:
          "Naloxone's duration is shorter than fentanyl's — re-sedation and re-arrest are real risks, so ventilation support and close monitoring continue. Flumazenil is a benzodiazepine antagonist and has no role here. The standard post-ROSC bundle still applies.",
        outcome: "He's admitted to the ICU on a ventilator with a naloxone infusion under discussion. Cause found, cause fixed.",
        intervention: "ICU admission · re-sedation precautions",
      },
    ],
  },

  {
    id: "afib-rvr-crash",
    title: "Afib with RVR decompensates",
    blurb: "Rate control goes sideways when the pressure drops. Know when medicine ends and electricity begins.",
    stages: [
      {
        rhythm: "afib",
        vitals: { hr: 148, spo2: 95, nibp: "116/72", rr: 18, etco2: null },
        scene: { cpr: false, pads: false, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative:
          "A 68-year-old admitted for pneumonia becomes aware of a 'fluttering' heart. The monitor shows an irregularly irregular narrow-complex rhythm at 148 with no P waves.",
        question: "What is the rhythm?",
        choices: [
          "Atrial fibrillation with rapid ventricular response",
          "Sinus tachycardia",
          "SVT",
          "Ventricular tachycardia",
        ],
        answer: 0,
        rationale:
          "Irregularly irregular + no P waves + narrow QRS = atrial fibrillation; at 148 it's 'with RVR.' SVT and sinus tach are regular; VT is wide.",
        outcome: "She's still perfusing well: alert, BP 116/72, mild palpitations. Stable — for the moment.",
        intervention: "Afib with RVR identified",
      },
      {
        rhythm: "afib",
        vitals: { hr: 146, spo2: 95, nibp: "112/70", rr: 18, etco2: null },
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
          "Stable afib with RVR gets AV-nodal rate control — IV diltiazem or a beta blocker. Adenosine can't hold afib (seconds-long effect), 300 mg amiodarone push is an arrest dose, and epinephrine would pour fuel on the rate.",
        outcome: "Ten minutes into the diltiazem her rate is 128… then the pump alarms: BP 70/40. She's pale, clammy, and confused. Still in afib.",
        intervention: "Diltiazem started — hypotension follows",
      },
      {
        rhythm: "afib",
        vitals: { hr: 138, spo2: 93, nibp: "70/40", rr: 22, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
        narrative:
          "She is now unstable: hypotensive and altered, still in rapid afib. The diltiazem is stopped and pads are on.",
        question: "What is indicated now?",
        choices: [
          "Synchronized cardioversion — for afib, an initial 200 J biphasic or more — with sedation if it won't delay the shock",
          "Another dose of diltiazem to finish the job",
          "Unsynchronized defibrillation at maximum energy",
          "A fluid bolus and a 4-hour observation period",
        ],
        answer: 0,
        rationale:
          "Unstable tachyarrhythmia with a pulse = synchronized cardioversion. Afib needs more energy than SVT — the 2025 AHA guidelines start at 200 J biphasic or more and escalate if it fails. More AV-nodal blocker would deepen the hypotension, and an unsynchronized shock risks R-on-T.",
        outcome: "One synchronized shock at 200 J. The monitor stutters — then a regular sinus rhythm at 92 appears. Her pressure climbs to 104/62.",
        intervention: "Synchronized cardioversion 200 J",
      },
      {
        rhythm: "nsr",
        vitals: { hr: 92, spo2: 96, nibp: "104/62", rr: 18, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative:
          "Converted and stabilizing. The resident asks: 'Wait — her afib duration was unknown. Weren't we supposed to worry about a clot before cardioverting?'",
        question: "What's the correct teaching point?",
        choices: [
          "Instability overrides the anticoagulation rule — an unstable patient is cardioverted immediately; with unknown duration, anticoagulation and stroke-risk evaluation follow the emergency",
          "Cardioversion was a mistake — a TEE was mandatory first",
          "Afib never requires anticoagulation considerations",
          "Clot risk only matters for atrial flutter",
        ],
        answer: 0,
        rationale:
          "Elective cardioversion of afib lasting >48 h (or unknown) requires prior anticoagulation or a TEE to rule out atrial clot. But an UNSTABLE patient is cardioverted without delay — the arrhythmia is killing them now. Anticoagulation is then addressed immediately after.",
        outcome: "Anticoagulation is started and a TEE is scheduled. The resident writes it down.",
        intervention: "Anticoagulation initiated post-conversion",
      },
      {
        rhythm: "nsr",
        vitals: { hr: 88, spo2: 97, nibp: "110/68", rr: 16, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "She's stable in sinus rhythm. The team reviews why this happened during a pneumonia admission.",
        question: "What completes the workup?",
        choices: [
          "Hunt the trigger: infection/sepsis, hypoxia, electrolytes (K+, Mg2+), thyroid function, and ischemia — and keep her on telemetry for recurrence",
          "No workup — afib is always idiopathic",
          "Discharge now; the problem is fixed",
          "Daily cardioversions as prophylaxis",
        ],
        answer: 0,
        rationale:
          "New afib usually has a driver — infection, hypoxia, electrolyte derangement, thyroid disease, ischemia, or structural disease. Treating the pneumonia and correcting electrolytes is as important as the rhythm itself, and recurrence is common enough to warrant telemetry.",
        outcome: "Her magnesium comes back low and is replaced. The pneumonia gets treated. No recurrence overnight.",
        intervention: "Trigger workup · telemetry continued",
      },
    ],
  },

  {
    id: "stemi-vt",
    title: "Chest pain into VT",
    blurb: "A STEMI declares itself, then the ventricle starts firing. Stable VT, pulseless VT, and the cath lab.",
    stages: [
      {
        rhythm: "sinus_tach",
        vitals: { hr: 118, spo2: 95, nibp: "142/88", rr: 20, etco2: null },
        scene: { cpr: false, pads: false, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative:
          "A 58-year-old develops crushing substernal chest pain radiating to the jaw, with diaphoresis and nausea. The monitor shows sinus tachycardia at 118.",
        question: "The 12-lead shows ST elevation in II, III, and aVF. What happens now?",
        choices: [
          "Aspirin 162–325 mg chewed, activate the cath lab — this is an inferior STEMI and time is muscle; check the right side and BP before any nitroglycerin",
          "Serial troponins over 12 hours before deciding anything",
          "Nitroglycerin immediately, no other assessment needed",
          "A GI cocktail to rule out reflux first",
        ],
        answer: 0,
        rationale:
          "ST elevation in II, III, aVF is an inferior STEMI — chewed aspirin and immediate reperfusion (cath lab) are the priorities. Inferior MIs often involve the right ventricle, where nitroglycerin can crash the pressure — obtain right-sided leads and check BP first.",
        outcome: "Cath lab is activated. While you're on the phone, the monitor alarm fires — the rhythm has changed to a wide, regular tachycardia. He's pale but still talking, BP 96/60.",
        intervention: "ASA given · cath lab activated",
      },
      {
        rhythm: "vt",
        vitals: { hr: 172, spo2: 93, nibp: "96/60", rr: 20, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: true, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative:
          "Monomorphic VT at 172 — but he has a pulse and is still perfusing (awake, BP 96/60). Pads are on as a precaution.",
        question: "How is stable VT with a pulse treated?",
        choices: [
          "Amiodarone 150 mg IV over 10 minutes (not the 300 mg arrest push), with pads on and cardioversion ready if he deteriorates",
          "Amiodarone 300 mg IV push — same as the arrest dose",
          "Immediate unsynchronized defibrillation",
          "Adenosine is first-line for all wide-complex rhythms",
        ],
        answer: 0,
        rationale:
          "Stable VT with a pulse is treated pharmacologically first: amiodarone 150 mg over 10 minutes (or procainamide/sotalol). The 300 mg rapid push is reserved for pulseless arrest. Electricity waits unless he becomes unstable — but you stay ready for it.",
        outcome: "The amiodarone is infusing… then his eyes roll back. The monitor still shows VT — but there is no pulse.",
        intervention: "Amiodarone 150 mg infusion started",
      },
      {
        rhythm: "vt",
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: 15 },
        scene: { cpr: true, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative: "Pulseless VT. Compressions have started and the defibrillator is charging.",
        question: "Pulseless VT is managed with…",
        choices: [
          "Immediate unsynchronized defibrillation (~200 J biphasic) with high-quality CPR between shocks — it's treated exactly like VF now",
          "Synchronized cardioversion at 100 J",
          "Finishing the amiodarone infusion before shocking",
          "Vagal maneuvers",
        ],
        answer: 0,
        rationale:
          "The moment VT loses the pulse it becomes a shockable arrest rhythm — same algorithm as VF: unsynchronized defibrillation, 2-minute CPR cycles, epinephrine, and antiarrhythmic per the arrest pathway.",
        outcome: "One shock — and at the next check: sinus rhythm at 98 with a palpable pulse. ROSC. ETCO2 jumps to 37.",
        intervention: "Defibrillated 200 J → ROSC",
      },
      {
        rhythm: "nsr",
        vitals: { hr: 98, spo2: 94, nibp: "98/60", rr: 0, etco2: 37 },
        scene: { cpr: false, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "WEAK", breathing: "ASSISTED" },
        narrative: "ROSC — but the STEMI that started all this is still there, and the cath lab is waiting.",
        question: "Does the arrest change the cath lab plan?",
        choices: [
          "No — get him to the cath lab emergently; reperfusion treats the cause of the arrest, alongside standard post-ROSC care en route",
          "Yes — cancel the cath lab; arrest patients can't be catheterized",
          "Wait 24 hours to see if he wakes up first",
          "The amiodarone replaced the need for reperfusion",
        ],
        answer: 0,
        rationale:
          "A STEMI with cardiac arrest and ROSC goes to the cath lab emergently — the occluded artery caused the VT, and reperfusion is the definitive treatment. Post-ROSC care (oxygenation, pressure support, temperature management) travels with him.",
        outcome: "He's wheeled to the lab within minutes — a 100% occluded RCA is opened and stented.",
        intervention: "Emergent PCI — RCA stented",
      },
      {
        rhythm: "aivr",
        vitals: { hr: 72, spo2: 97, nibp: "112/70", rr: 14, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative:
          "Post-PCI, extubated and awake. On the monitor you notice occasional short runs of a slow, wide rhythm around 70 that come and go.",
        question: "How do you interpret these runs after reperfusion?",
        choices: [
          "Likely accelerated idioventricular rhythm (AIVR) — a common, usually benign reperfusion rhythm; observe, don't suppress, but stay alert for true VT",
          "Recurrent VT — defibrillate immediately",
          "Artifact — ignore all wide rhythms now",
          "Complete heart block — start pacing",
        ],
        answer: 0,
        rationale:
          "AIVR — a wide rhythm at roughly 40–120 appearing after reperfusion — is a classic, usually benign 'reperfusion arrhythmia' that resolves on its own. It's observed, not suppressed. The nurse's job is telling it apart from fast, sustained VT, which is a different conversation.",
        outcome: "The runs fade out over the next hour. He asks what's for lunch. Case closed.",
        intervention: "AIVR observed · resolved",
      },
    ],
  },

  {
    id: "hyperk-code",
    title: "The dialysis no-show",
    blurb: "A missed-dialysis patient with a potassium of 8.1 codes in front of you. Calcium, shifting, and the machine that fixes it.",
    stages: [
      {
        rhythm: "hyperk",
        vitals: { hr: 46, spo2: 95, nibp: "98/60", rr: 16, etco2: null },
        scene: { cpr: false, pads: false, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative:
          "A dialysis patient who missed his last two sessions arrives weak and nauseated. The monitor shows a slow rhythm with tall, peaked T waves and a widening QRS. Stat K+ returns: 8.1.",
        question: "What is the FIRST medication, and why?",
        choices: [
          "IV calcium (gluconate or chloride) — it stabilizes the cardiac membrane within minutes but does not lower the potassium",
          "Kayexalate — it removes potassium fastest",
          "Insulin and D50 before anything else",
          "A normal saline bolus is sufficient",
        ],
        answer: 0,
        rationale:
          "With hyperkalemic EKG changes, calcium comes first: it raises the threshold potential and protects against arrest within minutes — but the K+ is untouched. Shifting agents come next; slow binders like Kayexalate are far too slow for a K of 8.1 with EKG changes.",
        outcome: "Calcium is in and the QRS narrows slightly. Now the potassium itself needs to move.",
        intervention: "Calcium gluconate 1 g IV",
      },
      {
        rhythm: "hyperk",
        vitals: { hr: 48, spo2: 95, nibp: "96/58", rr: 16, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: true, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "Membrane stabilized. The provider asks for the shifting therapy while nephrology is paged.",
        question: "Which combination shifts potassium into the cells?",
        choices: [
          "Regular insulin 10 units IV with D50, plus high-dose albuterol nebs; bicarbonate if acidotic",
          "More calcium — it lowers potassium if repeated",
          "Furosemide alone — diuresis is the main therapy",
          "Potassium-sparing diuretics",
        ],
        answer: 0,
        rationale:
          "Insulin (with dextrose to prevent hypoglycemia) and beta-agonists drive K+ intracellularly within 15–30 minutes; bicarbonate helps when acidotic. These buy time — they don't remove potassium. Watch the glucose after insulin. Removal requires dialysis (or GI binders over hours).",
        outcome: "Insulin/D50 and albuterol are running… then he slumps. The monitor shows a slow, wide rhythm — and there is no pulse.",
        intervention: "Insulin/D50 + albuterol given",
      },
      {
        rhythm: "idioventricular",
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: 13 },
        scene: { cpr: true, pads: true, bvm: true, iv: true, meds: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative:
          "PEA arrest — a slow, wide complex on the screen with no pulse. CPR is underway.",
        question: "How does the known hyperkalemia change the code?",
        choices: [
          "Run standard PEA care (CPR + epinephrine) AND aggressively treat the cause: repeat IV calcium, continue shifting therapy, push for emergent dialysis",
          "Hyperkalemia doesn't matter during the arrest — drugs come after ROSC",
          "Defibrillate the wide slow rhythm",
          "Stop resuscitation — hyperkalemic arrests are futile",
        ],
        answer: 0,
        rationale:
          "PEA management is CPR + epinephrine + fixing the reversible cause — and here the cause is known. Repeat calcium, keep shifting K+, and mobilize dialysis. Hyperkalemic arrests can have good outcomes precisely because the cause is treatable. A wide slow PEA is not shockable. (The 2025 AHA guidelines note that calcium's benefit once the patient is in arrest is not well established — it must never delay CPR or epinephrine.)",
        outcome: "Second dose of calcium, epi in, compressions never stop… at the next check: an organized rhythm with a femoral pulse. ROSC.",
        intervention: "Epi 1 mg · calcium repeated",
      },
      {
        rhythm: "nsr",
        vitals: { hr: 88, spo2: 94, nibp: "92/56", rr: 0, etco2: 36 },
        scene: { cpr: false, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "WEAK", breathing: "ASSISTED" },
        narrative: "ROSC. The potassium is still 7.4 — the calcium and insulin bought time, nothing more.",
        question: "What is the definitive treatment he needs now?",
        choices: [
          "Emergent hemodialysis — it's the only thing that actually removes the potassium",
          "Another round of calcium is definitive",
          "Insulin infusion for 24 hours will normalize it permanently",
          "Observation — potassium self-corrects after ROSC",
        ],
        answer: 0,
        rationale:
          "Calcium protects, insulin/albuterol shift — only dialysis (or, slowly, GI binders and kidneys that work) removes potassium. In a dialysis patient post-hyperkalemic-arrest, emergent hemodialysis is the definitive move, with the K+ rechecked serially until then.",
        outcome: "The dialysis nurse arrives with the machine. Two hours later the potassium is 5.2.",
        intervention: "Emergent hemodialysis",
      },
      {
        rhythm: "nsr",
        vitals: { hr: 82, spo2: 97, nibp: "108/66", rr: 14, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "Dialyzed, extubated, and stable. The team debriefs the roles each drug played.",
        question: "Which summary of hyperkalemia treatment is correct?",
        choices: [
          "Calcium stabilizes the membrane, insulin/albuterol shift potassium into cells temporarily, and dialysis removes it — three different jobs, all three needed",
          "Calcium lowers potassium, so repeat it until the level normalizes",
          "Kayexalate is the emergency drug of choice for K+ of 8 with EKG changes",
          "Once the EKG normalizes, no further treatment or monitoring is needed",
        ],
        answer: 0,
        rationale:
          "The classic triad: stabilize (calcium), shift (insulin/glucose, albuterol, bicarb if acidotic), remove (dialysis, binders). Shifted potassium rebounds as the drugs wear off, so serial levels and telemetry continue until removal is done.",
        outcome: "He promises to never miss dialysis again. The team believes him... mostly.",
        intervention: "Debrief · serial K+ monitoring",
      },
    ],
  },

  {
    id: "tension-pneumo",
    title: "Crashing chest trauma",
    blurb: "A chest-trauma patient obstructs his own circulation. No drug fixes this one — find it and decompress it.",
    stages: [
      {
        rhythm: "sinus_tach",
        vitals: { hr: 132, spo2: 84, nibp: "84/52", rr: 28, etco2: null },
        scene: { cpr: false, pads: false, bvm: false, iv: true, meds: false, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
        narrative:
          "A patient admitted after a fall with right-sided rib fractures suddenly deteriorates: severe dyspnea, SpO2 84%, BP 84/52. Breath sounds are ABSENT on the right, the trachea deviates left, and his neck veins are distended.",
        question: "What is happening, and what does he need?",
        choices: [
          "Tension pneumothorax — immediate needle decompression (2nd intercostal space midclavicular, or 4th/5th anterior axillary); do NOT wait for a chest X-ray",
          "Pulmonary embolism — stat CT angiogram",
          "Cardiac tamponade — pericardiocentesis",
          "Anxiety attack — benzodiazepines and reassurance",
        ],
        answer: 0,
        rationale:
          "Unilateral absent breath sounds + tracheal deviation + JVD + shock after chest trauma is tension pneumothorax — a clinical diagnosis. Imaging first is a classic fatal delay: decompress on recognition. (Tamponade causes JVD too, but not the unilateral silent chest.)",
        outcome: "The provider is grabbing the needle — but before it's ready, his eyes close. The carotid is silent. He has arrested.",
        intervention: "Tension pneumothorax recognized",
      },
      {
        rhythm: "sinus_tach",
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: 10 },
        scene: { cpr: true, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative:
          "PEA arrest — a fast narrow rhythm on the monitor, no pulse. CPR is running, and the ETCO2 is only 10 despite good compressions.",
        question: "What must happen alongside CPR and epinephrine?",
        choices: [
          "Needle decompression NOW — this is an obstructive arrest; compressions and epinephrine cannot generate output past an obstructed circulation",
          "Nothing else — standard PEA care alone will fix it",
          "Defibrillate the fast rhythm",
          "Pause CPR for a portable chest X-ray to confirm",
        ],
        answer: 0,
        rationale:
          "Obstructive causes (tension pneumothorax, tamponade, massive PE) are mechanical problems — the chest is pressurized and the heart can't fill. The decompression IS the resuscitation; drugs and compressions merely bridge to it. And no imaging pauses in an arrest.",
        outcome: "The needle goes in — a hiss of air escapes. Within one cycle the ETCO2 leaps from 10 to 34, and the next check finds a bounding femoral pulse.",
        intervention: "Needle decompression → ROSC",
      },
      {
        rhythm: "sinus_tach",
        vitals: { hr: 124, spo2: 91, nibp: "88/54", rr: 0, etco2: 34 },
        scene: { cpr: false, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "WEAK", breathing: "ASSISTED" },
        narrative: "ROSC after decompression. The needle catheter is taped in place, hissing softly with each ventilation.",
        question: "The needle worked — what does he still need, and why?",
        choices: [
          "A chest tube (tube thoracostomy) — needle decompression is temporizing; the catheter can kink or clot and the tension can rebuild",
          "Nothing further — the needle is the definitive treatment",
          "Immediate removal of the needle now that he has a pulse",
          "Bilateral prophylactic needles",
        ],
        answer: 0,
        rationale:
          "The needle converts a tension pneumothorax into an open one and buys minutes — it is never definitive. A chest tube follows as soon as possible; until it's in, watch for the tension re-accumulating (falling sats, rising airway pressures, dropping BP).",
        outcome: "The chest tube goes in at the fourth interspace — a rush of air, and the lung begins to re-expand.",
        intervention: "Chest tube placed",
      },
      {
        rhythm: "sinus_tach",
        vitals: { hr: 112, spo2: 95, nibp: "98/60", rr: 0, etco2: 36 },
        scene: { cpr: false, pads: true, bvm: true, iv: true, meds: false, loc: "ALTERED", pulse: "PRESENT", breathing: "ASSISTED" },
        narrative: "Chest tube in, pressures improving. NOW the post-decompression chest X-ray is done, confirming placement and re-expansion.",
        question: "Which monitoring priorities follow a tension pneumothorax arrest?",
        choices: [
          "Watch the chest tube (swing, output, air leak), respiratory status, and vitals for re-tension; serial imaging; and standard post-ROSC care",
          "The chest tube needs no monitoring once placed",
          "Clamp the chest tube for transport",
          "Remove the tube after one hour if he looks well",
        ],
        answer: 0,
        rationale:
          "Post-tube care: confirm position, monitor for air leak and drainage, never clamp a bubbling tube (that rebuilds the tension), and watch for recurrence. The X-ray happens AFTER decompression — it confirms, it never gates, the treatment.",
        outcome: "The tube swings with respiration and the trachea is midline again. He's waking up and fighting the ETT — a good sign, honestly.",
        intervention: "Chest tube monitoring · CXR confirms",
      },
      {
        rhythm: "nsr",
        vitals: { hr: 96, spo2: 97, nibp: "108/66", rr: 16, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "Extubated and stable with the chest tube to suction. The team debriefs the case.",
        question: "What is the core lesson of an obstructive-cause arrest?",
        choices: [
          "When PEA has a mechanical cause, the mechanical fix is the resuscitation — recognize obstructive physiology clinically and act without waiting for imaging",
          "Epinephrine works equally well for every PEA cause",
          "Tension pneumothorax can only be diagnosed radiographically",
          "Obstructive arrests are unsurvivable",
        ],
        answer: 0,
        rationale:
          "The H's and T's aren't a recitation — they're a search list. Obstructive causes (tension pneumothorax, tamponade, PE) kill by physics, and only reversing the physics restores circulation. Clinical recognition and immediate action made this save.",
        outcome: "He keeps the chest tube for two days and walks out a week later. The debrief goes in the unit's teaching file.",
        intervention: "Debrief · teaching case filed",
      },
    ],
  },
  {
    id: "wpw-afib",
    title: "Too fast, too wide, too irregular",
    blurb: "A young patient with palpitations and an accessory pathway. The reflex drug is the dangerous one — know what NOT to give.",
    stages: [
      {
        rhythm: "wpw_afib",
        vitals: { hr: 236, spo2: 96, nibp: "108/70", rr: 20, etco2: null },
        scene: { cpr: false, pads: false, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative:
          "A 24-year-old arrives with sudden palpitations. They mention an 'abnormal EKG' as a teenager that nobody followed up. The monitor shows a very fast, irregular rhythm with wide QRS complexes that change shape beat to beat. Awake, BP 108/70.",
        question: "What is the most likely rhythm, and what is the core treatment principle?",
        choices: [
          "Pre-excited atrial fibrillation (WPW) — pads on, avoid all AV-nodal blockers, and anticipate procainamide or cardioversion",
          "Afib with RVR — give diltiazem for rate control",
          "SVT — give adenosine 6 mg rapid IV push",
          "Sinus tachycardia — treat the anxiety",
        ],
        answer: 0,
        rationale:
          "Irregularly irregular + wide, varying QRS + rates over 200 in a young patient is afib conducting down an accessory pathway. Blocking the AV node forces more impulses down the pathway and can trigger VF — so adenosine, diltiazem/verapamil, beta blockers, and digoxin are all off the table.",
        outcome: "Pads go on. The provider orders procainamide — but a colleague walks in holding a syringe of diltiazem, 'to get the rate down.'",
        intervention: "Pre-excited afib recognized · pads on",
      },
      {
        rhythm: "wpw_afib",
        vitals: { hr: 244, spo2: 95, nibp: "104/66", rr: 22, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: true, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "The diltiazem syringe is at the IV port.",
        question: "What do you do?",
        choices: [
          "Stop it — speak up and hold the diltiazem; AV-nodal blockade in pre-excited afib can accelerate the ventricular rate and cause VF",
          "Give it — diltiazem is first-line for any afib with a fast rate",
          "Give half the dose as a compromise",
          "Give adenosine instead — it's shorter-acting and safer",
        ],
        answer: 0,
        rationale:
          "This is a patient-safety stop. Diltiazem is correct for ordinary afib with RVR but dangerous here. Procainamide slows the accessory pathway itself; it is loaded carefully, stopping for hypotension, QRS widening > 50%, arrhythmia suppression, or the maximum dose.",
        outcome: "The diltiazem goes back in the drawer and the procainamide load begins. Minutes later the patient becomes gray and confused — BP 76/40.",
        intervention: "Diltiazem held · procainamide started",
      },
      {
        rhythm: "wpw_afib",
        vitals: { hr: 252, spo2: 91, nibp: "76/40", rr: 24, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: true, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
        narrative: "Now hypotensive and altered, still in the same fast, wide, irregular rhythm, with a weak pulse.",
        question: "What is the next step?",
        choices: [
          "Stop the procainamide and perform synchronized cardioversion (sedate if it won't delay the shock)",
          "Continue the procainamide load and recheck in 10 minutes",
          "Give amiodarone 300 mg IV push",
          "Start CPR",
        ],
        answer: 0,
        rationale:
          "Instability (hypotension, altered mental status) with a tachyarrhythmia and a pulse means electricity: synchronized cardioversion. Hypotension is also a stop point for procainamide. The patient still has a pulse, so this is not CPR — yet.",
        outcome: "Synchronized shock delivered. The monitor resets to a regular sinus rhythm — with a short PR and a slurred upstroke on every QRS.",
        intervention: "Synchronized cardioversion → sinus",
      },
      {
        rhythm: "wpw",
        vitals: { hr: 82, spo2: 98, nibp: "112/70", rr: 16, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "Converted and stable. The underlying sinus rhythm now shows the patient's baseline pattern.",
        question: "What does this strip show, and what is the long-term plan?",
        choices: [
          "WPW pattern (short PR, delta wave, wide QRS) — electrophysiology referral for catheter ablation, and flag the chart: no AV-nodal blockers for future afib",
          "Normal sinus rhythm — no follow-up needed",
          "First-degree AV block — permanent pacemaker",
          "Hyperkalemia — give calcium",
        ],
        answer: 0,
        rationale:
          "Short PR + delta wave + widened QRS = WPW pattern. Catheter ablation of the accessory pathway is curative. Until then, every future clinician needs to know that AV-nodal blockers are dangerous in this patient if afib recurs.",
        outcome: "EP is consulted and ablation is scheduled. The chart carries a bold allergy-style warning about AV-nodal blockers.",
        intervention: "EP referral · chart flagged",
      },
    ],
  },

  {
    id: "mobitz2-anterior",
    title: "The block that doesn't warn",
    blurb: "Two days after an anterior MI, a Mobitz II appears. Pads before it's needed, pacing when it is, and proving capture.",
    stages: [
      {
        rhythm: "mobitz2",
        vitals: { hr: 50, spo2: 96, nibp: "104/66", rr: 16, etco2: null },
        scene: { cpr: false, pads: false, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative:
          "A 67-year-old, two days after an anterior MI, feels 'a little lightheaded.' The monitor shows a constant PR interval with QRS complexes that drop without warning. BP 104/66.",
        question: "What is the nursing priority right now?",
        choices: [
          "Apply transcutaneous pacing pads now, notify the provider, and stay with the patient — Mobitz II can progress to complete heart block without warning",
          "Recheck in an hour — the BP is fine",
          "Give metoprolol for post-MI protection as scheduled",
          "Obtain an order for adenosine",
        ],
        answer: 0,
        rationale:
          "Mobitz II is an infranodal block — especially ominous after an anterior MI — and can abruptly become complete heart block. Pads go on BEFORE it's needed, the provider is notified, and AV-nodal blockers are held.",
        outcome: "Pads are on and the provider is on the way. Then the alarm sounds: P waves march on, but the QRS complexes are slow, wide, and unrelated. The patient is gray and barely responsive.",
        intervention: "Pacing pads applied · provider notified",
      },
      {
        rhythm: "avb3",
        vitals: { hr: 30, spo2: 90, nibp: "70/40", rr: 14, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
        narrative: "Complete heart block with a wide ventricular escape at 30. BP 70/40, altered.",
        question: "What is the priority intervention?",
        choices: [
          "Begin transcutaneous pacing immediately — atropine may be tried, but it rarely works in infranodal block and must not delay pacing",
          "Atropine every 3–5 minutes up to 3 mg, and pace only if that fails",
          "Synchronized cardioversion at 100 J",
          "Amiodarone 150 mg IV to stabilize the ventricle",
        ],
        answer: 0,
        rationale:
          "Unstable high-degree block with a wide escape is an infranodal problem that atropine usually can't fix. Pacing (or a chronotropic infusion as bridge) is the move. Antiarrhythmics could suppress the escape rhythm — the only thing keeping this patient alive.",
        outcome: "Pacing starts at a rate of 70. You turn up the output until every spike is followed by a wide QRS.",
        intervention: "Transcutaneous pacing started",
      },
      {
        rhythm: "paced_v",
        vitals: { hr: 70, spo2: 94, nibp: "92/58", rr: 16, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALTERED", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "Electrical capture on the monitor: a spike before every wide QRS at 70.",
        question: "How do you confirm MECHANICAL capture?",
        choices: [
          "Palpate a femoral pulse (or watch a pleth waveform) that matches the paced rate — carotid palpation is unreliable because pacing makes the neck muscles jerk",
          "The spikes on the monitor prove capture — nothing else is needed",
          "Check the carotid pulse only",
          "Ask the patient whether they feel better",
        ],
        answer: 0,
        rationale:
          "Electrical capture (spike → QRS) does not guarantee the ventricle is contracting. Mechanical capture is confirmed by a pulse at the paced rate, best felt at the femoral artery, since transcutaneous pacing causes muscle twitching that can mimic a carotid pulse.",
        outcome: "Femoral pulse at 70, matching the monitor. BP rises to 92/58 — and the patient, more alert now, grimaces with every paced beat.",
        intervention: "Mechanical capture confirmed (femoral)",
      },
      {
        rhythm: "paced_v",
        vitals: { hr: 70, spo2: 96, nibp: "98/62", rr: 18, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: true, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "Perfusing, awake, and in obvious pain from the pacing.",
        question: "What does the patient need now?",
        choices: [
          "Analgesia and/or sedation per order while pacing continues, and arrangements for transvenous pacing, then permanent pacemaker evaluation",
          "Turn the pacer off — the pain means it's working too well",
          "Nothing — transcutaneous pacing is a long-term solution",
          "Reduce the mA below the capture threshold to ease the pain",
        ],
        answer: 0,
        rationale:
          "Transcutaneous pacing is painful and is only a bridge. Treat the pain, keep the output above threshold, and move toward transvenous pacing and a permanent pacemaker.",
        outcome: "Comfortable on analgesia, the patient goes to the cath lab for a transvenous wire. A permanent pacemaker follows two days later.",
        intervention: "Analgesia · transvenous pacing arranged",
      },
    ],
  },

  {
    id: "dig-toxicity",
    title: "Yellow halos",
    blurb: "An older patient on digoxin with new kidney injury, nausea, and a changing rhythm. Spot the toxicity, give the antidote, watch the potassium.",
    stages: [
      {
        rhythm: "junctional",
        vitals: { hr: 44, spo2: 95, nibp: "102/60", rr: 16, etco2: null },
        scene: { cpr: false, pads: false, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative:
          "An 84-year-old on digoxin for afib and heart failure was admitted with dehydration and an acute kidney injury. Now nauseated and confused, they describe 'yellow halos' around the lights. The monitor shows a slow, narrow rhythm with no P waves.",
        question: "What do you suspect, and what do you do?",
        choices: [
          "Digoxin toxicity — hold the digoxin, notify the provider, and request a digoxin level, potassium, and renal function",
          "Normal side effects — give the scheduled digoxin dose",
          "Delirium from dehydration only — give a fluid bolus and continue all medications",
          "Migraine — give an antiemetic and dim the lights",
        ],
        answer: 0,
        rationale:
          "GI symptoms, confusion, visual color changes, and a new junctional rhythm in a digoxin patient with falling kidney function is toxicity until proven otherwise. Digoxin is renally cleared, so AKI drives the level up.",
        outcome: "Digoxin held. The level returns markedly elevated, the potassium is 6.1 — and the monitor now shows wide beats alternating with narrow ones.",
        intervention: "Digoxin held · labs sent",
      },
      {
        rhythm: "pvc_bigeminy",
        vitals: { hr: 52, spo2: 94, nibp: "92/56", rr: 18, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALTERED", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "Ventricular bigeminy, a potassium of 6.1, and worsening confusion.",
        question: "Which therapy is indicated?",
        choices: [
          "Digoxin immune Fab (the antidote), dosed per provider and pharmacy from the level or amount ingested",
          "A potassium chloride rider to protect against arrhythmias",
          "Another digoxin dose to control the afib",
          "Amiodarone 300 mg IV push for the PVCs",
        ],
        answer: 0,
        rationale:
          "Digoxin toxicity with ventricular dysrhythmias, hyperkalemia, or end-organ effects is an indication for digoxin immune Fab. Giving potassium to a hyperkalemic patient is dangerous, and amiodarone raises digoxin levels.",
        outcome: "The Fab fragments infuse over 30 minutes. The ectopy settles and the rhythm slows into a regular sinus rhythm.",
        intervention: "Digoxin immune Fab given",
      },
      {
        rhythm: "sinus_brady",
        vitals: { hr: 58, spo2: 96, nibp: "108/64", rr: 16, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "Much better. The provider asks what to watch for over the next several hours.",
        question: "Which monitoring priorities follow digoxin immune Fab?",
        choices: [
          "Serial potassium (it can fall quickly — even to hypokalemia), telemetry for a rebound of the afib rate or heart failure, and know that post-Fab digoxin levels are unreliable",
          "Repeat the digoxin level every hour to guide more Fab",
          "No monitoring — the antidote is permanent and has no side effects",
          "Restart digoxin at the home dose tomorrow morning",
        ],
        answer: 0,
        rationale:
          "Fab reactivates the Na-K pump, so potassium moves back into cells — levels can drop fast. With digoxin's effect removed, the afib rate and heart failure can return. Standard assays measure bound digoxin too, so levels after Fab are misleading.",
        outcome: "The potassium settles at 4.2. The afib rate stays controlled on a different agent while the kidneys recover.",
        intervention: "Post-Fab K+ and telemetry monitoring",
      },
      {
        rhythm: "nsr",
        vitals: { hr: 76, spo2: 97, nibp: "118/70", rr: 16, etco2: null },
        scene: { cpr: false, pads: false, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "Recovering well. The team reviews how this happened.",
        question: "Which factors raise the risk of digoxin toxicity?",
        choices: [
          "Kidney dysfunction, hypokalemia (e.g., from diuretics), hypomagnesemia, older age, and interacting drugs such as amiodarone and verapamil",
          "Hyperkalemia only",
          "Taking digoxin with food",
          "A high-protein diet",
        ],
        answer: 0,
        rationale:
          "Digoxin has a narrow therapeutic window. Reduced renal clearance and interacting drugs raise the level, and low potassium or magnesium increases myocardial sensitivity even at 'normal' levels. Hyperkalemia is a marker of severe acute toxicity, not a risk factor for it.",
        outcome: "Discharge teaching covers pulse checks, toxicity symptoms, and lab follow-up. Digoxin stays on hold pending cardiology review.",
        intervention: "Debrief · risk factors reviewed",
      },
    ],
  },

  {
    id: "stemi-vf",
    title: "Door-to-balloon, interrupted",
    blurb: "An inferolateral STEMI in the ED: aspirin, nitrates, and the clock — until VF arrives with the pads already on.",
    stages: [
      {
        rhythm: "stemi",
        vitals: { hr: 84, spo2: 96, nibp: "138/84", rr: 20, etco2: null },
        scene: { cpr: false, pads: false, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative:
          "A 55-year-old with 40 minutes of crushing chest pressure. The lead II strip shows an elevated ST segment merging into the T wave. The 12-lead, done within 10 minutes of arrival, shows ST elevation in II, III, aVF, V5, and V6. SpO2 96% on room air.",
        question: "What happens next?",
        choices: [
          "Aspirin 162–325 mg chewed and activate the cath lab; no supplemental oxygen is needed while SpO2 is 96%",
          "100% non-rebreather mask for every chest pain patient",
          "Wait for the first troponin before activating anything",
          "Give a GI cocktail to rule out reflux first",
        ],
        answer: 0,
        rationale:
          "STEMI is a diagnosis made on the EKG — troponins don't gate reperfusion. The goal is primary PCI within 90 minutes of first medical contact. Oxygen is given only for hypoxemia (SpO2 < 90%); routine oxygen offers no benefit.",
        outcome: "Aspirin chewed, cath lab activated, pads applied as a precaution. The pain is still 8/10.",
        intervention: "ASA 324 mg chewed · cath lab activated",
      },
      {
        rhythm: "stemi",
        vitals: { hr: 86, spo2: 96, nibp: "136/82", rr: 18, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative:
          "Right-sided leads show no RV involvement. BP 136/82. The patient denies any erectile-dysfunction medication. Nitroglycerin is ordered.",
        question: "How is nitroglycerin given here?",
        choices: [
          "0.4 mg sublingual every 5 minutes, up to 3 doses, checking BP before each dose and holding for SBP < 90",
          "One 0.4 mg dose only, then never again",
          "Three doses at once for faster relief",
          "It's contraindicated in all inferior MIs",
        ],
        answer: 0,
        rationale:
          "Sublingual nitroglycerin 0.4 mg every 5 minutes (up to 3 doses) is appropriate for ongoing ischemic pain when BP allows, there's no RV infarct, and no recent PDE-5 inhibitor use. Inferior MI isn't an absolute contraindication — RV involvement is the concern.",
        outcome: "After the second nitro, the patient says, 'I feel strange' — and slumps. The monitor alarm screams.",
        intervention: "Nitroglycerin 0.4 mg SL ×2",
      },
      {
        rhythm: "vf_coarse",
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "NONE" },
        narrative: "Coarse VF. Unresponsive and pulseless. The pads are already on and connected.",
        question: "What is the immediate action?",
        choices: [
          "Defibrillate immediately — a monitored, witnessed VF arrest with pads on should be shocked without delay — then resume CPR for 2 minutes",
          "Give 2 minutes of CPR first, then consider a shock",
          "Give epinephrine 1 mg before the first shock",
          "Synchronized cardioversion at 100 J",
        ],
        answer: 0,
        rationale:
          "When VF is witnessed and a defibrillator is already attached, the shock comes first — every second of delay lowers the chance it works. Compressions resume immediately after, without a pulse check.",
        outcome: "One shock. Compressions start. At the 2-minute check: an organized rhythm, a strong femoral pulse — and the patient opens their eyes and asks what happened.",
        intervention: "Defibrillated ×1 → ROSC",
      },
      {
        rhythm: "stemi",
        vitals: { hr: 96, spo2: 97, nibp: "112/70", rr: 18, etco2: null },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALERT", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "ROSC after a brief arrest. Awake, following commands, with the ST elevation still on the monitor.",
        question: "What is the plan now?",
        choices: [
          "Straight to the cath lab with pads on — the occluded artery caused the VF; targeted temperature management is for patients who don't follow commands after ROSC",
          "Cancel the cath lab — the arrest makes PCI too risky",
          "Start targeted temperature management and delay PCI 24 hours",
          "Give amiodarone 300 mg IV push now that the pulse is back",
        ],
        answer: 0,
        rationale:
          "ROSC with ongoing STEMI means emergent reperfusion. TTM applies to comatose patients (not following commands) after ROSC; this patient is awake. The 300 mg amiodarone push is an arrest dose, not a post-ROSC one.",
        outcome: "Door-to-balloon: 71 minutes, including the arrest. The circumflex is opened and stented.",
        intervention: "Emergent PCI",
      },
    ],
  },

  {
    id: "opioid-arrest",
    title: "The PCA pump",
    blurb: "A post-op patient on a PCA stops breathing. Ventilation first, naloxone as an adjunct, and CPR when the pulse goes.",
    stages: [
      {
        rhythm: "sinus_brady",
        vitals: { hr: 50, spo2: 78, nibp: "96/58", rr: 4, etco2: null },
        scene: { cpr: false, pads: false, bvm: false, iv: true, meds: false, loc: "ALTERED", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative:
          "A 70-year-old post-op patient on a hydromorphone PCA is found responding only to a sternal rub. Pinpoint pupils, respirations 4 and shallow, SpO2 78%. A pulse is present.",
        question: "What comes first?",
        choices: [
          "Call for help, open the airway, and ventilate with a bag-valve-mask; stop the PCA and get naloxone",
          "Recheck the vitals in 15 minutes",
          "Give naloxone and walk away to document",
          "Place a nasal cannula at 2 L and let the patient sleep it off",
        ],
        answer: 0,
        rationale:
          "Opioid respiratory depression with a pulse: breathing is the emergency. Open the airway and ventilate, stop the opioid source, and give naloxone. Oxygen alone doesn't fix a respiratory rate of 4.",
        outcome: "BVM ventilations bring the SpO2 up to 90%. The PCA is stopped. Naloxone is in your hand.",
        intervention: "BVM ventilation · PCA stopped",
      },
      {
        rhythm: "sinus_brady",
        vitals: { hr: 54, spo2: 90, nibp: "98/60", rr: 6, etco2: null },
        scene: { cpr: false, pads: false, bvm: true, iv: true, meds: true, loc: "ALTERED", pulse: "PRESENT", breathing: "ASSISTED" },
        narrative: "Ventilations continue. Time for the reversal agent.",
        question: "How is naloxone given for this post-op patient?",
        choices: [
          "IV per protocol — commonly 0.4 mg, or smaller titrated doses in opioid-tolerant post-op patients to restore breathing without a pain crisis — repeated every 2–3 minutes as needed while ventilating",
          "Flumazenil 0.2 mg IV",
          "Naloxone only if the patient loses a pulse",
          "A single large dose and stop ventilating",
        ],
        answer: 0,
        rationale:
          "The goal is adequate breathing, not full wakefulness. Titrating naloxone avoids abrupt withdrawal, severe pain, and a hypertensive surge. Flumazenil reverses benzodiazepines, not opioids. Ventilation continues until the patient breathes adequately.",
        outcome: "Before the naloxone takes effect, the pulse fades. The monitor shows a slow, wide rhythm — and no pulse.",
        intervention: "Naloxone IV",
      },
      {
        rhythm: "idioventricular",
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: 12 },
        scene: { cpr: true, pads: true, bvm: true, iv: true, meds: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative: "Pulseless with an organized rhythm — hypoxic PEA arrest.",
        question: "What now takes priority?",
        choices: [
          "Standard ACLS: high-quality CPR with ventilations and epinephrine 1 mg as soon as possible; naloxone doesn't replace CPR",
          "Give more naloxone and wait for it to work before starting CPR",
          "Defibrillate the wide rhythm",
          "Ventilate only — compressions aren't needed in an opioid arrest",
        ],
        answer: 0,
        rationale:
          "Once there's no pulse, it's a cardiac arrest: CPR with good ventilation (hypoxia caused this), and early epinephrine for PEA. Naloxone may be given but never delays or replaces CPR. PEA is not shockable.",
        outcome: "Two cycles of CPR with good ventilation and a dose of epinephrine — ROSC. Minutes later the patient starts breathing on their own.",
        intervention: "CPR + epinephrine → ROSC",
      },
      {
        rhythm: "sinus_tach",
        vitals: { hr: 108, spo2: 95, nibp: "124/78", rr: 14, etco2: 38 },
        scene: { cpr: false, pads: true, bvm: false, iv: true, meds: false, loc: "ALTERED", pulse: "PRESENT", breathing: "SPONTANEOUS" },
        narrative: "Breathing spontaneously and starting to rouse.",
        question: "What is the key monitoring concern over the next few hours?",
        choices: [
          "Re-sedation: naloxone wears off before many opioids — use continuous pulse oximetry and capnography, reassess often, and anticipate repeat doses or an infusion",
          "None — one naloxone dose permanently reverses the opioid",
          "Restart the PCA at the same settings once awake",
          "Remove all monitoring so the patient can sleep",
        ],
        answer: 0,
        rationale:
          "Naloxone's duration (roughly 30–90 minutes) is shorter than many opioids', so respiratory depression can come back. Continuous capnography catches hypoventilation earlier than SpO2. The opioid regimen must be re-evaluated before any restart.",
        outcome: "Transferred to step-down on capnography with a revised pain plan. The PCA settings go to the pharmacy safety committee.",
        intervention: "Capnography · re-sedation watch",
      },
    ],
  },

  {
    id: "massive-pe",
    title: "Post-op day 3 collapse",
    blurb: "Sudden dyspnea and shock after hip surgery. Think of the clot, then run a PEA arrest that targets it.",
    stages: [
      {
        rhythm: "sinus_tach",
        vitals: { hr: 128, spo2: 86, nibp: "86/52", rr: 30, etco2: null },
        scene: { cpr: false, pads: false, bvm: false, iv: true, meds: false, loc: "ALTERED", pulse: "WEAK", breathing: "SPONTANEOUS" },
        narrative:
          "Post-op day 3 after hip replacement, a 72-year-old suddenly becomes short of breath with pleuritic chest pain. SpO2 86%, HR 128, BP 86/52, distended neck veins, clear lungs.",
        question: "What is the most likely problem, and what do you do?",
        choices: [
          "Massive pulmonary embolism — call a rapid response, give oxygen, apply pads, and alert the team that reperfusion (thrombolytics or embolectomy) may be needed",
          "Anxiety — reassurance and a benzodiazepine",
          "Pneumonia — send a sputum culture and start antibiotics in the morning",
          "Fluid overload — give furosemide 40 mg IV",
        ],
        answer: 0,
        rationale:
          "Sudden hypoxemia, tachycardia, hypotension, JVD, and clear lungs after orthopedic surgery point to a large PE obstructing right-heart outflow. Hypotension makes it 'massive' (high-risk) PE, which is a candidate for reperfusion therapy.",
        outcome: "The rapid response team arrives. A bedside echo shows a dilated right ventricle. Then the patient stops responding — no pulse, but the monitor still shows sinus tachycardia.",
        intervention: "Rapid response · suspected massive PE",
      },
      {
        rhythm: "sinus_tach",
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: 9 },
        scene: { cpr: true, pads: true, bvm: true, iv: true, meds: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative: "PEA arrest. CPR is underway; ETCO2 is only 9 despite good compressions.",
        question: "What is the management?",
        choices: [
          "Standard PEA care (CPR, epinephrine 1 mg every 3–5 minutes) PLUS treatment of the presumed cause: thrombolytic therapy or emergent embolectomy for suspected PE, per the team",
          "Defibrillate the fast rhythm",
          "Stop CPR for a CT scan to confirm the PE",
          "Atropine 1 mg for PEA",
        ],
        answer: 0,
        rationale:
          "Obstructive PEA needs its cause treated. For confirmed or strongly suspected PE causing arrest, thrombolytics (or surgical/catheter embolectomy) are reasonable. Nobody leaves for CT mid-arrest, PEA is not shockable, and atropine is not in the arrest algorithm.",
        outcome: "A thrombolytic is given during CPR. The team leader asks how long to keep going.",
        intervention: "Epinephrine · thrombolytic given",
      },
      {
        rhythm: "sinus_tach",
        vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0, etco2: 14 },
        scene: { cpr: true, pads: true, bvm: true, iv: true, meds: true, loc: "UNRESPONSIVE", pulse: "ABSENT", breathing: "ASSISTED" },
        narrative: "The thrombolytic is in. Still no pulse at the next check.",
        question: "How does the thrombolytic change the resuscitation?",
        choices: [
          "Continue high-quality CPR for a prolonged period — the drug needs time to break up the clot, so stopping early may waste its effect",
          "Stop CPR — compressions are contraindicated after thrombolytics",
          "Stop the resuscitation at the next pulse check if there's no pulse",
          "Switch to defibrillation every 2 minutes",
        ],
        answer: 0,
        rationale:
          "Once a thrombolytic is given in arrest, extended CPR gives the drug time to work. The bleeding risk from compressions is accepted; stopping early throws away the therapy.",
        outcome: "Twenty minutes later the ETCO2 climbs to 32 — the next check finds a pulse. ROSC.",
        intervention: "Prolonged CPR → ROSC",
      },
      {
        rhythm: "sinus_tach",
        vitals: { hr: 112, spo2: 93, nibp: "98/60", rr: 0, etco2: 35 },
        scene: { cpr: false, pads: true, bvm: true, iv: true, meds: false, loc: "UNRESPONSIVE", pulse: "PRESENT", breathing: "ASSISTED" },
        narrative: "ROSC, intubated, on a low-dose vasopressor, heading to the ICU.",
        question: "What nursing priorities follow thrombolysis?",
        choices: [
          "Bleeding surveillance: neuro checks, puncture and surgical sites, avoid new IM injections and non-compressible punctures — plus the standard post-ROSC bundle",
          "Start aspirin 325 mg and a heparin bolus immediately without an order",
          "No special precautions are needed after thrombolytics",
          "Insert a subclavian central line and an arterial line in the same hour, without extra precautions",
        ],
        answer: 0,
        rationale:
          "Thrombolytics bring a major bleeding risk — including intracranial hemorrhage — made worse by a fresh surgical site and CPR. Frequent neuro checks, site checks, and avoiding new invasive punctures are essential, alongside standard post-cardiac-arrest care.",
        outcome: "Neuro checks stay stable. The hip wound oozes but holds. Extubated on day 2.",
        intervention: "Post-lytic bleeding precautions",
      },
    ],
  },
];

window.EKG_SCENARIOS = EKG_SCENARIOS;
