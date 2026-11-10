import glob, sys
files = {p: open(p).read().split("\n") for p in sorted(glob.glob("questions/*.txt"))}

def find(prefix):
    hits = [(p, i) for p, L in files.items() for i, l in enumerate(L) if l.startswith("? " + prefix)]
    assert len(hits) == 1, (prefix, len(hits))
    return hits[0]

def block_end(L, i):
    j = i
    while j < len(L) and L[j].strip():
        j += 1
    return j

def replace_block(prefix, new):
    p, i = find(prefix)
    L = files[p]
    L[i:block_end(L, i)] = new.strip("\n").split("\n")

def set_flag(prefix, text):
    p, i = find(prefix)
    L = files[p]
    j = block_end(L, i)
    blk = [l for l in L[i:j] if not l.startswith("!")]
    blk.append("! " + text)
    L[i:j] = blk

def set_line(prefix, tag, text):
    p, i = find(prefix)
    L = files[p]
    j = block_end(L, i)
    idx = [k for k in range(i, j) if L[k].startswith(tag)]
    assert len(idx) == 1, (prefix, tag, len(idx))
    L[idx[0]] = tag + " " + text

def set_distractor(prefix, starts, text):
    p, i = find(prefix)
    L = files[p]
    j = block_end(L, i)
    idx = [k for k in range(i, j) if L[k].startswith("- " + starts)]
    assert len(idx) == 1, (prefix, starts, len(idx))
    why = L[idx[0]].split(" | ", 1)[1]
    L[idx[0]] = "- " + text + " | " + why

replace_block("Bloody fluid is aspirated during pericardiocentesis.", """
? A pericardial drain was placed two hours ago for tamponade. Drainage has suddenly stopped, and the patient's BP has fallen from 118/70 to 84/58 with rising neck veins. What should the nurse suspect?
+ The drain is blocked and fluid is building up again
- The effusion has completely resolved and the drain can come out | A falling pressure with distended neck veins means tamponade is recurring.
- The patient is dehydrated and needs the drain clamped | Clamping would stop any remaining drainage and worsen tamponade.
- The transducer needs to be zeroed again before any action | Rechecking equipment must not delay action on clear clinical deterioration.
= Sudden loss of drainage with returning hypotension and jugular distension signals catheter occlusion and reaccumulation; the provider is notified at once.
""")

replace_block("A hiker is brought in 40 minutes after a rattlesnake bite", """
? A hiker is brought in 40 minutes after a rattlesnake bite to the forearm. A friend applied a tight tourniquet and ice. What should the nurse do first?
+ Remove the tourniquet and the ice, and immobilize the arm
- Leave the tourniquet in place until the antivenom has been started | Arterial occlusion concentrates venom and causes ischemic injury.
- Cut across the fang marks and apply suction | Incision and suction add tissue damage and remove no useful venom.
- Replace the ice with a fresh chemical cold pack | Cold worsens local tissue injury and does not slow venom spread.
= Tourniquets, ice, incision and suction all cause harm after pit viper bites; constricting items are removed and the limb is immobilized while antivenom is arranged.
""")

replace_block("A 70 kg adult sustained 40% TBSA burns two hours ago.", """
? A 70 kg adult sustained 40% TBSA burns two hours ago and has received no fluid yet. The burn protocol in use specifies 4 mL/kg/% TBSA of lactated Ringer's in 24 hours, with half in the first eight hours from the time of injury. At what rate should the infusion start?
+ About 930 mL per hour
- About 470 mL per hour | That spreads the first half over twelve hours.
- About 700 mL per hour | That counts eight hours from arrival instead of from the burn.
- About 1,400 mL per hour | That delivers the first half in four hours.
= The total is 11,200 mL; half (5,600 mL) is due within eight hours of the burn, leaving six hours, or roughly 930 mL per hour.
! Starting-rate formulas differ by protocol (4 mL/kg/% Parkland; 2 mL/kg/% in much current ABA/ATLS teaching for adults). The arithmetic follows the protocol named in the stem; I could not confirm the current ABA figure from a primary source.
""")

replace_block("An 18 kg child in hemorrhagic shock", """
? An 18 kg child in hemorrhagic shock is ordered an isotonic crystalloid bolus of 20 mL/kg. What volume should the nurse prepare?
+ 360 mL
- 90 mL | That is 5 mL/kg, a quarter of the ordered dose.
- 180 mL | That is 10 mL/kg, half of the ordered dose.
- 1,000 mL | An adult bolus would overload a child of this size.
= 20 mL/kg for an 18 kg child is 360 mL; blood products are started early if shock persists.
""")

replace_block("An 88-year-old is brought in after two falls and new confusion.", """
? An 88-year-old has new confusion with a temperature of 38.6 C, burning on urination for two days and suprapubic tenderness. Which test should be included in the workup?
+ Urinalysis and urine culture
- A sweat chloride test for cystic fibrosis | It has no relevance to this presentation.
- A throat swab for streptococcal infection | It would not explain urinary symptoms and suprapubic tenderness.
- A fecal occult blood test as the only study | It does not evaluate a urinary source of infection.
= Fever with urinary symptoms in an older adult warrants urine testing; confusion or falls alone, without fever or urinary symptoms, should not be attributed to bacteriuria.
""")

