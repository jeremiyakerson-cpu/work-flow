"""Decides which category a pin belongs in, and invents new categories.

Each category is a name plus a list of keywords. A pin goes to the category
whose keywords appear most in its title, description and alt text. Pins that
match nothing are grouped by the words they share: when enough of them share
a word (e.g. 5 pins all mention "crochet"), that word becomes a new category.
New categories are remembered in state/categories.json.
"""
import json
import re
from collections import Counter

from .config import STATE_DIR

LEARNED_PATH = STATE_DIR / "categories.json"

STOPWORDS = set("""
a about above after again all also an and any are as at be because been before being below between
both but by can could did do does doing down during each easy few for from further get got had has
have having here how i if in into is it its just like make made more most my new no nor not now of
off on once one only or other our out over own same she should so some such than that the their
them then there these they this those through to too under until up very was way we were what when
where which while who why will with would you your yours best ideas idea diy home com www http https
pin pins pinterest html amp quot top great love cute simple beautiful amazing perfect free tips
step guide how-to look looks inspiration inspo design designs style styles day days year years
time small big little good really things thing use using used want need first two three
""".split())

WORD_RE = re.compile(r"[a-z][a-z'\-]+")


def normalize(word):
    """Simple singular form: "cookies" -> "cookie", "rooms" -> "room"."""
    word = word.strip("'-")
    if len(word) > 3 and word.endswith("s") and not word.endswith(("ss", "us", "is")):
        return word[:-1]
    return word


def variants(word):
    """Other spellings to accept for the same word ("berries" / "berry")."""
    out = {word}
    if word.endswith("ie"):
        out.add(word[:-2] + "y")
    if word.endswith("e"):
        out.add(word[:-1])
    if word.endswith("y"):
        out.add(word[:-1] + "ie")
    return out


def tokens(text):
    return [normalize(w) for w in WORD_RE.findall((text or "").lower())]


class Categorizer:
    def __init__(self, cfg):
        self.cfg = cfg
        self.categories = {}
        for name, words in (cfg.get("categories") or {}).items():
            self.categories[name] = [w.lower() for w in (words or [])] or [name.lower()]
        self.learned = {}
        if LEARNED_PATH.exists():
            self.learned = json.loads(LEARNED_PATH.read_text(encoding="utf-8"))
            for name, words in self.learned.items():
                self.categories.setdefault(name, words)

    def save_learned(self):
        LEARNED_PATH.write_text(json.dumps(self.learned, indent=2), encoding="utf-8")

    def classify(self, text):
        """Returns (category, score). Category is None when nothing matched."""
        toks = tokens(text)
        joined = " " + " ".join(toks) + " "
        token_set = set().union(*(variants(t) for t in toks)) if toks else set()
        best, best_score = None, 0
        for name, keywords in self.categories.items():
            score = 0
            for kw in keywords:
                kw_toks = tokens(kw)
                if not kw_toks:
                    continue
                if len(kw_toks) == 1:
                    score += 1 if kw_toks[0] in token_set else 0
                elif f" {' '.join(kw_toks)} " in joined:
                    score += 2  # phrases are a stronger signal
            if score > best_score:
                best, best_score = name, score
        return best, best_score

    def discover_categories(self, unmatched_texts):
        """Turns words shared by many unmatched pins into new categories.

        Returns the names of the categories it created.
        """
        sort_cfg = self.cfg["sorting"]
        min_pins = int(sort_cfg["min_pins_for_new_category"])
        limit = int(sort_cfg["max_new_categories"])
        existing = {n.lower() for n in self.categories}
        remaining = [set(t for t in tokens(text) if t not in STOPWORDS and len(t) > 2)
                     for text in unmatched_texts]
        created = []
        while len(created) < limit:
            counts = Counter(t for toks in remaining for t in toks if t not in existing)
            if not counts:
                break
            word, n = counts.most_common(1)[0]
            if n < min_pins:
                break
            # Pull in words that almost always appear alongside this one
            # ("sourdough" with "bread") so the new category catches more pins.
            group = [toks for toks in remaining if word in toks]
            partners = Counter(t for toks in group for t in toks if t != word and t not in existing)
            keywords = [word] + [w for w, c in partners.most_common(3) if c >= max(min_pins, 0.8 * n)]
            name = word.replace("-", " ").title()
            self.categories[name] = keywords
            self.learned[name] = keywords
            existing.update(keywords)
            existing.add(name.lower())
            created.append(name)
            remaining = [toks for toks in remaining if not (toks & set(keywords))]
        if created:
            self.save_learned()
        return created

    def search_terms(self):
        """Search phrases for finding new pins, one per category."""
        unsorted = self.cfg["sorting"]["unsorted_name"].lower()
        return [name for name in self.categories if name.lower() != unsorted]
