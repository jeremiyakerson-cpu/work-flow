#!/usr/bin/env python3
"""Generate NCLEX-RN Next Generation-style practice tests from the local question bank.

See nclex/SCHEMA.md for the question-bank format and nclex/README.md for usage examples.
"""

from __future__ import annotations

import argparse
import json
import random
import string
import sys
from datetime import datetime
from pathlib import Path

HERE = Path(__file__).resolve().parent
DEFAULT_BANK_DIR = HERE / "data"
DEFAULT_BLUEPRINT = HERE / "blueprint.json"

CANONICAL_CATEGORIES = [
    "Management of Care",
    "Safety and Infection Control",
    "Health Promotion and Maintenance",
    "Psychosocial Integrity",
    "Basic Care and Comfort",
    "Pharmacological and Parenteral Therapies",
    "Reduction of Risk Potential",
    "Physiological Adaptation",
]

ITEM_TYPES = {"multiple_choice", "sata", "ordered_response", "fill_in_blank"}
DIFFICULTIES = {"easy", "moderate", "hard"}
NCJMM_STEPS = {
    "Recognize Cues",
    "Analyze Cues",
    "Prioritize Hypotheses",
    "Generate Solutions",
    "Take Action",
    "Evaluate Outcomes",
    "Foundational Knowledge",
}

LETTERS = string.ascii_uppercase


class BankError(Exception):
    pass


def load_bank(bank_dir: Path) -> list[dict]:
    questions: list[dict] = []
    files = sorted(bank_dir.glob("*.json"))
    if not files:
        raise BankError(f"No question files found in {bank_dir}")
    for f in files:
        try:
            data = json.loads(f.read_text())
        except json.JSONDecodeError as e:
            raise BankError(f"{f.name}: invalid JSON ({e})") from e
        if not isinstance(data, list):
            raise BankError(f"{f.name}: top-level JSON must be an array of question objects")
        for q in data:
            q["_source_file"] = f.name
            questions.append(q)
    return questions


def validate_question(q: dict, seen_ids: set[str]) -> list[str]:
    errors = []
    where = f"[{q.get('id', '???')} in {q.get('_source_file', '?')}]"

    qid = q.get("id")
    if not qid or not isinstance(qid, str):
        errors.append(f"{where}: missing/invalid 'id'")
    elif qid in seen_ids:
        errors.append(f"{where}: duplicate id '{qid}'")
    else:
        seen_ids.add(qid)

    category = q.get("category")
    if category not in CANONICAL_CATEGORIES:
        errors.append(f"{where}: category '{category}' is not one of the 8 canonical categories")

    if not q.get("subcategory"):
        errors.append(f"{where}: missing 'subcategory'")

    item_type = q.get("item_type")
    if item_type not in ITEM_TYPES:
        errors.append(f"{where}: item_type '{item_type}' not in {sorted(ITEM_TYPES)}")

    if q.get("difficulty") not in DIFFICULTIES:
        errors.append(f"{where}: difficulty '{q.get('difficulty')}' not in {sorted(DIFFICULTIES)}")

    if q.get("clinical_judgment_step") not in NCJMM_STEPS:
        errors.append(f"{where}: clinical_judgment_step '{q.get('clinical_judgment_step')}' invalid")

    if not q.get("stem"):
        errors.append(f"{where}: missing 'stem'")

    if not q.get("rationale"):
        errors.append(f"{where}: missing 'rationale'")

    options = q.get("options")
    answer = q.get("answer")

    if item_type == "multiple_choice":
        if not isinstance(options, list) or len(options) != 4:
            errors.append(f"{where}: multiple_choice requires exactly 4 options")
        if not isinstance(answer, list) or len(answer) != 1:
            errors.append(f"{where}: multiple_choice requires a single-element 'answer' index list")
        elif options and not (0 <= answer[0] < len(options)):
            errors.append(f"{where}: answer index out of range")

    elif item_type == "sata":
        if not isinstance(options, list) or not (5 <= len(options) <= 7):
            errors.append(f"{where}: sata requires 5-7 options")
        if not isinstance(answer, list) or len(answer) < 2:
            errors.append(f"{where}: sata requires 2+ correct answer indices")
        elif options and any(not (0 <= a < len(options)) for a in answer):
            errors.append(f"{where}: an answer index is out of range")

    elif item_type == "ordered_response":
        if not isinstance(options, list) or not (4 <= len(options) <= 6):
            errors.append(f"{where}: ordered_response requires 4-6 options")
        if not isinstance(answer, list) or (options and len(answer) != len(options)):
            errors.append(f"{where}: ordered_response 'answer' must list every option index exactly once")
        elif options and sorted(answer) != list(range(len(options))):
            errors.append(f"{where}: ordered_response 'answer' must be a permutation of all option indices")

    elif item_type == "fill_in_blank":
        if options:
            errors.append(f"{where}: fill_in_blank must not have 'options'")
        if not isinstance(answer, list) or len(answer) != 1 or not isinstance(answer[0], str):
            errors.append(f"{where}: fill_in_blank requires answer as a single-element string list")

    return errors


