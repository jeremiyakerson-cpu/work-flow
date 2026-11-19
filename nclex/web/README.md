# NCLEX-RN Practice Test — Web App

A static, dependency-free browser quiz for the question bank in `nclex/data/`.
Click through a test, answer each item type (multiple choice, select-all-that-apply,
ordered-response, fill-in-the-blank), flag questions, jump around via the question
grid, submit, and get an instant score with a per-category breakdown and full
rationales.

## Run it

Browsers block `fetch()` of local JSON files opened via `file://`, so serve this
folder instead of double-clicking `index.html`:

```bash
cd nclex/web
python3 -m http.server 8000
# then open http://localhost:8000/
```

Any other static file server works too (`npx serve`, VS Code's Live Server, etc.).

## Regenerating the data files

`bank.json` and `blueprint.json` are plain exports of the Python question bank —
regenerate them any time you add/edit questions in `nclex/data/`:

```bash
# from the repo root
python3 nclex/generate_test.py --export-bank --output nclex/web/bank.json
cp nclex/blueprint.json nclex/web/blueprint.json
```

## Files

- `index.html` — page structure (setup / quiz / results screens)
- `styles.css` — styling
- `app.js` — all quiz logic (loading the bank, building a test, rendering each
  item type, scoring, rendering the review screen). No framework, no build step.
- `bank.json` / `blueprint.json` — generated data (see above)

## Notes on scoring

- `sata` and `ordered_response` items are all-or-nothing (every correct option
  selected, no incorrect ones; exact order), matching how most NCLEX prep
  platforms score these formats.
- `fill_in_blank` answers are graded with lenient normalization (case/whitespace
  insensitive) — if you believe a dosage-calc answer was marked wrong unfairly,
  check it against the rationale shown in the review screen.