replace_block("A woman who is Rh negative has bleeding at 11 weeks", """
? A woman who is Rh negative has vaginal bleeding at 16 weeks of pregnancy. What should the nurse anticipate?
+ Rh immune globulin
- A blood transfusion in every case of bleeding | Transfusion depends on blood loss, not on Rh status.
- No treatment at any stage of the pregnancy | After the first trimester, bleeding can sensitize an Rh-negative mother.
- An antibiotic to prevent sensitization | Antibiotics do not prevent antibody formation.
= Rh immune globulin prevents an Rh-negative mother from forming antibodies after bleeding in the second or third trimester.
! ACOG's 2024 update suggests forgoing Rh testing and Rh immune globulin for pregnancy loss before 12 weeks; many facilities still give it. Confirm local practice.
""")

replace_block("A patient with a ruptured ectopic pregnancy is blood type O negative.", """
? Four days after methotrexate for an ectopic pregnancy, a patient returns with severe abdominal pain, dizziness and HR 126. What should the nurse suspect?
+ Rupture of the ectopic pregnancy
- The mild cramping that is expected after methotrexate | Expected pain is mild and is not accompanied by dizziness and tachycardia.
- A stomach upset caused by the medication | Gastric side effects do not cause severe pain with signs of blood loss.
- An allergic reaction to the methotrexate dose | Allergy would cause rash, wheeze or swelling rather than this picture.
= Ectopic pregnancies can rupture after methotrexate; severe pain with signs of hypovolemia is treated as rupture until proven otherwise.
""")

replace_block("Which finding shows that diabetic ketoacidosis has resolved?", """
? Which finding shows that diabetic ketoacidosis has resolved?
+ A venous pH of 7.3 or higher with ketones cleared
- A glucose below 250 mg/dL as the only measure used | Glucose normalizes well before the acidosis clears.
- The patient asking for something to eat | Appetite is not a resolution criterion.
- Urine that tests negative for glucose | That does not reflect ketosis or acidosis.
= Resolution is defined by clearance of ketones with a venous pH of 7.3 or more (or bicarbonate of 18 mEq/L or more), not by glucose alone.
! 2024 ADA consensus criteria (ketones below 0.6 mmol/L with pH 7.3 or bicarbonate 18); older criteria used closure of the anion gap. Checked against secondary summaries only.
""")

set_line("A patient in diabetic ketoacidosis has a potassium of 3.0 mEq/L.", "=", "Insulin drives potassium into cells, and total body potassium is already depleted; insulin is withheld until potassium has been replaced to at least 3.5 mEq/L.")
set_flag("A patient in diabetic ketoacidosis has a potassium of 3.0 mEq/L.", "The 2024 ADA consensus uses 3.5 mEq/L as the threshold; older references use 3.3. Checked against secondary summaries only.")

set_line("In diabetic ketoacidosis, when is IV sodium bicarbonate considered?", "+", "Only for very severe acidosis, with a pH below 7.0")
set_distractor("In diabetic ketoacidosis, when is IV sodium bicarbonate considered?", "Whenever the glucose", "Whenever the glucose is above 400 mg/dL on two readings in a row")
set_flag("In diabetic ketoacidosis, when is IV sodium bicarbonate considered?", "The 2024 ADA consensus uses pH below 7.0; older references use 6.9. Checked against secondary summaries only.")

set_flag("A patient achieves ROSC and is receiving 100% oxygen", "2025 AHA post-arrest guidance is reported as SpO2 90-98% once the patient is stabilized (secondary summaries); confirm against the guideline text.")
set_flag("A patient with ROSC after a 12-minute VF arrest", "2025 AHA guidance is reported as temperature control at 32-37.5 C for at least 36 hours with fever prevention (secondary summaries); confirm your protocol.")
set_line("In a restaurant an adult clutches his throat.", "=", "Severe airway obstruction in a responsive adult is treated with repeated cycles of five back blows followed by five abdominal thrusts.")
set_flag("In a restaurant an adult clutches his throat.", "The 2025 AHA guidelines added back blows first for adults (five back blows, then five abdominal thrusts); older materials teach abdominal thrusts alone. Confirmed from secondary summaries.")
set_flag("An 80 kg patient with sepsis has BP 82/46", "The 2026 Surviving Sepsis Campaign guideline keeps at least 30 mL/kg in the first 3 hours as a suggestion with low-certainty evidence (SCCM summary; full text not read).")
set_flag("What is the initial mean arterial pressure target", "The 2026 Surviving Sepsis Campaign guideline keeps 65 mmHg and suggests a 60-65 mmHg range for patients aged 65 and over (SCCM summary).")
set_flag("A patient with septic shock remains hypotensive after the fluid bolus.", "Norepinephrine as first-line vasopressor was not re-confirmed against the 2026 Surviving Sepsis text.")
set_flag("When predicting resources for an ESI level", "Resource list follows long-standing ESI teaching; not re-checked against the ESI v5 (2023) handbook text.")
set_flag("A 42-year-old with a productive cough would be ESI level 3", "Danger-zone vital sign thresholds were not confirmed against the ESI v5 handbook.")
set_flag("A 3-week-old infant is brought in with a rectal temperature", "Neonatal fever rule (at least ESI 2 under 28 days) is from earlier handbook editions; not confirmed against ESI v5.")
set_flag("A patient with ischemic stroke is otherwise eligible for alteplase.", "INR above 1.7 is the long-standing exclusion; not confirmed against the 2026 AHA/ASA guideline text.")
set_flag("A patient arrives 9 hours after onset with dense left hemiplegia", "Late-window thrombectomy selection criteria were not re-checked against the 2026 AHA/ASA guideline text.")

for p, L in files.items():
    open(p, "w").write("\n".join(L))
print("ok")
