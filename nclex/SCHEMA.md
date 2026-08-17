# NCLEX Question Bank — Schema

Each category lives in its own JSON file under `nclex/data/`, e.g.
`nclex/data/pharmacological_and_parenteral_therapies.json`. Each file is a
JSON array of question objects.

## Question object

```jsonc
{
  "id": "PHARM-014",                 // unique across the whole bank; PREFIX-### (see prefixes below)
  "category": "Pharmacological and Parenteral Therapies", // must exactly match one of the 8 canonical categories
  "subcategory": "Adverse Effects/Contraindications/Side Effects/Interactions", // NCSBN test-plan subcategory
  "item_type": "multiple_choice",    // one of: multiple_choice | sata | ordered_response | fill_in_blank
  "clinical_judgment_step": "Analyze Cues", // one of the 6 NCJMM steps, or "Foundational Knowledge" for pure recall
  "difficulty": "moderate",          // easy | moderate | hard
  "stem": "The nurse is caring for a client who...",
  "options": [                       // omitted for fill_in_blank
    "Option text A",
    "Option text B",
    "Option text C",
    "Option text D"
  ],
  "answer": [1],                     // 0-based index/indices into options; see item_type rules below
  "rationale": "Explains why the correct answer(s) are right AND briefly why each distractor is wrong.",
  "nursing_concept": "Priority setting / ABCs"   // optional short tag
}
```

## `item_type` rules

- **multiple_choice** — exactly 4 options, `answer` is a single-element list, e.g. `[2]`.
- **sata** ("select all that apply") — 5 to 7 options, `answer` has 2+ elements, e.g. `[0, 2, 4]`.
- **ordered_response** — 4 to 6 options given in scrambled order; `answer` is the full list of indices
  in the *correct* order (e.g., priority-setting or steps-of-a-procedure items), e.g. `[3, 0, 2, 1]`.
- **fill_in_blank** — no `options` key; `answer` is a single-element list containing the exact expected
  string, including units where relevant (e.g., `["1.5 mL/hr"]` or `["2 tablets"]`). Used for dosage/IV
  calculations.

## Canonical categories (NCSBN 2024 NCLEX-RN Next Generation Test Plan)

1. Management of Care
2. Safety and Infection Control
3. Health Promotion and Maintenance
4. Psychosocial Integrity
5. Basic Care and Comfort
6. Pharmacological and Parenteral Therapies
7. Reduction of Risk Potential
8. Physiological Adaptation

## ID prefixes

| Category | Prefix |
|---|---|
| Management of Care | MOC |
| Safety and Infection Control | SIC |
| Health Promotion and Maintenance | HPM |
| Psychosocial Integrity | PSI |
| Basic Care and Comfort | BCC |
| Pharmacological and Parenteral Therapies | PHARM |
| Reduction of Risk Potential | ROR |
| Physiological Adaptation | PHYS |

## Clinical Judgment Measurement Model (NCJMM) steps

Recognize Cues → Analyze Cues → Prioritize Hypotheses → Generate Solutions →
Take Action → Evaluate Outcomes (use `"Foundational Knowledge"` for a
straight recall/fact item that doesn't map to a clinical-judgment step).

Run `python3 nclex/generate_test.py --validate` after adding questions —
it checks every file against these rules before you generate a test.
