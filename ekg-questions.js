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
];

window.EKG_QUESTIONS = EKG_QUESTIONS;
