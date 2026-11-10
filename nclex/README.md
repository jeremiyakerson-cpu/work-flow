# NCLEX-RN Practice Test Generator

A question bank and CLI generator for building 25-item NCLEX-RN Next
Generation-style practice tests, structured around the current NCSBN
NCLEX-RN Test Plan's 8 client-needs categories and the Clinical Judgment
Measurement Model (NCJMM).

## Layout

```
nclex/
  SCHEMA.md          question-bank JSON schema + validation rules
  blueprint.json      default 25-item category mix (NCLEX-RN blueprint midpoints)
  generate_test.py    CLI generator / validator
  data/                one JSON file per client-needs category
  generated/          suggested output folder for generated tests (gitignored content, folder tracked)
```

## Quick start

```bash
# Full 25-item exam-style test using the default blueprint, shuffled category order
python3 nclex/generate_test.py --seed 42 --output nclex/generated/test1.md

# Same test, but questions and the answer key in two separate files
# (so you can take the test "blind" before checking answers)
python3 nclex/generate_test.py --seed 42 --split --output nclex/generated/test1.md

# Focus a whole 25-question test on one weak category
python3 nclex/generate_test.py --category "Pharmacological and Parenteral Therapies" \
  --count 25 --output nclex/generated/pharm_drill.md

# Custom category mix (e.g., heavier on Physiological Adaptation)
cat > /tmp/my_mix.json <<'EOF'
{"categories": {
  "Physiological Adaptation": 8,
  "Pharmacological and Parenteral Therapies": 6,
  "Reduction of Risk Potential": 6,
  "Safety and Infection Control": 5
}}
EOF
python3 nclex/generate_test.py --mix /tmp/my_mix.json --output nclex/generated/custom.md

# Machine-readable output for another tool/app to render
python3 nclex/generate_test.py --seed 42 --format json --output nclex/generated/test1.json

# Check what's in the bank
python3 nclex/generate_test.py --stats

# Validate the whole bank (run this after adding/editing questions)
python3 nclex/generate_test.py --validate
```

Every generated test defaults to full exam simulation: categories are
weighted per the current NCLEX-RN blueprint, questions are shuffled so
categories aren't blocked together, and it reports a suggested time limit
(~1.15 min/item, standard NCLEX pacing). Use `--group-by-category` to keep
categories blocked together for a more study-by-topic session instead.

## Item types

The bank includes the item formats most representative of current
NCLEX-RN Next Generation testing:

- **multiple_choice** — classic single-answer, 4 options
- **sata** ("select all that apply") — 5-7 options, 2+ correct
- **ordered_response** — rank/sequence items (prioritization, procedural steps)
- **fill_in_blank** — calculation items (dosage/IV rate math)

## Growing the bank

See `SCHEMA.md` for the full field spec. Add questions to the matching
category file in `data/`, then run `--validate` before generating tests.
More questions per category means less repetition across multiple
generated tests — each category currently holds ~12 questions, enough for
a couple of non-repeating full-length tests; keep adding to reduce repeats
further.

## A note on currency

Content reflects the NCSBN 2024 Next Generation NCLEX-RN Test Plan
categories/weights and current evidence-based nursing practice standards
as of this writing. NCSBN periodically revises the test plan and its
category percentages — re-check `blueprint.json` against the latest
published NCSBN NCLEX-RN Test Plan periodically and adjust if categories
or weights change.
