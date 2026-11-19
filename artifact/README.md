# CEN Study Hub artifact

Source for the published board at https://claude.ai/artifact/ScGmYRwVnbTJc2HY5cqLDD.

| File | Purpose |
| --- | --- |
| `template.html` | The page (styles, markup, behaviour). Edit this, not the built file. |
| `questions/dNN*.txt` | Question bank source, one or more files per board domain. |
| `build.js` | Validates the bank and injects it into the template. |
| `rebalance.js` | Lists items where the keyed option is the longest, and applies distractor patches. |
| `patches/` | The option-length patches and the rewrite script applied after review (kept as a record). |
| `cen-study-hub.html` | Built output. This is the file that gets published. |
| `VERIFICATION.md` | How the bank was reviewed, what failed, and what still needs an RN's check. |

```sh
node build.js check      # validate sources, print questions per topic
node build.js build      # write cen-study-hub.html
node build.js blind DIR  # write key-stripped, shuffled review files, one per domain
node build.js compare DIR  # compare reviewer answers (review-*.json) with the key
```

## Question format

Blank line between questions. Options are written with the correct one first and are shuffled
deterministically per question at build time, so key position is balanced and stable.

```
@ 1.0                       domain.topicIndex (topic order must match the board; progress is stored by index)
? stem
+ correct option
- wrong option | one-line reason it is wrong        (exactly three)
= rationale for the correct option
! optional: a figure or guideline step that still needs confirming (shown as "Verify" after answering)
```

## Stored progress

- `cen-study-hub/v1` — topic ticks, `{domainId: [bool, ...]}`. Unchanged since the first version.
- `cen-study-hub/q1` — question history, `{questionId: [attempts, timesCorrect, lastCorrect]}`.
- `cen-study-hub/x1` — timed exams, `{active, past}`. `active` is the exam in progress
  (`ids`, `ans`, `flag`, `i`, `left` in ms, `started`) or `null`; `past` is the last ten results
  (`at`, `right`, `total`, `secs`, `blank`, `per` domain).

The backup file (version 3) carries all three; older backups still restore.

## Timed exam

150 questions drawn in the outline's proportions (the `WEIGHT` map in `template.html`), 3 hours on
the clock, no feedback until submission. The clock runs only while the exam is open, so closing it
pauses the exam and it can be resumed after a reload. Blank answers count as wrong; answered
questions are added to the question history when the exam is submitted. The result is compared
with the passing standard of 99 of 150.

Question ids are `domain.topic.sequence` in source order. Append new questions to the end of a
topic so existing ids, and people's saved history, stay valid.
