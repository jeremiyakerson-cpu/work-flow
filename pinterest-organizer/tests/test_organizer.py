"""Run with:  python -m unittest discover tests"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

os.environ["PINBOARD_STATE_DIR"] = tempfile.mkdtemp()
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import yaml  # noqa: E402

from pinboard import categorize, config  # noqa: E402
from pinboard.sorter import sort_pins  # noqa: E402

EXAMPLE = yaml.safe_load((config.ROOT / "config.example.yaml").read_text(encoding="utf-8"))


def make_cfg(**sorting):
    cfg = config._merge(config.DEFAULTS, EXAMPLE)
    cfg["sorting"].update(sorting)
    return cfg


def reset_state():
    for f in config.STATE_DIR.glob("*.json"):
        f.unlink()


class FakePinterest:
    def __init__(self, boards):
        self._boards = [{"id": f"b{i}", "name": name} for i, name in enumerate(boards)]
        self.pins = {b["id"]: [] for b in self._boards}
        self.section_list, self.moves, self.saves = {}, [], []

    def add(self, board_name, pin_id, title):
        board = next(b for b in self._boards if b["name"] == board_name)
        self.pins[board["id"]].append({"id": pin_id, "title": title, "board_id": board["id"]})

    def boards(self):
        return self._boards

    def board_pins(self, board_id):
        return iter(self.pins.get(board_id, []))

    def find_or_create_board(self, name, privacy="PUBLIC"):
        for b in self._boards:
            if b["name"].lower() == name.lower():
                return b
        board = {"id": f"b{len(self._boards)}", "name": name}
        self._boards.append(board)
        return board

    def find_or_create_section(self, board_id, name):
        secs = self.section_list.setdefault(board_id, [])
        for s in secs:
            if s["name"].lower() == name.lower():
                return s
        sec = {"id": f"{board_id}-s{len(secs)}", "name": name}
        secs.append(sec)
        return sec

    def move_pin(self, pin_id, board_id, section_id=None):
        self.moves.append((pin_id, board_id, section_id))

    def save_pin(self, pin_id, board_id, section_id=None, image_url=None, title=""):
        self.saves.append((pin_id, board_id, section_id))


class CategorizerTests(unittest.TestCase):
    def setUp(self):
        reset_state()

    def test_matches_keywords_and_phrases(self):
        c = categorize.Categorizer(make_cfg())
        self.assertEqual(c.classify("Easy chicken pasta dinner")[0], "Recipes")
        self.assertEqual(c.classify("Cozy living room ideas")[0], "Home Decor")
        self.assertEqual(c.classify("Crochet blanket pattern")[0], "Crafts")
        self.assertIsNone(c.classify("Vintage motorcycle")[0])

    def test_plurals_match(self):
        c = categorize.Categorizer(make_cfg())
        self.assertEqual(c.classify("Succulents in pots")[0], "Garden")
        self.assertEqual(c.classify("10 cookies to bake")[0], "Recipes")
        self.assertEqual(c.classify("Cozy living rooms")[0], "Home Decor")

    def test_new_category_is_created_and_remembered(self):
        c = categorize.Categorizer(make_cfg(min_pins_for_new_category=3))
        texts = ["Vintage motorcycle restoration", "Cafe racer motorcycle build",
                 "Motorcycles for beginners", "Watercolor sunset", "Random thing"]
        created = c.discover_categories(texts)
        self.assertEqual(created, ["Motorcycle"])
        self.assertEqual(c.classify("Old motorcycle")[0], "Motorcycle")
        again = categorize.Categorizer(make_cfg())  # loaded back from disk
        self.assertIn("Motorcycle", again.categories)

    def test_no_category_from_too_few_pins(self):
        c = categorize.Categorizer(make_cfg(min_pins_for_new_category=4))
        self.assertEqual(c.discover_categories(["Watercolor sunset", "Watercolor bird"]), [])


class SorterTests(unittest.TestCase):
    def setUp(self):
        reset_state()
        self.api = FakePinterest(["Stuff", "Keep Me"])
        for i, title in enumerate(["Chocolate cake recipe", "Boho bedroom decor",
                                   "Vintage motorcycle", "Motorcycle helmet",
                                   "Motorcycle garage tools", "Motorcycle garage",
                                   "Mystery item"]):
            self.api.add("Stuff", f"p{i}", title)
        self.api.add("Keep Me", "k1", "Chicken soup")

    def test_preview_changes_nothing(self):
        sort_pins(self.api, categorize.Categorizer(make_cfg()), make_cfg(), apply=False)
        self.assertEqual(self.api.moves, [])
        self.assertEqual(len(self.api.boards()), 2)

    def test_apply_into_sections_creates_new_category(self):
        cfg = make_cfg(skip_boards=["Keep Me"])
        summary = sort_pins(self.api, categorize.Categorizer(cfg), cfg, apply=True)
        self.assertEqual(summary["Recipes"], 1)
        self.assertEqual(summary["Motorcycle"], 4)
        self.assertEqual(summary["Unsorted"], 1)
        self.assertNotIn("k1", [m[0] for m in self.api.moves])
        sorted_board = next(b for b in self.api.boards() if b["name"] == "Sorted Pins")
        names = {s["name"] for s in self.api.section_list[sorted_board["id"]]}
        self.assertEqual(names, {"Recipes", "Home Decor", "Motorcycle", "Unsorted"})
        self.assertTrue(all(m[1] == sorted_board["id"] and m[2] for m in self.api.moves))

    def test_apply_into_boards(self):
        cfg = make_cfg(organize_as="boards")
        sort_pins(self.api, categorize.Categorizer(cfg), cfg, apply=True)
        names = {b["name"] for b in self.api.boards()}
        self.assertTrue({"Recipes", "Home Decor", "Motorcycle"} <= names)
        self.assertTrue(all(m[2] is None for m in self.api.moves))


class PickerTests(unittest.TestCase):
    def setUp(self):
        reset_state()
        from pinboard.webapp import create_app
        self.api = FakePinterest([])
        self.queries = []

        def finder(queries, per_query, show=True):
            self.queries.append(list(queries))
            n = len(self.queries)
            return [
                {"id": f"{n}01", "image": "https://i.pinimg.com/a.jpg", "title": "Lemon cake recipe",
                 "query": queries[0]},
                {"id": f"{n}02", "image": "https://i.pinimg.com/b.jpg", "title": "Sunset photo",
                 "query": queries[0]},
            ]

        self.app = create_app(make_cfg(), lambda: self.api, finder=finder).test_client()

    def test_refresh_shows_pins_with_checkboxes(self):
        self.app.post("/refresh")
        html = self.app.get("/").get_data(as_text=True)
        self.assertIn('type="checkbox" name="pin" value="101"', html)
        self.assertIn("i.pinimg.com/a.jpg", html)
        self.assertEqual(self.queries[0], ["Recipes", "Home Decor", "Garden"])

    def test_refresh_rotates_categories(self):
        self.app.post("/refresh")
        self.app.post("/refresh")
        self.assertEqual(self.queries[1], ["Fashion", "Beauty", "Crafts"])

    def test_custom_search(self):
        self.app.post("/refresh", data={"query": "cozy reading nook"})
        self.assertEqual(self.queries[0], ["cozy reading nook"])

    def test_save_files_ticked_pins_into_categories(self):
        self.app.post("/refresh")
        resp = self.app.post("/save", data={"pin": ["101", "102"], "cat_102": "Garden"},
                             follow_redirects=True)
        self.assertIn("Saved", resp.get_data(as_text=True))
        board = next(b for b in self.api.boards() if b["name"] == "Sorted Pins")
        secs = {s["id"]: s["name"] for s in self.api.section_list[board["id"]]}
        filed = {pin: secs[sec] for pin, _, sec in self.api.saves}
        self.assertEqual(filed, {"101": "Recipes", "102": "Garden"})
        html = self.app.get("/").get_data(as_text=True)
        self.assertNotIn('value="101"', html)

    def test_refresh_skips_pins_already_seen(self):
        from pinboard import discover
        self.app.post("/refresh")
        self.assertEqual(discover.load_seen(), set())
        self.app.post("/refresh")  # first batch was shown and passed over
        self.assertEqual(discover.load_seen(), {"101", "102"})


if __name__ == "__main__":
    unittest.main()