def validate_bank(questions: list[dict]) -> list[str]:
    errors = []
    seen_ids: set[str] = set()
    for q in questions:
        errors.extend(validate_question(q, seen_ids))
    return errors


def bank_stats(questions: list[dict]) -> str:
    lines = ["Question bank inventory", "=" * 24, ""]
    by_cat: dict[str, list[dict]] = {}
    for q in questions:
        by_cat.setdefault(q.get("category", "UNKNOWN"), []).append(q)
    for cat in CANONICAL_CATEGORIES:
        qs = by_cat.get(cat, [])
        lines.append(f"{cat}: {len(qs)}")
        by_type: dict[str, int] = {}
        for q in qs:
            by_type[q.get("item_type", "?")] = by_type.get(q.get("item_type", "?"), 0) + 1
        if by_type:
            detail = ", ".join(f"{k}={v}" for k, v in sorted(by_type.items()))
            lines.append(f"    ({detail})")
    unknown = set(by_cat) - set(CANONICAL_CATEGORIES)
    for cat in unknown:
        lines.append(f"[NON-CANONICAL] {cat}: {len(by_cat[cat])}")
    lines.append("")
    lines.append(f"Total: {len(questions)}")
    return "\n".join(lines)


def select_questions(
    questions: list[dict],
    mix: dict[str, int],
    rng: random.Random,
) -> list[dict]:
    by_cat: dict[str, list[dict]] = {}
    for q in questions:
        by_cat.setdefault(q["category"], []).append(q)

    selected: list[dict] = []
    for cat, count in mix.items():
        pool = by_cat.get(cat, [])
        if len(pool) < count:
            raise BankError(
                f"Category '{cat}' needs {count} question(s) but the bank only has {len(pool)}. "
                f"Add more questions to nclex/data/ or lower the count for this category."
            )
        selected.extend(rng.sample(pool, count))
    return selected


def render_question_md(number: int, q: dict, reveal: bool) -> str:
    lines = [f"**{number}.** {q['stem']}", ""]
    item_type = q["item_type"]
    options = q.get("options")

    if item_type == "multiple_choice":
        for i, opt in enumerate(options):
            lines.append(f"{LETTERS[i]}. {opt}")
    elif item_type == "sata":
        lines.append("*(Select all that apply.)*")
        lines.append("")
        for i, opt in enumerate(options):
            lines.append(f"[ ] {LETTERS[i]}. {opt}")
    elif item_type == "ordered_response":
        lines.append("*(Place the following in the correct order.)*")
        lines.append("")
        for i, opt in enumerate(options):
            lines.append(f"{LETTERS[i]}. {opt}")
    elif item_type == "fill_in_blank":
        lines.append("*(Fill in the blank.)* Answer: ______________________")

    if reveal:
        lines.append("")
        lines.append(f"> **Answer:** {format_answer(q)}")
        lines.append(f"> **Rationale:** {q['rationale']}")
        lines.append(
            f"> _Category: {q['category']} — {q['subcategory']} | "
            f"Clinical judgment: {q['clinical_judgment_step']} | Difficulty: {q['difficulty']}_"
        )

    lines.append("")
    return "\n".join(lines)


