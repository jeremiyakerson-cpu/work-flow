"""Sorts the pins already on your boards into categories."""
from .api import pin_text


class Destinations:
    """Turns a category name into the board (and section) it lives in."""

    def __init__(self, api, cfg):
        self.api = api
        self.cfg = cfg["sorting"]

    def resolve(self, category, dry_run=False):
        """Returns (board_id, section_id, label). With dry_run nothing is created."""
        privacy = self.cfg["board_privacy"]
        if self.cfg["organize_as"] == "boards":
            label = f"board '{category}'"
            if dry_run:
                return None, None, label
            board = self.api.find_or_create_board(category, privacy)
            return board["id"], None, label
        board_name = self.cfg["sections_board"]
        label = f"'{board_name}' > section '{category}'"
        if dry_run:
            return None, None, label
        board = self.api.find_or_create_board(board_name, privacy)
        section = self.api.find_or_create_section(board["id"], category)
        return board["id"], section["id"], label


def _boards_to_sort(api, cfg):
    sort_cfg = cfg["sorting"]
    skip = {b.lower() for b in sort_cfg["skip_boards"]}
    only = {b.lower() for b in sort_cfg["only_boards"]}
    for board in api.boards():
        name = board["name"].lower()
        if name in skip:
            continue
        if only and name not in only:
            continue
        yield board


def sort_pins(api, categorizer, cfg, apply=False):
    sort_cfg = cfg["sorting"]
    dest = Destinations(api, cfg)

    print("Reading your boards and pins...")
    pins = []
    for board in _boards_to_sort(api, cfg):
        for pin in api.board_pins(board["id"]):
            pin["_board_name"] = board["name"]
            pins.append(pin)
    print(f"Found {len(pins)} pins.\n")

    # Pass 1: match against known categories.
    assigned, unmatched = {}, []
    for pin in pins:
        category, _ = categorizer.classify(pin_text(pin))
        if category:
            assigned[pin["id"]] = category
        else:
            unmatched.append(pin)

    # Pass 2: invent new categories from what's left, then match again.
    created = categorizer.discover_categories([pin_text(p) for p in unmatched])
    if created:
        print("New categories found: " + ", ".join(created) + "\n")
    for pin in unmatched:
        category, _ = categorizer.classify(pin_text(pin))
        assigned[pin["id"]] = category or sort_cfg["unsorted_name"]

    # Pass 3: build the plan, then move.
    summary = {}
    moved = errors = 0
    for pin in pins:
        category = assigned[pin["id"]]
        summary[category] = summary.get(category, 0) + 1
        board_id, section_id, label = dest.resolve(category, dry_run=not apply)
        title = (pin.get("title") or pin.get("description") or pin["id"])[:60]
        if not apply:
            print(f"  [{pin['_board_name']}] {title!r} -> {label}")
            continue
        if pin.get("board_id") == board_id and pin.get("board_section_id") == section_id:
            continue
        try:
            api.move_pin(pin["id"], board_id, section_id)
            moved += 1
        except Exception as exc:  # keep going; report at the end
            errors += 1
            print(f"  ! could not move {title!r}: {exc}")

    print("\nCategories:")
    for category, count in sorted(summary.items(), key=lambda kv: -kv[1]):
        print(f"  {category:<30} {count} pin{'s' if count != 1 else ''}")
    if apply:
        print(f"\nMoved {moved} pins. {errors} errors.")
    else:
        print("\nThis was a preview. Nothing was changed.")
        print("Happy with it? Run 'Sort my pins (apply)' to move them for real.")
    return summary
