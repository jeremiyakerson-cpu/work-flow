/*
 * EKG question bank for the Zoll monitor trainer (quizzes draw from it).
 * category: "rhythm" | "meds" | "condition" | "code"
 * rhythm: key into EkgRhythms.synthesizeRhythm (waveform shown on the monitor)
 * vitals: what the monitor readout displays for this stem
 * shockable: only set on "code" questions, used for the results breakdown
 */

const EKG_QUESTIONS = [
  // ---------- RHYTHM IDENTIFICATION ----------
  {
    id: 1,
    category: "rhythm",
    rhythm: "nsr",
    vitals: { hr: 78, spo2: 98, nibp: "118/74", rr: 16 },
    stem: "Your patient is stable and asymptomatic. What rhythm is displayed on the monitor?",
    choices: ["Normal sinus rhythm", "Sinus tachycardia", "Junctional rhythm", "First-degree AV block"],
    answer: 0,
    rationale:
      "Regular rate 60–100, a P wave before every QRS, and a normal PR interval (0.12–0.20s) define normal sinus rhythm.",
  },
  {
    id: 2,
    category: "rhythm",
    rhythm: "sinus_brady",
    vitals: { hr: 44, spo2: 97, nibp: "112/70", rr: 14 },
    stem: "The patient is asymptomatic with this rhythm on the monitor. What is it?",
    choices: ["Sinus bradycardia", "Junctional rhythm", "Third-degree AV block", "Sinus arrest"],
    answer: 0,
    rationale:
      "A P wave precedes every QRS with a normal, constant PR interval — the only abnormality is a regular rate under 60, which is sinus bradycardia.",
  },
  {
    id: 3,
    category: "rhythm",
    rhythm: "afib",
    vitals: { hr: 132, spo2: 96, nibp: "104/68", rr: 20 },
    stem: "The rhythm below is irregularly irregular with no discernible P waves. What is it?",
    choices: ["Atrial fibrillation", "Atrial flutter", "Multifocal atrial tachycardia", "SVT"],
    answer: 0,
    rationale:
      "A chaotic fibrillatory baseline with no organized P waves and an irregularly irregular QRS rhythm is the hallmark of atrial fibrillation.",
  },
  {
    id: 4,
    category: "rhythm",
    rhythm: "aflutter",
    vitals: { hr: 75, spo2: 97, nibp: "116/72", rr: 16 },
    stem: "This strip shows a classic 'sawtooth' baseline with a regular ventricular rate of 75 (4:1 conduction). What is it?",
    choices: ["Atrial flutter", "Atrial fibrillation", "Sinus tachycardia with artifact", "Ventricular tachycardia"],
    answer: 0,
    rationale:
      "Regular sawtooth flutter waves at ~300/min with a fixed conduction ratio to the ventricles (here 4:1) is atrial flutter.",
  },
  {
    id: 5,
    category: "rhythm",
    rhythm: "svt",
    vitals: { hr: 188, spo2: 97, nibp: "102/66", rr: 20 },
    stem:
      "The patient reports palpitations that started suddenly a few minutes ago. The monitor shows a narrow-complex, perfectly regular rhythm at 188 with no visible P waves. What is it?",
    choices: ["Supraventricular tachycardia (SVT)", "Sinus tachycardia", "Ventricular tachycardia", "Atrial fibrillation with RVR"],
    answer: 0,
    rationale:
      "An abrupt onset, narrow QRS, regular rate well above the usual sinus ceiling, and absent P waves point to SVT rather than a gradual sinus tachycardia.",
  },
  {
    id: 6,
    category: "rhythm",
    rhythm: "mobitz1",
    vitals: { hr: 58, spo2: 97, nibp: "110/68", rr: 16 },
    stem: "The PR interval on this strip progressively lengthens until one P wave is not followed by a QRS, then the pattern repeats. What is this rhythm?",
    choices: [
      "Second-degree AV block, Mobitz I (Wenckebach)",
      "Second-degree AV block, Mobitz II",
      "Third-degree AV block",
      "First-degree AV block",
    ],
    answer: 0,
    rationale:
      "Progressive PR lengthening culminating in a dropped QRS, in a repeating group-beat pattern, is Mobitz I (Wenckebach).",
  },
  {
    id: 7,
    category: "rhythm",
    rhythm: "mobitz2",
    vitals: { hr: 52, spo2: 96, nibp: "104/64", rr: 16 },
    stem: "The PR interval here is constant, but every third P wave fails to conduct to the ventricles. What is this rhythm?",
    choices: [
      "Second-degree AV block, Mobitz II",
      "Second-degree AV block, Mobitz I",
      "First-degree AV block",
      "Complete heart block",
    ],
    answer: 0,
    rationale:
      "A fixed PR interval with intermittent, unpredictable dropped QRS complexes (no progressive lengthening) is Mobitz II — higher risk of progressing to complete block.",
  },
  {
    id: 8,
    category: "rhythm",
    rhythm: "avb3",
    vitals: { hr: 36, spo2: 94, nibp: "82/54", rr: 18 },
    stem: "P waves and QRS complexes are both regular, but they march through the strip completely independent of one another. What is this rhythm?",
    choices: [
      "Third-degree (complete) AV block",
      "Mobitz II second-degree block",
      "Atrial fibrillation",
      "Sinus arrhythmia",
    ],
    answer: 0,
    rationale:
      "Complete AV dissociation — atrial and ventricular rates each regular but unrelated to each other — defines third-degree (complete) heart block.",
  },
  {
    id: 9,
    category: "rhythm",
    rhythm: "vt",
    vitals: { hr: 178, spo2: 91, nibp: "88/56", rr: 22 },
    stem: "This is a regular, wide-complex tachycardia with uniform QRS morphology beat to beat and no visible P waves. What is it?",
    choices: [
      "Monomorphic ventricular tachycardia",
      "SVT with aberrant conduction",
      "Torsades de pointes",
      "Ventricular fibrillation",
    ],
    answer: 0,
    rationale:
      "Wide, uniform (monomorphic) QRS complexes at a fast, regular rate without P waves is ventricular tachycardia — treat as VT until proven otherwise.",
  },
  {
    id: 10,
    category: "rhythm",
    rhythm: "torsades",
    vitals: { hr: 220, spo2: 89, nibp: "76/48", rr: 24 },
    stem: "This wide-complex tachycardia has a QRS amplitude that appears to twist around the baseline. What is it?",
    choices: ["Torsades de pointes", "Monomorphic ventricular tachycardia", "Coarse ventricular fibrillation", "Atrial flutter"],
    answer: 0,
    rationale:
      "A polymorphic, wide-complex tachycardia with QRS amplitude that twists around the isoelectric line is the classic appearance of torsades de pointes, typically associated with a prolonged QT.",
  },

  // ---------- MEDICATIONS ----------
  {
    id: 11,
    category: "meds",
    rhythm: "svt",
    vitals: { hr: 188, spo2: 97, nibp: "108/68", rr: 18 },
    stem:
      "Same SVT patient, now confirmed stable (alert, BP 108/68, mild palpitations only). What is the first-line pharmacologic treatment?",
    choices: [
      "Adenosine 6 mg rapid IV push, followed by a rapid saline flush",
      "Amiodarone 150 mg IV over 10 minutes",
      "Synchronized cardioversion before any medication",
      "Epinephrine 1 mg IV push",
    ],
    answer: 0,
    rationale:
      "For stable SVT, adenosine 6 mg rapid IV push (followed immediately by a saline flush, then 12 mg if needed) is first-line — it transiently blocks the AV node to interrupt the reentry circuit.",
  },
  {
    id: 12,
    category: "meds",
    rhythm: "vt",
    vitals: { hr: 178, spo2: 94, nibp: "96/60", rr: 18 },
    stem: "This monomorphic VT patient still has a pulse and is stable (talking, BP 96/60). What is the first-line drug therapy?",
    choices: [
      "Amiodarone 150 mg IV over 10 minutes",
      "Adenosine 6 mg IV push",
      "Unsynchronized defibrillation",
      "Atropine 1 mg IV push",
    ],
    answer: 0,
    rationale:
      "Stable, monomorphic wide-complex tachycardia with a pulse is treated pharmacologically first — amiodarone (or procainamide/sotalol per local protocol) — reserving cardioversion for instability.",
  },
  {
    id: 13,
    category: "meds",
    rhythm: "torsades",
    vitals: { hr: 220, spo2: 90, nibp: "80/50", rr: 22 },
    stem: "This patient has recurrent torsades de pointes and a known history of hypomagnesemia. What is the first-line treatment?",
    choices: [
      "Magnesium sulfate 1–2 g IV",
      "Amiodarone 300 mg IV push",
      "Calcium chloride 1 g IV",
      "Lidocaine 1–1.5 mg/kg IV",
    ],
    answer: 0,
    rationale:
      "IV magnesium sulfate is first-line for torsades de pointes, regardless of serum magnesium level — it suppresses the early afterdepolarizations that trigger torsades (it does not meaningfully shorten the QT itself). Pulseless torsades is defibrillated.",
  },
  {
    id: 14,
    category: "meds",
    rhythm: "vf_coarse",
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "During a cardiac arrest resuscitation, how is epinephrine dosed once IV/IO access is available?",
    choices: [
      "1 mg IV/IO every 3–5 minutes",
      "1 mg IV once only, for the entire code",
      "0.5 mg IV every 10 minutes",
      "1 mg given intramuscularly",
    ],
    answer: 0,
    rationale:
      "ACLS dosing is epinephrine 1 mg IV/IO every 3–5 minutes throughout the arrest, alternating with rhythm checks and continued high-quality CPR.",
  },
  {
    id: 15,
    category: "meds",
    rhythm: "sinus_brady",
    vitals: { hr: 38, spo2: 93, nibp: "78/48", rr: 14 },
    stem: "This patient is symptomatic — dizzy and hypotensive — with this bradycardic rhythm. What is the first-line medication?",
    choices: [
      "Atropine 1 mg IV push",
      "Adenosine 6 mg IV push",
      "Amiodarone 150 mg IV",
      "Start an epinephrine infusion before anything else",
    ],
    answer: 0,
    rationale:
      "For symptomatic bradycardia, atropine 1 mg IV (repeatable to a max of 3 mg) is first-line; if ineffective, move to transcutaneous pacing or a dopamine/epinephrine infusion.",
  },
  {
    id: 16,
    category: "meds",
    rhythm: "svt",
    vitals: { hr: 190, spo2: 94, nibp: "72/44", rr: 24 },
    stem: "Which situation calls for SYNCHRONIZED cardioversion rather than defibrillation?",
    choices: [
      "An unstable tachyarrhythmia with a pulse (e.g., unstable SVT or unstable VT with a pulse)",
      "Pulseless ventricular fibrillation",
      "Pulseless ventricular tachycardia",
      "Asystole",
    ],
    answer: 0,
    rationale:
      "Synchronized cardioversion times the shock to the R wave and is used for unstable organized rhythms that still have a pulse. Pulseless VF/VT are shocked unsynchronized (defibrillation); asystole is never shocked.",
  },

  // ---------- CHANGE IN CONDITION ----------
  {
    id: 17,
    category: "condition",
    rhythm: "vf_coarse",
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem:
      "You are at the bedside when the monitor alarms. Your patient is unresponsive with no palpable pulse and the rhythm shown appears. What is your first action?",
    choices: [
      "Begin high-quality CPR immediately and call for the defibrillator / code team",
      "Give amiodarone IV push before starting compressions",
      "Attempt vagal maneuvers",
      "Wait one minute and recheck the rhythm before doing anything",
    ],
    answer: 0,
    rationale:
      "Unresponsive + pulseless = start CPR immediately and activate the code/get the defibrillator. Coarse VF is shockable, but compressions start first while the defibrillator is retrieved and charged.",
  },
  {
    id: 18,
    category: "condition",
    rhythm: "sinus_tach",
    vitals: { hr: 112, spo2: 95, nibp: "128/82", rr: 22 },
    stem:
      "Your previously stable patient suddenly reports crushing chest pain and becomes diaphoretic. The monitor still shows an organized sinus tachycardia. What should you do first?",
    choices: [
      "Obtain a stat 12-lead EKG, notify the provider immediately, and prepare to activate the chest-pain/STEMI pathway",
      "Give PRN acetaminophen and reassess in an hour",
      "Turn up the oxygen only and chart the complaint for later",
      "Give a full-dose aspirin on your own without notifying anyone",
    ],
    answer: 0,
    rationale:
      "New chest pain with diaphoresis is a change in condition requiring an immediate 12-lead EKG and provider notification — time to intervention drives outcomes in ACS.",
  },
  {
    id: 19,
    category: "condition",
    rhythm: "vt",
    vitals: { hr: 178, spo2: 96, nibp: "100/62", rr: 18 },
    stem:
      "The monitor rhythm suddenly changes to what looks like ventricular tachycardia, but your patient is awake, talking to you, with a BP of 100/62. What is the most appropriate immediate nursing action?",
    choices: [
      "Assess the patient directly — level of consciousness, pulse, and blood pressure — before treating the monitor",
      "Defibrillate immediately",
      "Give adenosine immediately",
      "Silence the alarm and continue routine charting",
    ],
    answer: 0,
    rationale:
      "Always treat the patient, not the monitor. A wide-complex rhythm with a clearly alert, perfusing patient is 'stable VT' and is managed differently (pharmacologic first) than a pulseless patient.",
  },
  {
    id: 20,
    category: "condition",
    rhythm: "sinus_brady",
    vitals: { hr: 32, spo2: 90, nibp: "76/40", rr: 16 },
    stem:
      "Your patient's rate drops to this bradycardia with altered mental status and hypotension. Atropine has already been given without effect. What should you anticipate next?",
    choices: [
      "Transcutaneous pacing and/or a dopamine or epinephrine infusion",
      "Immediate defibrillation",
      "Adenosine 12 mg IV push",
      "Synchronized cardioversion at 50 J",
    ],
    answer: 0,
    rationale:
      "When atropine fails for symptomatic, unstable bradycardia, the ACLS bradycardia algorithm moves to transcutaneous pacing and/or a chronotropic infusion (dopamine or epinephrine) — cardioversion and defibrillation are for tachyarrhythmias, not bradycardia.",
  },
  {
    id: 21,
    category: "condition",
    rhythm: "nsr",
    vitals: { hr: 96, spo2: 93, nibp: "82/50", rr: 18 },
    stem:
      "Return of spontaneous circulation (ROSC) has just occurred — the rhythm is organized and a pulse is present, but the BP is 82/50. What is the priority now?",
    choices: [
      "Begin post-ROSC care: optimize oxygenation/ventilation, treat hypotension, obtain a 12-lead, and consider targeted temperature management",
      "Immediately resume chest compressions",
      "Give another round of epinephrine push-dose immediately",
      "Discontinue monitoring since the rhythm has normalized",
    ],
    answer: 0,
    rationale:
      "Once ROSC is achieved, care shifts to the post-cardiac-arrest bundle — airway/ventilation optimization, hemodynamic support for hypotension, a 12-lead to look for a cause, and targeted temperature management — while monitoring closely for re-arrest.",
  },

  // ---------- CODE RHYTHMS ----------
  {
    id: 22,
    category: "code",
    rhythm: "asystole",
    shockable: false,
    vitals: { hr: 0, spo2: null, nibp: "--/--", rr: 0 },
    stem: "This flatline rhythm is confirmed in more than one lead on a pulseless patient. What is the correct management?",
    choices: [
      "Non-shockable — continue high-quality CPR and give epinephrine per the ACLS algorithm",
      "Defibrillate immediately at 200 J",
      "Synchronized cardioversion",
      "Give adenosine 6 mg IV push",
    ],
    answer: 0,
    rationale:
      "Asystole is a non-shockable rhythm. Management is uninterrupted high-quality CPR, epinephrine every 3–5 minutes, and searching for a reversible cause (the H's and T's) — never defibrillate a flatline.",
  },
  {
    id: 23,
    category: "code",
    rhythm: "vf_coarse",
    shockable: true,
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "Which rhythms are considered 'shockable' in the ACLS cardiac arrest algorithm?",
    choices: [
      "Pulseless ventricular tachycardia and ventricular fibrillation",
      "Asystole and PEA",
      "Sinus bradycardia and junctional rhythm",
      "First- and second-degree AV block",
    ],
    answer: 0,
    rationale:
      "Only pulseless VT and VF are shockable in cardiac arrest. Asystole and PEA are non-shockable and are managed with CPR, epinephrine, and reversible-cause identification instead.",
  },
  {
    id: 24,
    category: "code",
    rhythm: "nsr",
    shockable: false,
    vitals: { hr: 82, spo2: null, nibp: "--/--", rr: 0 },
    stem:
      "The monitor shows this organized, narrow-complex rhythm — but the patient is unresponsive with no palpable pulse and no respirations. What does this represent, and what is the priority action?",
    choices: [
      "Pulseless electrical activity (PEA) — start CPR immediately; an organized tracing does not mean a perfusing rhythm",
      "A perfusing sinus rhythm requiring no action",
      "Asystole requiring immediate defibrillation",
      "Lead artifact — reapply the electrodes and reassess in five minutes",
    ],
    answer: 0,
    rationale:
      "An organized rhythm on the monitor without a palpable pulse is PEA. It is non-shockable — treat with immediate high-quality CPR, epinephrine, and a rapid search for a reversible cause (H's and T's).",
  },
  {
    id: 25,
    category: "code",
    rhythm: "vf_fine",
    shockable: true,
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "Which of the following reflects a high-quality CPR practice per current ACLS guidelines?",
    choices: [
      "Minimize interruptions to compressions, aim for a chest-compression fraction of at least 60% (ideally higher), and rotate compressors every 2 minutes to prevent fatigue",
      "Pause compressions for up to 60 seconds before every rhythm check",
      "One person performs compressions for the entire code without rotating",
      "Check for a pulse before every single shock is delivered, pausing compressions each time",
    ],
    answer: 0,
    rationale:
      "High-quality CPR means minimizing interruptions (rhythm/pulse checks kept under ~10 seconds), a high compression fraction, adequate rate/depth with full recoil, and rotating compressors every 2 minutes to avoid fatigue-related decay in compression quality.",
  },

  // ---------- EXPANDED BANK: MORE RHYTHM IDENTIFICATION ----------
  {
    id: 26,
    category: "rhythm",
    rhythm: "avb1",
    vitals: { hr: 70, spo2: 98, nibp: "122/76", rr: 14 },
    stem: "Every P wave on this strip is followed by a QRS, but the PR interval is constant at 0.32 seconds. What is this rhythm?",
    choices: [
      "First-degree AV block",
      "Second-degree AV block, Mobitz I",
      "Junctional rhythm",
      "Normal sinus rhythm",
    ],
    answer: 0,
    rationale:
      "A PR interval longer than 0.20 seconds that stays constant, with every P conducted, is first-degree AV block. It usually needs no treatment — just monitoring and a review of AV-nodal-blocking medications.",
  },
  {
    id: 27,
    category: "rhythm",
    rhythm: "junctional",
    vitals: { hr: 48, spo2: 96, nibp: "108/66", rr: 14 },
    stem: "This rhythm is regular at 48 with narrow QRS complexes and no visible P waves. What is it?",
    choices: [
      "Junctional escape rhythm",
      "Sinus bradycardia",
      "Idioventricular rhythm",
      "Fine ventricular fibrillation",
    ],
    answer: 0,
    rationale:
      "A regular, narrow-complex rhythm at 40–60 with absent (or inverted/retrograde) P waves is a junctional escape rhythm — the AV junction has taken over as pacemaker. A ventricular escape would be wide and slower (20–40).",
  },
  {
    id: 28,
    category: "rhythm",
    rhythm: "pvc_bigeminy",
    vitals: { hr: 76, spo2: 97, nibp: "118/72", rr: 16 },
    stem: "On this strip, every other beat is a wide, early complex without a preceding P wave. What is this pattern called?",
    choices: [
      "Ventricular bigeminy (PVC every other beat)",
      "Atrial fibrillation",
      "Second-degree AV block, Mobitz II",
      "Ventricular tachycardia",
    ],
    answer: 0,
    rationale:
      "A premature ventricular complex alternating with every normal sinus beat is ventricular bigeminy. Check electrolytes (especially potassium and magnesium), oxygenation, and ischemia — frequent PVCs can herald something more serious.",
  },
  {
    id: 29,
    category: "rhythm",
    rhythm: "vf_fine",
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "Your pulseless patient's monitor shows low-amplitude, chaotic undulations with no organized complexes. What is this rhythm?",
    choices: [
      "Fine ventricular fibrillation",
      "Asystole",
      "Artifact from a loose lead",
      "Atrial fibrillation",
    ],
    answer: 0,
    rationale:
      "Chaotic, disorganized electrical activity with no identifiable QRS complexes is ventricular fibrillation; low amplitude makes it 'fine' VF. It is still shockable — don't mistake it for asystole. Confirm in a second lead and increase the gain if unsure.",
  },
  {
    id: 30,
    category: "rhythm",
    rhythm: "sinus_tach",
    vitals: { hr: 122, spo2: 96, nibp: "112/70", rr: 20 },
    stem:
      "A febrile post-op patient has this rhythm at 122. It sped up gradually over the last hour, and each QRS has a visible preceding P wave. What is it?",
    choices: [
      "Sinus tachycardia",
      "SVT",
      "Atrial flutter with 2:1 conduction",
      "Ventricular tachycardia",
    ],
    answer: 0,
    rationale:
      "Gradual onset, visible P waves before each QRS, and an identifiable trigger (fever, pain, hypovolemia) point to sinus tachycardia. Treat the underlying cause — don't give adenosine to a sinus tach.",
  },

  // ---------- EXPANDED BANK: MORE MEDICATIONS ----------
  {
    id: 31,
    category: "meds",
    rhythm: "afib",
    vitals: { hr: 142, spo2: 96, nibp: "118/74", rr: 18 },
    stem:
      "A stable patient is in atrial fibrillation with a rapid ventricular response of 142. Which medication is a first-line choice for rate control?",
    choices: [
      "Diltiazem IV (e.g., 0.25 mg/kg over 2 minutes)",
      "Adenosine 6 mg rapid IV push",
      "Epinephrine 1 mg IV push",
      "Atropine 1 mg IV push",
    ],
    answer: 0,
    rationale:
      "Rate control for stable afib with RVR is typically a calcium channel blocker (diltiazem) or beta blocker (metoprolol). Adenosine's effect is too transient to control afib, and it won't convert it.",
  },
  {
    id: 32,
    category: "meds",
    rhythm: "svt",
    vitals: { hr: 186, spo2: 97, nibp: "110/70", rr: 18 },
    stem: "Adenosine 6 mg was given for stable SVT with no effect. What is the next dose?",
    choices: [
      "Adenosine 12 mg rapid IV push with saline flush",
      "Adenosine 6 mg again, slowly this time",
      "Adenosine 3 mg as a maintenance infusion",
      "Skip straight to defibrillation",
    ],
    answer: 0,
    rationale:
      "The adenosine sequence is 6 mg, then 12 mg if the first dose fails — always as a rapid push through the closest possible IV site, followed immediately by a saline flush, because its half-life is only seconds.",
  },
  {
    id: 33,
    category: "meds",
    rhythm: "vf_coarse",
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "In refractory VF/pulseless VT (persisting after defibrillation), what is the initial amiodarone dose?",
    choices: [
      "300 mg IV/IO push",
      "150 mg IV over 10 minutes",
      "1 mg/min infusion only",
      "50 mg IV push",
    ],
    answer: 0,
    rationale:
      "In cardiac arrest, amiodarone is 300 mg IV/IO push (repeat 150 mg once if needed). The slower 150 mg over 10 minutes is the dose for stable VT with a pulse — a key distinction between the arrest and non-arrest doses.",
  },
  {
    id: 34,
    category: "meds",
    rhythm: "vf_coarse",
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "If amiodarone is unavailable during a VF arrest, what is the alternative antiarrhythmic and its initial dose?",
    choices: [
      "Lidocaine 1–1.5 mg/kg IV/IO",
      "Diltiazem 20 mg IV",
      "Magnesium 4 g IV push",
      "Procainamide 100 mg/min until conversion",
    ],
    answer: 0,
    rationale:
      "Lidocaine 1–1.5 mg/kg IV/IO is the accepted alternative to amiodarone in VF/pVT arrest (may repeat 0.5–0.75 mg/kg). Magnesium in arrest is reserved for torsades.",
  },
  {
    id: 35,
    category: "meds",
    rhythm: "sinus_brady",
    vitals: { hr: 36, spo2: 92, nibp: "78/46", rr: 14 },
    stem:
      "Atropine and transcutaneous pacing have failed to stabilize this symptomatic bradycardia. Which infusion is appropriate next per the ACLS bradycardia algorithm?",
    choices: [
      "Dopamine 5–20 mcg/kg/min or epinephrine 2–10 mcg/min",
      "Amiodarone 1 mg/min",
      "Adenosine drip at 6 mg/hr",
      "Diltiazem 5–15 mg/hr",
    ],
    answer: 0,
    rationale:
      "When atropine and pacing fail (or pacing isn't tolerated), the algorithm calls for a chronotropic infusion: dopamine 5–20 mcg/kg/min or epinephrine 2–10 mcg/min, while arranging transvenous pacing.",
  },
  {
    id: 36,
    category: "meds",
    rhythm: "sinus_tach",
    vitals: { hr: 110, spo2: 95, nibp: "84/52", rr: 20 },
    stem: "Your chest-pain patient is due for nitroglycerin. Which finding makes you HOLD the dose and call the provider?",
    choices: [
      "Systolic BP of 84 mmHg (hypotension) — or recent sildenafil/tadalafil use, or suspected RV infarction",
      "Heart rate of 90",
      "Pain rated 6/10",
      "History of hyperlipidemia",
    ],
    answer: 0,
    rationale:
      "Nitroglycerin is held for hypotension (SBP < 90 or a significant drop from baseline), recent phosphodiesterase-5 inhibitor use, and suspected right ventricular infarction — all can cause catastrophic drops in preload and blood pressure.",
  },
  {
    id: 37,
    category: "meds",
    rhythm: "nsr",
    vitals: { hr: 92, spo2: 96, nibp: "132/84", rr: 18 },
    stem: "For a patient with suspected ACS and no contraindications, how is aspirin given?",
    choices: [
      "162–325 mg non-enteric-coated, chewed",
      "81 mg swallowed whole with food",
      "650 mg rectally as first-line",
      "Aspirin is contraindicated in ACS",
    ],
    answer: 0,
    rationale:
      "Chewing 162–325 mg of non-enteric-coated aspirin achieves rapid platelet inhibition in suspected ACS. The 81 mg enteric-coated daily dose is for maintenance, not the acute event.",
  },
  {
    id: 38,
    category: "meds",
    rhythm: "wpw_afib",
    vitals: { hr: 230, spo2: 95, nibp: "112/68", rr: 18 },
    stem:
      "A patient has an irregular, wide-complex tachycardia suspected to be atrial fibrillation with WPW (pre-excitation). Which drugs must be AVOIDED?",
    choices: [
      "AV-nodal blockers — adenosine, diltiazem/verapamil, beta blockers, and digoxin",
      "Procainamide",
      "Urgent cardiology consultation",
      "Synchronized cardioversion",
    ],
    answer: 0,
    rationale:
      "In pre-excited afib, blocking the AV node shunts conduction down the accessory pathway and can accelerate the rhythm into VF. Avoid adenosine, calcium channel blockers, beta blockers, and digoxin (current guidelines also caution against IV amiodarone here). Procainamide if stable, synchronized cardioversion if unstable.",
  },
  {
    id: 39,
    category: "meds",
    rhythm: "torsades",
    vitals: { hr: 210, spo2: 91, nibp: "84/50", rr: 22 },
    stem: "A patient keeps having runs of torsades de pointes. Beyond magnesium, which underlying problems should you correct?",
    choices: [
      "Hypokalemia and QT-prolonging medications (review and stop them)",
      "Hyperkalemia and excess IV fluids",
      "Hypernatremia and hyperglycemia",
      "Nothing else — magnesium is the only intervention",
    ],
    answer: 0,
    rationale:
      "Torsades is driven by a prolonged QT. After magnesium, replace potassium to high-normal and stop QT-prolonging drugs (many antiemetics, antipsychotics, antibiotics, and methadone). Overdrive pacing or isoproterenol may be needed for recurrent runs.",
  },
  {
    id: 40,
    category: "meds",
    rhythm: "asystole",
    vitals: { hr: 0, spo2: null, nibp: "--/--", rr: 0 },
    stem: "Which statement about atropine in cardiac arrest is correct?",
    choices: [
      "Atropine is NOT part of the asystole/PEA algorithm — arrest management is CPR, epinephrine, and reversible causes",
      "Atropine 1 mg is given every 3–5 minutes in asystole",
      "Atropine replaces epinephrine in PEA",
      "Atropine is only used after defibrillation",
    ],
    answer: 0,
    rationale:
      "Atropine was removed from the pulseless-arrest algorithms — it belongs to the symptomatic bradycardia (with a pulse) algorithm. Asystole and PEA are managed with high-quality CPR, epinephrine every 3–5 minutes, and treating H's and T's.",
  },

  // ---------- EXPANDED BANK: MORE CHANGE IN CONDITION ----------
  {
    id: 41,
    category: "condition",
    rhythm: "hyperk",
    vitals: { hr: 58, spo2: 96, nibp: "128/78", rr: 16 },
    stem:
      "A dialysis patient who missed two treatments develops peaked T waves and a widening QRS on the monitor. Potassium returns at 7.8. What is the FIRST medication?",
    choices: [
      "IV calcium (gluconate or chloride) to stabilize the myocardium",
      "Insulin and dextrose before anything else",
      "Sodium polystyrene sulfonate (Kayexalate) alone",
      "Normal saline bolus only",
    ],
    answer: 0,
    rationale:
      "With EKG changes from hyperkalemia, IV calcium comes first — it stabilizes the cardiac membrane within minutes but doesn't lower potassium. Insulin/dextrose, albuterol, and bicarbonate then shift potassium intracellularly, and dialysis removes it.",
  },
  {
    id: 42,
    category: "condition",
    rhythm: "sinus_brady",
    vitals: { hr: 44, spo2: 96, nibp: "104/62", rr: 14 },
    stem:
      "A patient on digoxin reports nausea and 'yellow-green halos' around lights, and the monitor shows new bradycardia with frequent PVCs. What should you suspect and do?",
    choices: [
      "Digoxin toxicity — hold the dose, notify the provider, and send a dig level plus electrolytes (especially potassium)",
      "Normal digoxin side effects — give the next dose on time",
      "Anxiety — offer reassurance and a PRN anxiolytic",
      "Food poisoning — give an antiemetic and continue all medications",
    ],
    answer: 0,
    rationale:
      "GI upset, visual color disturbances, bradycardia, and ventricular ectopy are classic digoxin toxicity. Hold the drug, draw a level, and check potassium — hypokalemia dramatically worsens dig toxicity. Digoxin immune Fab is the antidote for severe cases.",
  },
  {
    id: 43,
    category: "condition",
    rhythm: "pacer_noncapture",
    vitals: { hr: 30, spo2: 93, nibp: "88/54", rr: 16 },
    stem:
      "You are transcutaneously pacing a patient in complete heart block, but the monitor shows pacer spikes that are not followed by QRS complexes. What is this, and what do you do?",
    choices: [
      "Failure to capture — increase the current (mA) until each spike produces a QRS, then confirm a matching pulse",
      "Normal pacing — document and continue",
      "Failure to sense — decrease the rate",
      "Oversensing — remove the pads and restart",
    ],
    answer: 0,
    rationale:
      "Spikes without QRS complexes mean the stimulus isn't capturing the myocardium. Increase output (mA) until electrical capture appears, then always verify mechanical capture by palpating a pulse that matches the paced rate.",
  },
  {
    id: 44,
    category: "condition",
    rhythm: "nsr",
    vitals: { hr: 96, spo2: null, nibp: "--/--", rr: 0 },
    stem:
      "During CPR, the end-tidal CO2 suddenly jumps from 14 to 42 mmHg. What does this most likely indicate?",
    choices: [
      "Return of spontaneous circulation — check for a pulse at the next rhythm check",
      "The ET tube has dislodged into the esophagus",
      "Compressions have become too deep",
      "The capnography sensor is failing",
    ],
    answer: 0,
    rationale:
      "An abrupt, sustained rise in ETCO2 during resuscitation is the earliest sign of ROSC — restored circulation suddenly delivers CO2 to the lungs. Confirm with a pulse check at the next scheduled rhythm check.",
  },
  {
    id: 45,
    category: "condition",
    rhythm: "vf_fine",
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "During CPR, the ETCO2 reads only 8 mmHg. What should the team do?",
    choices: [
      "Improve compression quality — check depth, rate, recoil, and compressor fatigue",
      "Stop compressions and recheck the pulse",
      "Hyperventilate the patient to raise the number",
      "Nothing — ETCO2 has no role during CPR",
    ],
    answer: 0,
    rationale:
      "ETCO2 below ~10 mmHg during CPR suggests compressions aren't generating adequate blood flow. Coach or swap the compressor, and reassess depth (2–2.4 in), rate (100–120), and full recoil. ETCO2 is a real-time gauge of CPR effectiveness.",
  },

  // ---------- EXPANDED BANK: MORE CODE MANAGEMENT ----------
  {
    id: 46,
    category: "code",
    rhythm: "nsr",
    shockable: false,
    vitals: { hr: 88, spo2: null, nibp: "--/--", rr: 0 },
    stem: "Your patient is in PEA. Which list correctly names reversible causes to search for (the H's and T's)?",
    choices: [
      "Hypovolemia, hypoxia, hydrogen ion (acidosis), hypo/hyperkalemia, hypothermia; tension pneumothorax, tamponade, toxins, thrombosis (pulmonary and coronary)",
      "Hypertension, hyperglycemia, headache; tremor, tinnitus, tachypnea",
      "Only hypovolemia and hypoxia — nothing else is reversible",
      "Fever, pain, anxiety, agitation",
    ],
    answer: 0,
    rationale:
      "The 5 H's and 5 T's: Hypovolemia, Hypoxia, Hydrogen ion (acidosis), Hypo-/Hyperkalemia, Hypothermia — Tension pneumothorax, Tamponade (cardiac), Toxins, Thrombosis-pulmonary (PE), Thrombosis-coronary (MI). PEA survival depends on finding and fixing the cause.",
  },
  {
    id: 47,
    category: "code",
    rhythm: "vf_coarse",
    shockable: true,
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "What are the correct compression targets for adult CPR?",
    choices: [
      "Rate 100–120/min, depth 2–2.4 inches (5–6 cm), full chest recoil between compressions",
      "Rate 60–80/min, depth 1 inch, lean on the chest between compressions",
      "Rate 140–160/min, as deep as possible",
      "Any rate, as long as ventilations are prioritized first",
    ],
    answer: 0,
    rationale:
      "Adult targets: 100–120 compressions per minute, 2–2.4 inches deep, full recoil (no leaning), on a firm surface, minimizing interruptions. Both too slow/shallow and too fast/deep reduce effectiveness.",
  },
  {
    id: 48,
    category: "code",
    rhythm: "asystole",
    shockable: false,
    vitals: { hr: 0, spo2: null, nibp: "--/--", rr: 0 },
    stem: "What is the correct compression-to-ventilation approach for an adult code?",
    choices: [
      "30:2 without an advanced airway; once intubated, continuous compressions with 1 breath every 6 seconds",
      "15:2 in all adults at all times",
      "5:1 with pauses for each breath after intubation",
      "Ventilate as fast as possible — more breaths mean better oxygenation",
    ],
    answer: 0,
    rationale:
      "Before an advanced airway: cycles of 30 compressions to 2 breaths. After intubation/supraglottic airway: continuous compressions with one breath every 6 seconds (10/min). Overventilation raises intrathoracic pressure and worsens survival.",
  },
  {
    id: 49,
    category: "code",
    rhythm: "vf_coarse",
    shockable: true,
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "How often are rhythm checks performed during a cardiac arrest, and how long may they last?",
    choices: [
      "Every 2 minutes, pausing compressions no longer than 10 seconds",
      "Every 30 seconds, taking as long as needed",
      "Every 10 minutes, for up to 1 minute",
      "Only after each medication is given",
    ],
    answer: 0,
    rationale:
      "Rhythm (and pulse, if organized) checks happen at 2-minute cycle changes and must keep the compression pause under 10 seconds. Swap compressors during the same pause to minimize hands-off time.",
  },
  {
    id: 50,
    category: "code",
    rhythm: "vt",
    shockable: true,
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "For a biphasic defibrillator, what is the appropriate initial energy for VF/pulseless VT?",
    choices: [
      "The manufacturer's recommended dose (typically 120–200 J); if unknown, use the maximum available, and consider escalating for subsequent shocks",
      "Always exactly 360 J monophasic-equivalent",
      "20 J, doubling with each shock",
      "50 J synchronized",
    ],
    answer: 0,
    rationale:
      "Biphasic defibrillation uses the device manufacturer's recommended energy (commonly 120–200 J); if unknown, use the maximum. Subsequent shocks may be equivalent or escalated. Shocks for VF/pVT are always unsynchronized.",
  },

  // ---------- EXPANDED BANK: ESCAPE, PRE-EXCITATION, PACED, ELECTROLYTE & STEMI PATTERNS ----------
  {
    id: 51,
    category: "rhythm",
    rhythm: "idioventricular",
    vitals: { hr: 34, spo2: 92, nibp: "84/50", rr: 16 },
    stem: "The monitor shows a regular rhythm at 34 with wide, bizarre QRS complexes and no P waves anywhere on the strip. What is it?",
    choices: [
      "Idioventricular (ventricular escape) rhythm",
      "Junctional escape rhythm",
      "Ventricular tachycardia",
      "Sinus bradycardia with a bundle branch block",
    ],
    answer: 0,
    rationale:
      "Wide QRS, no P waves, and a rate of 20–40 is the intrinsic ventricular pacemaker firing as a last resort — an idioventricular (ventricular escape) rhythm. A junctional escape would be narrow and faster (40–60).",
  },
  {
    id: 52,
    category: "meds",
    rhythm: "idioventricular",
    vitals: { hr: 32, spo2: 91, nibp: "80/48", rr: 16 },
    stem: "A colleague suggests lidocaine to 'get rid of' this slow, wide idioventricular rhythm. The patient has a weak pulse. What is the correct response?",
    choices: [
      "Do not suppress it — it is an escape rhythm and may be the only thing keeping the heart beating; treat as symptomatic bradycardia (atropine, prepare to pace) and find the cause",
      "Give lidocaine 1–1.5 mg/kg to abolish the ventricular focus",
      "Give amiodarone 300 mg IV push",
      "Defibrillate at 200 J",
    ],
    answer: 0,
    rationale:
      "Antiarrhythmics can abolish the ventricular escape focus and leave the patient in asystole. A slow escape rhythm with a pulse is managed with the bradycardia algorithm; without a pulse it is PEA.",
  },
  {
    id: 53,
    category: "rhythm",
    rhythm: "aivr",
    vitals: { hr: 72, spo2: 97, nibp: "116/70", rr: 16 },
    stem: "Minutes after the cath lab opened an occluded coronary artery, the patient develops a regular wide-complex rhythm at 72. The patient is awake with a normal BP. What is the most likely rhythm and response?",
    choices: [
      "Accelerated idioventricular rhythm (AIVR) — a usually benign reperfusion rhythm; monitor, assess perfusion, and report it",
      "Ventricular tachycardia — synchronized cardioversion now",
      "Complete heart block — start transcutaneous pacing",
      "Ventricular fibrillation — defibrillate",
    ],
    answer: 0,
    rationale:
      "A wide rhythm at about 40–100 after reperfusion is AIVR. It is typically self-limited and well tolerated, and it is not suppressed with antiarrhythmics. Rates over 100 would point toward VT.",
  },
  {
    id: 54,
    category: "rhythm",
    rhythm: "wpw",
    vitals: { hr: 80, spo2: 99, nibp: "118/72", rr: 14 },
    stem: "A 22-year-old with episodes of palpitations has this strip: a short PR interval, a slurred upstroke at the start of each QRS, and a slightly wide QRS. What does this show?",
    choices: [
      "Wolff-Parkinson-White (pre-excitation) pattern — a delta wave from an accessory pathway",
      "First-degree AV block",
      "Right bundle branch block",
      "Accelerated junctional rhythm",
    ],
    answer: 0,
    rationale:
      "The WPW triad is a short PR (< 0.12 s), a delta wave (slurred QRS upstroke), and a widened QRS — conduction reaches the ventricles early through an accessory pathway that bypasses the AV node.",
  },
  {
    id: 55,
    category: "condition",
    rhythm: "wpw",
    vitals: { hr: 80, spo2: 99, nibp: "120/74", rr: 14 },
    stem: "Your patient's admission EKG shows a WPW pattern. Why is this important to communicate at handoff?",
    choices: [
      "If the patient develops atrial fibrillation, AV-nodal blockers (adenosine, diltiazem, beta blockers, digoxin) can accelerate conduction down the accessory pathway and trigger VF",
      "The patient needs a permanent pacemaker before discharge",
      "WPW means digoxin is the preferred drug for any future tachycardia",
      "It is a benign normal variant with no treatment implications",
    ],
    answer: 0,
    rationale:
      "An accessory pathway changes which drugs are safe. In pre-excited afib, blocking the AV node forces impulses down the pathway at very high rates — a route to VF. Catheter ablation is the definitive treatment.",
  },
  {
    id: 56,
    category: "meds",
    rhythm: "wpw_afib",
    vitals: { hr: 240, spo2: 95, nibp: "108/68", rr: 20 },
    stem: "A stable young patient has an irregular, very fast, wide-complex rhythm with QRS shapes that vary beat to beat — suspected pre-excited atrial fibrillation. Which drug is appropriate?",
    choices: [
      "Procainamide IV (20–50 mg/min to a maximum of 17 mg/kg, stopping for hypotension or QRS widening > 50%)",
      "Diltiazem 0.25 mg/kg IV",
      "Adenosine 6 mg rapid IV push",
      "Metoprolol 5 mg IV",
    ],
    answer: 0,
    rationale:
      "Procainamide slows conduction over the accessory pathway itself. AV-nodal blockers (diltiazem, adenosine, beta blockers, digoxin) can speed the ventricular response and precipitate VF. If the patient becomes unstable: synchronized cardioversion.",
  },
  {
    id: 57,
    category: "rhythm",
    rhythm: "paced_v",
    vitals: { hr: 70, spo2: 97, nibp: "124/76", rr: 14 },
    stem: "A patient with a permanent pacemaker has this rhythm: a sharp spike before every wide QRS, regular at 70. What is it, and what else must you confirm?",
    choices: [
      "Ventricular paced rhythm with capture — confirm a palpable pulse that matches the paced rate",
      "Ventricular tachycardia — prepare to cardiovert",
      "Pacemaker failure to capture — increase the output",
      "Idioventricular rhythm — no action needed",
    ],
    answer: 0,
    rationale:
      "A spike followed by a wide QRS on every beat is ventricular pacing with electrical capture. The nurse still confirms mechanical capture — a pulse at the paced rate. Wide paced complexes are expected and are not VT.",
  },
  {
    id: 58,
    category: "rhythm",
    rhythm: "paced_av",
    vitals: { hr: 70, spo2: 98, nibp: "130/78", rr: 14 },
    stem: "This regular rhythm at 70 shows TWO spikes per beat — one before the P wave and one before the wide QRS. What is it?",
    choices: [
      "AV (dual-chamber) paced rhythm",
      "Ventricular paced rhythm only",
      "Atrial flutter with 2:1 conduction",
      "Pacemaker failure to sense",
    ],
    answer: 0,
    rationale:
      "An atrial spike before the P wave and a ventricular spike before the QRS on each beat is dual-chamber (AV) pacing, as with a DDD pacemaker.",
  },
  {
    id: 59,
    category: "rhythm",
    rhythm: "pacer_noncapture",
    vitals: { hr: 30, spo2: 91, nibp: "82/50", rr: 18 },
    stem: "A pacemaker patient becomes dizzy. The monitor shows regular pacer spikes at 70, but most are not followed by a QRS; a slow wide rhythm at about 30 shows through. What is this?",
    choices: [
      "Pacemaker failure to capture",
      "Pacemaker failure to sense (undersensing)",
      "Normal demand pacing",
      "Oversensing with inhibited pacing",
    ],
    answer: 0,
    rationale:
      "Spikes that arrive on time but produce no depolarization are failure to capture. Undersensing is spikes firing without regard to the patient's own beats; oversensing is spikes missing when they should fire. Notify the provider and be ready for transcutaneous pacing.",
  },
  {
    id: 60,
    category: "rhythm",
    rhythm: "hyperk",
    vitals: { hr: 58, spo2: 96, nibp: "134/80", rr: 16 },
    stem: "A patient with chronic kidney disease has this strip: tall, narrow, tented T waves, small P waves, and a QRS that is beginning to widen. What does it suggest?",
    choices: [
      "Hyperkalemia",
      "Hypokalemia",
      "Hypercalcemia",
      "Normal early repolarization",
    ],
    answer: 0,
    rationale:
      "Peaked T waves, flattening P waves, PR prolongation, and QRS widening are the progressive EKG signs of hyperkalemia. Hypokalemia does the opposite: flat T waves and prominent U waves.",
  },
  {
    id: 61,
    category: "condition",
    rhythm: "hyperk",
    vitals: { hr: 56, spo2: 96, nibp: "128/76", rr: 16 },
    stem: "Calcium, insulin with dextrose, and albuterol were given for a potassium of 7.2 with peaked T waves. The T waves have improved. What must the nurse monitor next?",
    choices: [
      "Blood glucose (hypoglycemia after insulin), serial potassium levels, and continuous telemetry — the shifted potassium rebounds until it is removed",
      "Nothing — the EKG has normalized, so the problem is solved",
      "Only urine output",
      "Stop telemetry and recheck the potassium in 24 hours",
    ],
    answer: 0,
    rationale:
      "Insulin and albuterol shift potassium into cells temporarily — levels climb again as they wear off unless potassium is removed (dialysis, diuretics, binders). Insulin also causes delayed hypoglycemia, so glucose is checked serially.",
  },
  {
    id: 62,
    category: "rhythm",
    rhythm: "stemi",
    vitals: { hr: 84, spo2: 95, nibp: "146/90", rr: 20 },
    stem: "A patient with chest pressure has this lead II strip: the ST segment is elevated and merges into the T wave. The 12-lead shows the same in II, III, and aVF with ST depression in aVL. What is this?",
    choices: [
      "Inferior STEMI (reciprocal change in aVL)",
      "Pericarditis",
      "Hyperkalemia",
      "Normal sinus rhythm",
    ],
    answer: 0,
    rationale:
      "ST elevation in the contiguous inferior leads (II, III, aVF) with reciprocal ST depression in aVL is an inferior STEMI, most often from the right coronary artery. Pericarditis elevates the ST diffusely without reciprocal depression.",
  },
  {
    id: 63,
    category: "condition",
    rhythm: "stemi",
    vitals: { hr: 88, spo2: 96, nibp: "92/60", rr: 20 },
    stem: "Your inferior-STEMI patient is still having chest pain; BP is 92/60 and right-sided lead V4R shows ST elevation. Nitroglycerin is ordered. What do you do?",
    choices: [
      "Hold the nitroglycerin and notify the provider — a right ventricular infarct is preload-dependent and nitrates can cause severe hypotension",
      "Give nitroglycerin 0.4 mg SL as ordered and recheck in 15 minutes",
      "Give three nitroglycerin doses back-to-back to relieve the pain quickly",
      "Start a nitroglycerin infusion instead",
    ],
    answer: 0,
    rationale:
      "ST elevation in V4R signals RV involvement. The RV depends on preload, and nitrates (which reduce preload) can crash the pressure — especially with an SBP already near 90. RV infarct hypotension is treated with IV fluids per the provider.",
  },
  {
    id: 64,
    category: "condition",
    rhythm: "stemi",
    vitals: { hr: 90, spo2: 97, nibp: "138/86", rr: 18 },
    stem: "An ED patient's 12-lead shows a STEMI. The hospital has a cath lab. What is the reperfusion time goal?",
    choices: [
      "Primary PCI within 90 minutes of first medical contact",
      "Within 6 hours — there is no rush once aspirin is given",
      "Within 24 hours after troponins trend",
      "Only after a stress test confirms ischemia",
    ],
    answer: 0,
    rationale:
      "For STEMI, the goal for primary PCI is first-medical-contact-to-device within 90 minutes. If PCI can't happen in time (about 120 minutes), fibrinolytics are given — ideally within 30 minutes of arrival — if there are no contraindications.",
  },
  {
    id: 65,
    category: "code",
    rhythm: "idioventricular",
    shockable: false,
    vitals: { hr: 30, spo2: null, nibp: "--/--", rr: 0 },
    stem: "The monitor shows a slow, wide, organized rhythm at 30, but the patient is unresponsive and has no palpable pulse. What is the correct management?",
    choices: [
      "PEA — start CPR, give epinephrine 1 mg IV/IO as soon as possible, and search for reversible causes (H's and T's)",
      "Defibrillate — wide complexes are shockable",
      "Atropine 1 mg IV and wait for the heart rate to rise",
      "Give amiodarone to suppress the ventricular rhythm",
    ],
    answer: 0,
    rationale:
      "Any organized rhythm without a pulse is PEA, which is not shockable. A slow, wide PEA often suggests hyperkalemia, acidosis, or massive MI — treat with CPR, early epinephrine, and cause-directed therapy.",
  },
  // ---------- PASS 2 (ids 66+): appended so saved-exam indices stay valid ----------
  {
    id: 66,
    category: "rhythm",
    rhythm: "aflutter",
    vitals: { hr: 75, spo2: 97, nibp: "118/72", rr: 16 },
    stem: "The sawtooth flutter waves on this strip run at about 300/min, and the ventricular rate is a regular 75. What is the conduction ratio?",
    choices: ["4:1", "2:1", "1:1", "Variable — the ventricular rhythm is irregularly irregular"],
    answer: 0,
    rationale:
      "Atrial rate ÷ ventricular rate = conduction ratio: 300 ÷ 75 = 4:1. A 2:1 flutter conducts near 150 — a rate that should always make you look for hidden flutter waves.",
  },
  {
    id: 67,
    category: "meds",
    rhythm: "aflutter",
    vitals: { hr: 76, spo2: 97, nibp: "122/76", rr: 16 },
    stem: "A patient in persistent atrial flutter is anticoagulated and sedated for an elective synchronized cardioversion. Per the 2025 AHA guidelines, what initial energy is reasonable on a biphasic defibrillator?",
    choices: [
      "200 J synchronized, increased if the first shock fails",
      "360 J unsynchronized",
      "2 J/kg unsynchronized",
      "Cardioversion is not used for flutter — give adenosine",
    ],
    answer: 0,
    rationale:
      "The 2025 AHA adult ALS guidelines state an initial 200 J may be reasonable for synchronized cardioversion of atrial flutter, escalating on failure depending on the device. Lower starting energies were more likely to need repeat shocks. Rhythms with a pulse are always shocked synchronized.",
  },
  {
    id: 68,
    category: "meds",
    rhythm: "afib",
    vitals: { hr: 134, spo2: 92, nibp: "74/42", rr: 24 },
    stem: "Your patient in atrial fibrillation with RVR is now hypotensive and altered. What initial synchronized cardioversion energy do the 2025 AHA guidelines support for afib on a biphasic device?",
    choices: [
      "At least 200 J, increased if the shock fails",
      "50 J — always start as low as possible",
      "200 J unsynchronized",
      "No shock — afib is treated with rate control only",
    ],
    answer: 0,
    rationale:
      "The 2025 AHA guidelines updated afib cardioversion: an initial energy of at least 200 J (biphasic) is reasonable, escalating on failure. In a network meta-analysis of more than 3,000 patients, 200 J shocks achieved over 90% cumulative success, and low-energy shocks were more likely to provoke VF.",
  },
  {
    id: 69,
    category: "condition",
    rhythm: "afib",
    vitals: { hr: 96, spo2: 97, nibp: "126/78", rr: 16 },
    stem: "A stable patient has been in atrial fibrillation for an unknown length of time. The team wants an elective cardioversion today. What must happen first?",
    choices: [
      "Address stroke risk: either ≥3 weeks of therapeutic anticoagulation or a TEE to exclude left atrial thrombus, with anticoagulation continued afterward",
      "Nothing — cardioversion is always safe in a stable patient",
      "Give adenosine to see whether it converts",
      "Check a troponin; if it is normal, cardiovert immediately",
    ],
    answer: 0,
    rationale:
      "Afib lasting more than 48 hours (or of unknown duration) can form left atrial thrombus, which cardioversion can dislodge and cause a stroke. The 2023 ACC/AHA/ACCP/HRS AF guideline calls for ≥3 weeks of anticoagulation or a TEE first, then anticoagulation afterward. An unstable patient is cardioverted immediately regardless.",
  },
  {
    id: 70,
    category: "condition",
    rhythm: "afib",
    vitals: { hr: 88, spo2: 97, nibp: "132/80", rr: 16 },
    stem: "A patient newly diagnosed with atrial fibrillation (now rate-controlled) asks why they need a 'blood thinner.' What is the main reason?",
    choices: [
      "Afib lets blood pool in the atria, where clots can form and travel to the brain — anticoagulation lowers the risk of stroke",
      "Anticoagulants slow the heart rate",
      "Anticoagulants convert afib back to sinus rhythm",
      "Only to prevent deep vein thrombosis in the legs",
    ],
    answer: 0,
    rationale:
      "Without organized atrial contraction, blood stagnates (especially in the left atrial appendage) and clots can embolize to the brain. The decision to anticoagulate is based on a stroke-risk score (such as CHA₂DS₂-VASc) weighed against bleeding risk.",
  },
  {
    id: 71,
    category: "rhythm",
    rhythm: "mobitz1",
    vitals: { hr: 58, spo2: 98, nibp: "124/76", rr: 14 },
    stem: "This Wenckebach (Mobitz I) pattern appeared on your asymptomatic patient overnight. Which statement is most accurate?",
    choices: [
      "The block is usually at the AV node — often benign, it typically responds to atropine if symptoms develop",
      "It is an infranodal block that commonly progresses to complete heart block without warning",
      "It requires immediate transcutaneous pacing",
      "It is a form of ventricular tachycardia",
    ],
    answer: 0,
    rationale:
      "Mobitz I is usually a nodal block (increased vagal tone, inferior MI, AV-nodal drugs). In an asymptomatic patient it is monitored and reported. Mobitz II is the infranodal block that can suddenly become complete heart block.",
  },
  {
    id: 72,
    category: "condition",
    rhythm: "mobitz2",
    vitals: { hr: 52, spo2: 97, nibp: "118/70", rr: 16 },
    stem: "Your patient is currently asymptomatic in Mobitz II second-degree block. What is the most appropriate nursing action?",
    choices: [
      "Notify the provider, apply transcutaneous pacing pads, and keep them connected — Mobitz II can progress to complete heart block",
      "Document it and recheck at the end of the shift",
      "Give the scheduled beta blocker",
      "Give atropine 1 mg to prevent progression",
    ],
    answer: 0,
    rationale:
      "Mobitz II is infranodal and unpredictable. Pads go on before they're needed, AV-nodal blockers are held, and the provider is told. Permanent pacing is generally indicated when there's no reversible cause (2018 ACC/AHA/HRS bradycardia guideline).",
  },
  {
    id: 73,
    category: "meds",
    rhythm: "avb3",
    vitals: { hr: 36, spo2: 91, nibp: "76/44", rr: 20 },
    stem: "Complete heart block with a wide ventricular escape at 36; the patient is hypotensive and dizzy. Atropine is ordered. What should you expect?",
    choices: [
      "It may not work, because the block is usually below the AV node — pacing (or a dopamine or epinephrine infusion) must not be delayed",
      "It will reliably restore 1:1 conduction",
      "It is contraindicated in every bradycardia",
      "It should be repeated every minute until the rate reaches 60",
    ],
    answer: 0,
    rationale:
      "Atropine acts on the SA and AV nodes. With an infranodal block and a wide escape, it often does nothing. The ACLS bradycardia algorithm allows atropine but moves straight to pacing or a chronotropic infusion when the patient stays unstable.",
  },
  {
    id: 74,
    category: "condition",
    rhythm: "avb3",
    vitals: { hr: 36, spo2: 94, nibp: "92/58", rr: 18 },
    stem: "Your patient in complete heart block is being paced transcutaneously with good capture. What is the expected plan for definitive treatment?",
    choices: [
      "Transcutaneous pacing is a bridge: transvenous pacing, then evaluation for a permanent pacemaker (unless a reversible cause is found)",
      "Continue transcutaneous pacing for several days",
      "Stop pacing once the BP improves",
      "Start amiodarone to control the ventricular escape",
    ],
    answer: 0,
    rationale:
      "Transcutaneous pacing is painful and temporary. The 2025 AHA guidelines note temporary transvenous pacing is reasonable for persistent unstable bradycardia refractory to medical therapy, as a bridge to fixing the cause or placing a permanent pacemaker.",
  },
  {
    id: 75,
    category: "rhythm",
    rhythm: "junctional",
    vitals: { hr: 48, spo2: 97, nibp: "112/68", rr: 14 },
    stem: "In a junctional rhythm, where might you find the P waves?",
    choices: [
      "Inverted just before the QRS, hidden inside it, or retrograde just after it",
      "Upright before every QRS with a normal PR",
      "As sawtooth waves at 300/min",
      "Marching independently of the QRS",
    ],
    answer: 0,
    rationale:
      "Junctional impulses travel backward into the atria, so in lead II the P is inverted and may land before, within, or after the QRS. A junctional escape fires at 40–60; accelerated junctional is 60–100 and junctional tachycardia is above 100.",
  },
  {
    id: 76,
    category: "condition",
    rhythm: "junctional",
    vitals: { hr: 46, spo2: 94, nibp: "80/48", rr: 18 },
    stem: "A patient in a junctional rhythm at 46 is pale, dizzy, and hypotensive. What is the first-line treatment?",
    choices: [
      "Atropine 1 mg IV (repeat every 3–5 minutes to 3 mg), while preparing for pacing or a chronotropic infusion",
      "Adenosine 6 mg rapid IV push",
      "Amiodarone 150 mg IV",
      "Synchronized cardioversion",
    ],
    answer: 0,
    rationale:
      "A symptomatic junctional rhythm is a symptomatic bradycardia. The ACLS bradycardia algorithm starts with atropine and moves to pacing or a dopamine or epinephrine infusion if it fails. Also look for a cause, such as digoxin toxicity or AV-nodal blockers.",
  },
  {
    id: 77,
    category: "condition",
    rhythm: "pvc_bigeminy",
    vitals: { hr: 84, spo2: 97, nibp: "128/78", rr: 16 },
    stem: "A post-op patient on IV furosemide develops new ventricular bigeminy. They feel fine. What is the most useful next step?",
    choices: [
      "Assess the patient, check potassium and magnesium, look for ischemia or hypoxia, and notify the provider",
      "Give lidocaine to suppress the PVCs",
      "Defibrillate",
      "Ignore it — PVCs are always benign",
    ],
    answer: 0,
    rationale:
      "New frequent PVCs signal an irritable ventricle. Common causes are low potassium or magnesium (loop diuretics), ischemia, hypoxia, and stimulants. Treat the cause. Routinely suppressing PVCs with antiarrhythmics isn't done.",
  },
  {
    id: 78,
    category: "rhythm",
    rhythm: "pvc_bigeminy",
    vitals: { hr: 82, spo2: 97, nibp: "120/76", rr: 16 },
    stem: "What feature distinguishes a PVC from a normally conducted beat on this strip?",
    choices: [
      "It is early, wide, and bizarre, with no preceding P wave, and is usually followed by a pause",
      "It is late and narrow with an upright P wave",
      "It has a shortened PR interval and a delta wave",
      "It is preceded by a pacer spike",
    ],
    answer: 0,
    rationale:
      "A PVC starts in the ventricle, so it arrives early, conducts slowly (wide QRS), has no P wave of its own, and its T wave usually points the opposite way. A pause usually follows. Every other beat being a PVC is bigeminy.",
  },
  {
    id: 79,
    category: "rhythm",
    rhythm: "vt",
    vitals: { hr: 160, spo2: 94, nibp: "98/60", rr: 22 },
    stem: "You can't tell whether this regular wide-complex tachycardia is VT or SVT with aberrancy. How should it be treated?",
    choices: [
      "Assume VT until proven otherwise and treat according to the patient's stability",
      "Assume SVT and give diltiazem",
      "Assume it's artifact and wait",
      "Give verapamil to see whether it slows",
    ],
    answer: 0,
    rationale:
      "Most regular wide-complex tachycardias are VT, especially in patients with heart disease. Treating VT as SVT with a calcium channel blocker can cause collapse. Treat it as VT: cardiovert if unstable, and give antiarrhythmics or get expert help if stable.",
  },
  {
    id: 80,
    category: "code",
    rhythm: "vt",
    shockable: true,
    vitals: { hr: 162, spo2: null, nibp: "--/--", rr: 0 },
    stem: "The monitor shows monomorphic VT, but your patient is unresponsive, not breathing, and has no pulse. What is the correct electrical therapy?",
    choices: [
      "Unsynchronized defibrillation, the same as VF, then immediately resume CPR",
      "Synchronized cardioversion at 100 J",
      "Transcutaneous pacing",
      "No shock — give amiodarone first",
    ],
    answer: 0,
    rationale:
      "Pulseless VT is managed with the VF algorithm: CPR plus unsynchronized defibrillation at the device's recommended energy. Synchronized cardioversion is only for rhythms that still have a pulse, and the sync can delay or fail to fire in pulseless rhythms.",
  },
  {
    id: 81,
    category: "meds",
    rhythm: "vt",
    vitals: { hr: 158, spo2: 96, nibp: "118/74", rr: 18 },
    stem: "A stable patient has a REGULAR, MONOMORPHIC wide-complex tachycardia. Which statement about adenosine is correct?",
    choices: [
      "It may be considered for a regular, monomorphic wide-complex tachycardia, but never for an irregular or polymorphic one",
      "It is first-line for any wide-complex tachycardia, including irregular ones",
      "It is the treatment of choice for pre-excited afib",
      "It is contraindicated in every wide-complex rhythm",
    ],
    answer: 0,
    rationale:
      "In a stable, regular, monomorphic wide-complex tachycardia, AHA ACLS guidance allows adenosine for both diagnosis and treatment, since some of these rhythms are SVT with aberrancy. In irregular wide-complex rhythms (possible pre-excited afib) or polymorphic VT, it can be dangerous.",
  },
  {
    id: 82,
    category: "rhythm",
    rhythm: "torsades",
    vitals: { hr: 220, spo2: 88, nibp: "70/40", rr: 22 },
    stem: "Torsades de pointes most often arises on a background of which baseline EKG finding?",
    choices: ["A prolonged QT interval", "A short PR interval", "A prolonged PR interval", "ST elevation in the inferior leads"],
    answer: 0,
    rationale:
      "Torsades is a polymorphic VT linked to a long QT. Causes include drugs (methadone, ondansetron, haloperidol, some antibiotics and antiarrhythmics), low potassium or magnesium, and congenital long-QT syndromes. A QTc above about 500 ms is especially high risk.",
  },
  {
    id: 83,
    category: "code",
    rhythm: "torsades",
    shockable: true,
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "Your patient in torsades de pointes loses their pulse. What is the electrical therapy?",
    choices: [
      "Unsynchronized defibrillation — a polymorphic rhythm can't be reliably synchronized — plus CPR and magnesium",
      "Synchronized cardioversion at 50 J",
      "Transcutaneous pacing at 100",
      "No shock — torsades is non-shockable",
    ],
    answer: 0,
    rationale:
      "Pulseless torsades is treated as VF: CPR and unsynchronized shocks. Even with a pulse, polymorphic VT is shocked unsynchronized if unstable, because the defibrillator can't reliably sync to a changing QRS. Magnesium is the drug of choice.",
  },
  {
    id: 84,
    category: "condition",
    rhythm: "torsades",
    vitals: { hr: 76, spo2: 97, nibp: "124/76", rr: 16 },
    stem: "After an episode of torsades, which of the patient's orders should you question first?",
    choices: [
      "Scheduled IV ondansetron and haloperidol — both prolong the QT",
      "Acetaminophen for fever",
      "A potassium supplement for a potassium of 3.2",
      "Continuous telemetry",
    ],
    answer: 0,
    rationale:
      "Stop or question every QT-prolonging drug after torsades. Common inpatient offenders include ondansetron, haloperidol, methadone, fluoroquinolones, macrolides, and several antiarrhythmics. Correcting a low potassium (and magnesium) helps protect against recurrence.",
  },
  {
    id: 85,
    category: "code",
    rhythm: "vf_coarse",
    shockable: true,
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "You just delivered a shock for VF. What happens next?",
    choices: [
      "Resume chest compressions immediately for 2 minutes, then check the rhythm",
      "Check for a pulse for 30 seconds",
      "Analyze the rhythm again right away",
      "Give rescue breaths only, and wait to see if the patient responds",
    ],
    answer: 0,
    rationale:
      "Even after a successful shock, the heart rarely generates a pulse right away. Resuming CPR immediately, with no pulse check, minimizes the no-flow time. The rhythm is rechecked after 2 minutes.",
  },
  {
    id: 86,
    category: "code",
    rhythm: "vf_coarse",
    shockable: true,
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "In a VF arrest, when do the 2025 AHA guidelines suggest giving the first dose of epinephrine?",
    choices: [
      "After initial defibrillation attempts have failed — rapid defibrillation comes first",
      "Before the first shock, as soon as IV access is obtained",
      "Only after amiodarone has been given",
      "Epinephrine is not used in shockable rhythms",
    ],
    answer: 0,
    rationale:
      "For shockable rhythms, the 2025 AHA adult ALS guidelines state it is reasonable to give epinephrine after initial defibrillation attempts fail, prioritizing rapid defibrillation. In non-shockable arrest (PEA or asystole), epinephrine is given as early as possible.",
  },
  {
    id: 87,
    category: "code",
    rhythm: "vf_coarse",
    shockable: true,
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "Which vascular-access approach for an adult in cardiac arrest matches the 2025 AHA guidelines?",
    choices: [
      "Attempt IV access first; use intraosseous (IO) if IV attempts are unsuccessful or not feasible",
      "Always place an IO first — it is faster and more effective",
      "Give all code drugs through the endotracheal tube",
      "Delay drugs until a central line is placed",
    ],
    answer: 0,
    rationale:
      "The 2025 AHA guidelines recommend attempting IV access first in adults. IO is reasonable if IV attempts fail or aren't feasible. A 2025 ILCOR review found lower odds of sustained ROSC with IO than with IV.",
  },
  {
    id: 88,
    category: "meds",
    rhythm: "vf_coarse",
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "You are drawing up code-dose epinephrine for a pulseless adult. Which preparation is correct?",
    choices: [
      "1 mg of the 0.1 mg/mL (1 mg/10 mL) prefilled syringe, IV/IO",
      "1 mg of the 1 mg/mL vial, IM into the thigh",
      "0.3 mg of the 1 mg/mL auto-injector, IV",
      "10 mg of the 0.1 mg/mL syringe",
    ],
    answer: 0,
    rationale:
      "The arrest dose is 1 mg IV/IO, from the 0.1 mg/mL (1 mg in 10 mL) prefilled syringe, every 3–5 minutes. The 1 mg/mL concentration is used for IM dosing in anaphylaxis. Confusing the two concentrations is a well-known cause of serious medication errors.",
  },
  {
    id: 89,
    category: "code",
    rhythm: "vf_fine",
    shockable: true,
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "VF continues after three shocks. A colleague suggests double sequential defibrillation (two defibrillators) or moving the pads to a new position. What do the 2025 AHA guidelines say?",
    choices: [
      "The usefulness of both double sequential and vector-change defibrillation is not established — keep up high-quality CPR, standard shocks, epinephrine, and an antiarrhythmic",
      "Double sequential defibrillation is now mandatory after the second shock",
      "Stop shocking — VF after three shocks is non-shockable",
      "Switch to synchronized cardioversion",
    ],
    answer: 0,
    rationale:
      "The 2025 AHA guidelines state that the usefulness of vector change and of double sequential defibrillation for refractory VF/pVT (after 3 or more shocks) has not been established. The core of refractory VF care is unchanged: high-quality CPR, shocks, epinephrine, amiodarone or lidocaine, and treating reversible causes.",
  },
  {
    id: 90,
    category: "code",
    rhythm: "asystole",
    shockable: false,
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "The monitor suddenly shows a flat line on a patient you just saw talking. What do you do first?",
    choices: [
      "Check the patient: responsiveness, pulse, and breathing. If pulseless, start CPR, then confirm the rhythm (leads, gain, second lead)",
      "Defibrillate at 200 J immediately",
      "Silence the alarm — it's almost certainly a loose lead",
      "Give atropine 1 mg",
    ],
    answer: 0,
    rationale:
      "Treat the patient, not the monitor. A disconnected lead can look like asystole, and so can a real arrest. Check the patient, start CPR if pulseless, and confirm asystole by checking connections, increasing the gain, and viewing another lead. Asystole is never shocked.",
  },
  {
    id: 91,
    category: "condition",
    rhythm: "sinus_tach",
    vitals: { hr: 128, spo2: 95, nibp: "88/54", rr: 24 },
    stem: "Post-op day 1, your patient has this sinus tachycardia at 128. They are pale and cool, with fresh blood in the drain. What is the priority?",
    choices: [
      "Treat the cause — suspect hemorrhage: notify the provider, support volume (fluids or blood per order), and get labs",
      "Give diltiazem to slow the rate",
      "Give adenosine 6 mg rapid IV push",
      "Give metoprolol 5 mg IV",
    ],
    answer: 0,
    rationale:
      "Sinus tachycardia is compensation. Slowing it with a rate-control drug in a bleeding, hypotensive patient takes away the one thing maintaining cardiac output. Find and treat the cause: here, hemorrhage and hypovolemia.",
  },
  {
    id: 92,
    category: "meds",
    rhythm: "svt",
    vitals: { hr: 190, spo2: 98, nibp: "114/72", rr: 18 },
    stem: "How should adenosine be administered?",
    choices: [
      "Through a proximal IV (such as the antecubital), as a rapid push immediately followed by a 20 mL saline flush, with a continuous strip running",
      "Diluted in 100 mL and infused over 15 minutes",
      "Slow IV push over 2 minutes through a hand IV",
      "IM into the deltoid",
    ],
    answer: 0,
    rationale:
      "Adenosine's half-life is about 10 seconds, so it has to reach the heart fast: use a large proximal vein, push it rapidly, and flush right away. Warn the patient about brief chest pressure, and record a strip, because the pause may reveal the underlying rhythm.",
  },
  {
    id: 93,
    category: "condition",
    rhythm: "svt",
    vitals: { hr: 186, spo2: 98, nibp: "112/70", rr: 18 },
    stem: "Before any drug is given for stable SVT, what should be tried first?",
    choices: [
      "Vagal maneuvers — such as a modified Valsalva (strain, then lie flat with legs raised)",
      "Synchronized cardioversion",
      "Amiodarone 300 mg IV push",
      "Ice to the chest and a sedative",
    ],
    answer: 0,
    rationale:
      "For stable regular narrow-complex SVT, the ACLS tachycardia algorithm starts with vagal maneuvers, then adenosine 6 mg, then 12 mg. The modified Valsalva (strain for about 15 seconds, then lie flat with legs raised) converts more patients than the standard maneuver.",
  },
  {
    id: 94,
    category: "condition",
    rhythm: "svt",
    vitals: { hr: 190, spo2: 98, nibp: "110/70", rr: 18 },
    stem: "Seconds after adenosine is pushed, the monitor shows a few seconds of near-asystole and the patient says, 'I feel like I'm going to die.' What is the correct response?",
    choices: [
      "Reassure the patient and keep watching — a brief pause and a sense of doom are expected and short-lived",
      "Start CPR immediately",
      "Give atropine 1 mg",
      "Defibrillate at 200 J",
    ],
    answer: 0,
    rationale:
      "Adenosine briefly blocks the AV node, so a transient pause is expected. Flushing, chest pressure, and a sense of impending doom are common and last only seconds. Warn the patient beforehand, stay with them, and keep recording the strip.",
  },
  {
    id: 95,
    category: "condition",
    rhythm: "idioventricular",
    vitals: { hr: 34, spo2: 92, nibp: "84/50", rr: 16 },
    stem: "The monitor shows a slow, wide rhythm at 34. How do you tell complete heart block with a ventricular escape apart from a pure idioventricular rhythm?",
    choices: [
      "Look for P waves: in complete heart block, regular P waves march through independently; in idioventricular rhythm there is no organized atrial activity",
      "Check the QRS width — only CHB is wide",
      "Count the rate — CHB is always faster than 60",
      "Give adenosine and see what happens",
    ],
    answer: 0,
    rationale:
      "Both rhythms show a slow, wide ventricular escape. The difference is above: CHB has an atrial rhythm that simply isn't conducted, while idioventricular rhythm has no organized atrial activity. Both are managed with the bradycardia algorithm when there's a pulse, and neither escape rhythm should be suppressed.",
  },
  {
    id: 96,
    category: "rhythm",
    rhythm: "aivr",
    vitals: { hr: 72, spo2: 97, nibp: "116/72", rr: 16 },
    stem: "This wide, regular rhythm with no P waves is running at 72. Which feature most reliably separates AIVR from ventricular tachycardia?",
    choices: [
      "The rate — AIVR is roughly 40–100, while VT is faster than 100",
      "AIVR always has a pacer spike",
      "VT always has visible P waves",
      "AIVR has a narrow QRS",
    ],
    answer: 0,
    rationale:
      "AIVR and VT both come from a ventricular focus, and the difference is the rate. AIVR runs about 40–100, often right after reperfusion, and is usually benign and self-limited. Above 100, a wide ventricular rhythm is VT.",
  },
  {
    id: 97,
    category: "rhythm",
    rhythm: "wpw",
    vitals: { hr: 80, spo2: 99, nibp: "118/74", rr: 14 },
    stem: "Which finding is NOT part of the classic WPW pattern?",
    choices: [
      "A prolonged PR interval (> 0.20 s)",
      "A short PR interval (< 0.12 s)",
      "A slurred upstroke (delta wave) at the start of the QRS",
      "A slightly widened QRS",
    ],
    answer: 0,
    rationale:
      "In WPW, the accessory pathway bypasses the AV node's normal delay, so the PR is SHORT. The early activation creates the delta wave and a slightly wide QRS. A long PR is first-degree AV block, which is the opposite problem.",
  },
  {
    id: 98,
    category: "condition",
    rhythm: "wpw",
    vitals: { hr: 78, spo2: 99, nibp: "116/70", rr: 14 },
    stem: "A young patient with a WPW pattern and recurrent palpitations asks about a long-term fix. What is the definitive treatment?",
    choices: [
      "Catheter ablation of the accessory pathway",
      "Lifelong digoxin",
      "A permanent pacemaker",
      "Daily diltiazem",
    ],
    answer: 0,
    rationale:
      "Ablation destroys the accessory pathway and is curative in most patients, which is why WPW with symptoms is referred to electrophysiology (2015 ACC/AHA/HRS SVT guideline). AV-nodal blockers such as digoxin and diltiazem can be dangerous if the patient develops afib.",
  },
  {
    id: 99,
    category: "rhythm",
    rhythm: "wpw_afib",
    vitals: { hr: 230, spo2: 95, nibp: "106/68", rr: 20 },
    stem: "A 26-year-old is alert with this rhythm: very fast (rates above 200), irregularly irregular, with wide QRS complexes that change shape from beat to beat. What is it?",
    choices: [
      "Pre-excited atrial fibrillation (afib conducting over an accessory pathway)",
      "Afib with RVR and a normal conduction system",
      "Monomorphic ventricular tachycardia",
      "Sinus tachycardia with a bundle branch block",
    ],
    answer: 0,
    rationale:
      "Irregularly irregular + wide + varying + very fast = afib going down an accessory pathway. Ordinary afib is narrow (unless there's a BBB, where the wide QRS looks the same on every beat), and monomorphic VT is regular. Recognizing it matters because AV-nodal blockers are dangerous here.",
  },
  {
    id: 100,
    category: "condition",
    rhythm: "wpw_afib",
    vitals: { hr: 240, spo2: 88, nibp: "72/40", rr: 26 },
    stem: "Your patient in pre-excited atrial fibrillation becomes hypotensive and confused, with a weak pulse. What is the treatment?",
    choices: [
      "Synchronized cardioversion now",
      "Adenosine 6 mg rapid IV push",
      "Diltiazem 0.25 mg/kg IV",
      "Start CPR",
    ],
    answer: 0,
    rationale:
      "An unstable tachyarrhythmia with a pulse calls for synchronized cardioversion. In pre-excited afib, adenosine and diltiazem block the AV node, which can drive more impulses down the accessory pathway and trigger VF. The patient still has a pulse, so this is not CPR.",
  },
  {
    id: 101,
    category: "code",
    rhythm: "paced_v",
    shockable: false,
    vitals: { hr: 70, spo2: null, nibp: "--/--", rr: 0 },
    stem: "Your patient with a permanent pacemaker is unresponsive and has no pulse. The monitor still shows a pacer spike before every wide QRS at 70. What is this, and what do you do?",
    choices: [
      "PEA — the pacer is capturing electrically but the heart isn't pumping; start CPR and follow the PEA algorithm",
      "Normal paced rhythm — no action needed",
      "Defibrillate — paced complexes are wide",
      "Turn the pacemaker off with a magnet",
    ],
    answer: 0,
    rationale:
      "Electrical activity on the monitor doesn't mean there's a pulse. A pacemaker keeps firing and can capture the myocardium electrically even when the heart isn't contracting effectively. No pulse means PEA: CPR, epinephrine as soon as possible, and a search for H's and T's.",
  },
  {
    id: 102,
    category: "condition",
    rhythm: "paced_v",
    vitals: { hr: 70, spo2: 96, nibp: "122/74", rr: 16 },
    stem: "A patient with an implanted pacemaker may need cardioversion or defibrillation. How should the pads be placed?",
    choices: [
      "Not directly over the device — use an anterior-posterior position or place the pad a few inches away — and have the device checked afterward",
      "Directly over the pacemaker generator for the best conduction",
      "Pads can't be used — patients with pacemakers can't be shocked",
      "Only after the pacemaker has been surgically removed",
    ],
    answer: 0,
    rationale:
      "Never delay a shock because of a device, but don't place a pad directly over the generator: it can reduce the current reaching the heart and damage the device. An anterior-posterior placement or moving the pad aside works. Afterward, the device should be interrogated.",
  },
  {
    id: 103,
    category: "rhythm",
    rhythm: "pacer_noncapture",
    vitals: { hr: 30, spo2: 91, nibp: "82/48", rr: 18 },
    stem: "This strip shows pacer spikes at a steady rate, most of which are NOT followed by a QRS. A colleague calls it 'failure to sense.' Are they right?",
    choices: [
      "No — spikes that produce no QRS are failure to CAPTURE; failure to sense is spikes firing regardless of (sometimes on top of) the patient's own beats",
      "Yes — any abnormal spike is failure to sense",
      "No — this is normal pacing",
      "No — this is oversensing, because the spikes are missing",
    ],
    answer: 0,
    rationale:
      "Failure to capture: the spike fires but no depolarization follows. Failure to sense (undersensing): the pacer ignores the patient's own beats and fires anyway, which can put a spike on a T wave. Oversensing: the pacer withholds spikes that should have fired.",
  },
  {
    id: 104,
    category: "condition",
    rhythm: "pacer_noncapture",
    vitals: { hr: 30, spo2: 90, nibp: "78/46", rr: 20 },
    stem: "A patient with a PERMANENT pacemaker is dizzy and hypotensive with failure to capture on the monitor. What is the priority?",
    choices: [
      "Notify the provider, apply transcutaneous pads and be ready to pace, and anticipate electrolytes, a chest X-ray, and device interrogation",
      "Turn the patient onto their left side and recheck in an hour",
      "Give adenosine to reset the pacemaker",
      "Defibrillate to restart the pacer",
    ],
    answer: 0,
    rationale:
      "A symptomatic, pacer-dependent patient needs a backup pacing source now (transcutaneous), plus a hunt for the cause: lead displacement or fracture, a rising threshold (hyperkalemia, ischemia, acidosis, drugs), or battery depletion. Interrogation identifies which one.",
  },
  {
    id: 105,
    category: "rhythm",
    rhythm: "hyperk",
    vitals: { hr: 58, spo2: 97, nibp: "138/84", rr: 18 },
    stem: "As serum potassium climbs, what is the classic order of EKG changes?",
    choices: [
      "Peaked T waves → flattened P and longer PR → widening QRS → sine-wave pattern → VF or asystole",
      "Wide QRS → peaked T waves → normal EKG",
      "ST elevation → Q waves → T-wave inversion",
      "Prominent U waves → flat T waves → prolonged QT",
    ],
    answer: 0,
    rationale:
      "This is the textbook sequence for hyperkalemia, but real patients can skip steps and go straight to a lethal rhythm, and EKG changes don't correlate reliably with the number. U waves and flat T waves are signs of HYPOkalemia.",
  },
  {
    id: 106,
    category: "meds",
    rhythm: "hyperk",
    vitals: { hr: 58, spo2: 97, nibp: "140/86", rr: 18 },
    stem: "Which hyperkalemia therapy actually REMOVES potassium from the body?",
    choices: [
      "Hemodialysis (or, more slowly, loop diuretics in patients who make urine, and GI potassium binders)",
      "IV calcium gluconate",
      "Regular insulin with dextrose",
      "Nebulized albuterol",
    ],
    answer: 0,
    rationale:
      "Calcium stabilizes the membrane; insulin, albuterol (and bicarbonate if acidotic) shift potassium into cells; only dialysis, diuresis, or binders remove it. Shifted potassium comes back out as the drugs wear off, so removal has to follow.",
  },
  {
    id: 107,
    category: "condition",
    rhythm: "nsr",
    vitals: { hr: 76, spo2: 99, nibp: "124/78", rr: 14 },
    stem: "A lab calls a potassium of 6.8 on a well-appearing patient. The monitor shows normal sinus rhythm with normal T waves, and the lab notes that the sample was hemolyzed. What do you do?",
    choices: [
      "Notify the provider, obtain a 12-lead, and anticipate a stat redraw — hemolysis can falsely raise potassium",
      "Give calcium, insulin, and albuterol immediately without further checks",
      "Ignore the result",
      "Give a potassium supplement",
    ],
    answer: 0,
    rationale:
      "Red cells burst during a difficult draw and release potassium into the sample, causing pseudohyperkalemia. A normal 12-lead and a hemolyzed specimen point that way, but the provider still needs to know, and a redraw confirms it. Never ignore a critical value.",
  },
  {
    id: 108,
    category: "code",
    rhythm: "idioventricular",
    shockable: false,
    vitals: { hr: null, spo2: null, nibp: "--/--", rr: 0 },
    stem: "A dialysis patient arrests with a slow, wide PEA and a known potassium of 8.0. Which statement matches the 2025 AHA guidelines?",
    choices: [
      "High-quality CPR and epinephrine come first; IV calcium may be given, but its benefit in arrest is not well established and it must not delay core resuscitation",
      "Calcium is proven to double survival, so give it before starting CPR",
      "Defibrillate — hyperkalemic PEA is shockable",
      "Stop resuscitation — hyperkalemic arrests are not survivable",
    ],
    answer: 0,
    rationale:
      "The 2025 AHA special-circumstances guidelines say the effectiveness of IV calcium in cardiac arrest from suspected hyperkalemia is not well established, so it has to be weighed against time spent on CPR, defibrillation of shockable rhythms, and epinephrine. Treating the cause (shifting potassium, emergent dialysis) still matters.",
  },
  {
    id: 109,
    category: "rhythm",
    rhythm: "stemi",
    vitals: { hr: 86, spo2: 95, nibp: "148/92", rr: 20 },
    stem: "A 12-lead shows ST elevation in V1–V4. Which territory and artery are most likely involved?",
    choices: [
      "Anterior wall — left anterior descending (LAD)",
      "Inferior wall — right coronary artery (RCA)",
      "Lateral wall — circumflex",
      "Posterior wall — the aorta",
    ],
    answer: 0,
    rationale:
      "V1–V4 look at the anterior wall and septum, which the LAD supplies. II, III, and aVF are inferior (usually the RCA), and I, aVL, V5, and V6 are lateral (circumflex). Anterior MIs put large areas of muscle at risk and can cause Mobitz II or complete heart block.",
  },
  {
    id: 110,
    category: "condition",
    rhythm: "stemi",
    vitals: { hr: 82, spo2: 97, nibp: "140/86", rr: 18 },
    stem: "Your STEMI patient is on room air with an SpO2 of 97%. A student starts a non-rebreather mask 'because it's a heart attack.' What is correct?",
    choices: [
      "Oxygen isn't needed at 97% — in ACS it's given for hypoxemia (SpO2 below 90%)",
      "Every MI patient needs high-flow oxygen",
      "Oxygen should be given only after the troponin returns",
      "Oxygen dissolves the clot",
    ],
    answer: 0,
    rationale:
      "Routine supplemental oxygen doesn't improve outcomes in normoxic ACS patients. Current ACS guidance (2025 ACC/AHA) reserves it for SpO2 below 90% or respiratory distress. Being on room air with normal saturation is not a reason to withhold aspirin, the 12-lead, or cath lab activation.",
  },
  {
    id: 111,
    category: "meds",
    rhythm: "stemi",
    vitals: { hr: 88, spo2: 96, nibp: "150/94", rr: 18 },
    stem: "PCI isn't available in time, so fibrinolytic therapy is being considered for your STEMI patient. Which history item is an ABSOLUTE contraindication?",
    choices: [
      "Any prior intracranial hemorrhage",
      "Age over 65",
      "Taking a daily aspirin",
      "A blood pressure of 150/94",
    ],
    answer: 0,
    rationale:
      "Absolute contraindications include any prior intracranial hemorrhage, a known cerebral vascular lesion or malignant intracranial tumor, an ischemic stroke within 3 months, suspected aortic dissection, and active bleeding. Report any of these right away, because the reperfusion plan depends on them.",
  },
  {
    id: 112,
    category: "condition",
    rhythm: "nsr",
    vitals: { hr: 96, spo2: 95, nibp: "94/58", rr: 0 },
    stem: "Your patient has just achieved ROSC and is ventilated. Which oxygenation and blood-pressure targets match the 2025 AHA post-arrest guidelines?",
    choices: [
      "SpO2 90–98% and a mean arterial pressure (MAP) of at least 65 mm Hg",
      "SpO2 100% at all times and SBP above 160",
      "SpO2 80–85% and MAP above 50",
      "No targets — titrate to comfort",
    ],
    answer: 0,
    rationale:
      "The 2025 AHA post-cardiac-arrest algorithm uses 100% FiO2 until SpO2 can be measured reliably, then titrates to 90–98% (PaO2 60–105 mm Hg). It targets PaCO2 35–45 and a MAP of at least 65 mm Hg, avoiding both hypoxia and hyperoxia.",
  },
  {
    id: 113,
    category: "condition",
    rhythm: "nsr",
    vitals: { hr: 88, spo2: 96, nibp: "108/66", rr: 0 },
    stem: "After ROSC, your patient does not follow commands off sedation. What do the 2025 AHA guidelines recommend for temperature?",
    choices: [
      "A deliberate temperature-control strategy with a goal of 32–37.5 °C, maintained for at least 36 hours",
      "No temperature management is needed",
      "Warm the patient to 39 °C to boost metabolism",
      "Cool to 28 °C for 6 hours",
    ],
    answer: 0,
    rationale:
      "In the 2025 guidelines, temperature control covers hypothermic (32–34 °C) and normothermic/fever-prevention (36–37.5 °C) strategies. It is reasonable for at least 36 hours in patients who don't follow commands after ROSC. Every strategy includes preventing fever.",
  },
  {
    id: 114,
    category: "code",
    rhythm: "nsr",
    shockable: false,
    vitals: { hr: 92, spo2: null, nibp: "--/--", rr: 0 },
    stem: "An adult is found in cardiac arrest next to drug paraphernalia, and an opioid overdose is suspected. Where does naloxone fit, per the 2025 AHA guidelines?",
    choices: [
      "It may be reasonable to give, as long as it doesn't interfere with high-quality CPR with breaths — standard resuscitation comes first",
      "Give naloxone and wait for it to work before starting CPR",
      "Naloxone replaces epinephrine in opioid arrests",
      "Naloxone is contraindicated in cardiac arrest",
    ],
    answer: 0,
    rationale:
      "Naloxone reverses respiratory depression, but the 2025 AHA guidelines note there are no trials showing it helps once the heart has stopped. It's reasonable if it doesn't interrupt CPR, ventilation, or epinephrine. Survivors should be discharged with naloxone and teaching.",
  },
  {
    id: 115,
    category: "code",
    rhythm: "sinus_tach",
    shockable: false,
    vitals: { hr: 128, spo2: null, nibp: "--/--", rr: 0 },
    stem: "A ventilated trauma patient loses their pulse. The monitor shows a fast, narrow PEA. Breath sounds are absent on the right, the trachea deviates left, and airway pressures are high. What reversible cause and action fit?",
    choices: [
      "Tension pneumothorax — CPR plus immediate needle (or finger) decompression by the provider, then a chest tube",
      "Hyperkalemia — give calcium",
      "Hypoglycemia — give D50",
      "VF — defibrillate",
    ],
    answer: 0,
    rationale:
      "Tension pneumothorax is one of the T's. It causes obstructive shock, with absent breath sounds on one side, tracheal deviation, and rising airway pressures in a ventilated patient. The treatment is decompression, and CPR alone won't fix it. PEA is not shockable.",
  },
];

window.EKG_QUESTIONS = EKG_QUESTIONS;