def format_answer(q: dict) -> str:
    item_type = q["item_type"]
    options = q.get("options")
    answer = q["answer"]

    if item_type == "multiple_choice":
        return f"{LETTERS[answer[0]]}. {options[answer[0]]}"
    if item_type == "sata":
        return ", ".join(f"{LETTERS[i]}. {options[i]}" for i in sorted(answer))
    if item_type == "ordered_response":
        return " → ".join(f"{LETTERS[i]}. {options[i]}" for i in answer)
    if item_type == "fill_in_blank":
        return answer[0]
    return "?"


def build_markdown(
    selected: list[dict],
    title: str,
    seed: int | None,
    mix: dict[str, int],
    split: str | None,
) -> tuple[str, str | None]:
    now = datetime.now().strftime("%Y-%m-%d %H:%M")
    header = [
        f"# {title}", "",
        f"_Generated {now}" + (f" · seed {seed}" if seed is not None else "") + "_",
        "",
        "**Blueprint:**",
        "",
    ]
    for cat, count in mix.items():
        header.append(f"- {cat}: {count}")
    header += [
        "",
        f"**Total items:** {len(selected)}  |  **Suggested time:** "
        f"{round(len(selected) * 1.15)} minutes (~69 sec/item, standard NCLEX pacing)",
        "",
        "**Item-type key:** plain lettered items are single-answer multiple choice; items marked "
        "*(Select all that apply.)* may have 2 or more correct options; items marked "
        "*(Place in the correct order.)* require ranking all options; fill-in-the-blank items "
        "require a calculated numeric/text answer.",
        "",
        "---",
        "",
    ]

    body = [render_question_md(i + 1, q, reveal=False) for i, q in enumerate(selected)]

    if split:
        questions_md = "\n".join(header + body)
        answers = ["# Answer Key & Rationales", "", f"_{title}_", "", "---", ""]
        answers += [render_answer_only_md(i + 1, q) for i, q in enumerate(selected)]
        answers_md = "\n".join(answers)
        return questions_md, answers_md

    combined = header + body
    combined += [
        "---", "", "\\newpage", "",
        "## STOP — Answer key and rationales follow below", "",
        "---", "",
    ]
    combined += [render_question_md(i + 1, q, reveal=True) for i, q in enumerate(selected)]
    return "\n".join(combined), None


def render_answer_only_md(number: int, q: dict) -> str:
    lines = [
        f"**{number}.** {format_answer(q)}",
        "",
        f"Rationale: {q['rationale']}",
        "",
        f"_Category: {q['category']} — {q['subcategory']} | "
        f"Clinical judgment: {q['clinical_judgment_step']} | Difficulty: {q['difficulty']}_",
        "",
    ]
    return "\n".join(lines)


def build_json(selected: list[dict], title: str, seed: int | None, mix: dict[str, int]) -> str:
    out = {
        "title": title,
        "generated_at": datetime.now().isoformat(timespec="seconds"),
        "seed": seed,
        "blueprint": mix,
        "questions": [{k: v for k, v in q.items() if k != "_source_file"} for q in selected],
    }
    return json.dumps(out, indent=2)


