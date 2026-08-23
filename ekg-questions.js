/*
 * 25-question EKG assessment for the Zoll monitor trainer.
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
      "IV magnesium sulfate is first-line for torsades de pointes, regardless of serum magnesium level, because it stabilizes the cardiac membrane and shortens the QT-related dysrhythmia.",
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
    rhythm: "afib",
    vitals: { hr: 150, spo2: 95, nibp: "112/68", rr: 18 },
    stem:
      "A patient has an irregular, wide-complex tachycardia suspected to be atrial fibrillation with WPW (pre-excitation). Which drugs must be AVOIDED?",
    choices: [
      "AV-nodal blockers — adenosine, diltiazem/verapamil, beta blockers, and digoxin",
      "Procainamide",
      "Amiodarone given as a slow infusion with cardiology guidance",
      "Synchronized cardioversion",
    ],
    answer: 0,
    rationale:
      "In pre-excited afib, blocking the AV node shunts conduction down the accessory pathway and can accelerate the rhythm into VF. Avoid adenosine, calcium channel blockers, beta blockers, and digoxin; procainamide or cardioversion are the safe options.",
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
    rhythm: "sinus_brady",
    vitals: { hr: 52, spo2: 96, nibp: "128/78", rr: 16 },
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
    rhythm: "avb3",
    vitals: { hr: 40, spo2: 93, nibp: "88/54", rr: 16 },
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
];

window.EKG_QUESTIONS = EKG_QUESTIONS;