def load_mix(args, questions: list[dict]) -> dict[str, int]:
    if args.category:
        return {args.category: args.count}
    if args.mix:
        data = json.loads(Path(args.mix).read_text())
        return data["categories"] if "categories" in data else data
    blueprint = json.loads(Path(args.blueprint).read_text())
    mix = dict(blueprint["categories"])
    if args.count != 25:
        scale = args.count / blueprint["total_items"]
        scaled = {k: round(v * scale) for k, v in mix.items()}
        diff = args.count - sum(scaled.values())
        keys = list(scaled)
        i = 0
        while diff != 0 and keys:
            k = keys[i % len(keys)]
            step = 1 if diff > 0 else -1
            scaled[k] = max(0, scaled[k] + step)
            diff -= step
            i += 1
        mix = scaled
    return mix


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bank-dir", type=Path, default=DEFAULT_BANK_DIR)
    parser.add_argument("--blueprint", type=Path, default=DEFAULT_BLUEPRINT,
                         help="Blueprint JSON file (category -> item count). Default: full NCLEX-RN mix.")
    parser.add_argument("--mix", type=Path, default=None,
                         help="Custom JSON file: {\"categories\": {\"Category Name\": count, ...}}")
    parser.add_argument("--category", type=str, default=None,
                         help="Generate a single-category focused test instead of the full blueprint.")
    parser.add_argument("--count", type=int, default=25, help="Total items (default 25).")
    parser.add_argument("--seed", type=int, default=None, help="Random seed for reproducible tests.")
    parser.add_argument("--title", type=str, default="NCLEX-RN Practice Test")
    parser.add_argument("--output", type=Path, default=None, help="Output file path (default: stdout).")
    parser.add_argument("--format", choices=["md", "json"], default="md")
    parser.add_argument("--split", action="store_true",
                         help="Write questions and answer key to two separate files (md only).")
    parser.add_argument("--group-by-category", action="store_true",
                         help="Keep questions grouped by category instead of shuffling exam-style.")
    parser.add_argument("--validate", action="store_true", help="Validate the question bank and exit.")
    parser.add_argument("--stats", action="store_true", help="Print bank inventory and exit.")
    parser.add_argument("--list-categories", action="store_true", help="List canonical categories and exit.")
    args = parser.parse_args()

    if args.list_categories:
        print("\n".join(CANONICAL_CATEGORIES))
        return

    try:
        questions = load_bank(args.bank_dir)
    except BankError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

    if args.stats:
        print(bank_stats(questions))
        return

    errors = validate_bank(questions)
    if args.validate:
        if errors:
            print(f"{len(errors)} error(s) found:\n")
            print("\n".join(errors))
            sys.exit(1)
        print(f"OK — {len(questions)} questions validated across {len(CANONICAL_CATEGORIES)} categories.")
        return

    if errors:
        print(f"Error: bank has {len(errors)} validation error(s). Run --validate for details.", file=sys.stderr)
        sys.exit(1)

    try:
        mix = load_mix(args, questions)
    except (json.JSONDecodeError, KeyError, FileNotFoundError) as e:
        print(f"Error reading mix/blueprint: {e}", file=sys.stderr)
        sys.exit(1)

    rng = random.Random(args.seed)

    try:
        selected = select_questions(questions, mix, rng)
    except BankError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

    if not args.group_by_category:
        rng.shuffle(selected)

    if args.format == "json":
        out = build_json(selected, args.title, args.seed, mix)
        if args.output:
            args.output.write_text(out)
            print(f"Wrote {args.output}")
        else:
            print(out)
        return

    questions_md, answers_md = build_markdown(
        selected, args.title, args.seed, mix, split=args.split if args.split else None
    )

    if args.split:
        if not args.output:
            print("Error: --split requires --output (used as the base filename).", file=sys.stderr)
            sys.exit(1)
        q_path = args.output.with_name(args.output.stem + "_questions" + args.output.suffix)
        a_path = args.output.with_name(args.output.stem + "_answers" + args.output.suffix)
        q_path.write_text(questions_md)
        a_path.write_text(answers_md)
        print(f"Wrote {q_path}\nWrote {a_path}")
    elif args.output:
        args.output.write_text(questions_md)
        print(f"Wrote {args.output}")
    else:
        print(questions_md)


if __name__ == "__main__":
    main()
