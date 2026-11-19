"""The local page that shows pins you might like, each with a tick box."""
import json
import secrets
import threading
import webbrowser

from flask import Flask, flash, redirect, render_template_string, request, url_for

from .categorize import Categorizer
from .config import STATE_DIR
from .discover import find_pins, mark_seen
from .sorter import Destinations

BATCH_PATH = STATE_DIR / "current_batch.json"
ROTATION_PATH = STATE_DIR / "rotation.json"


def _read(path, default):
    return json.loads(path.read_text(encoding="utf-8")) if path.exists() else default


def _write(path, data):
    path.write_text(json.dumps(data, indent=2), encoding="utf-8")


def _next_queries(terms, count):
    """Cycles through your categories so each refresh brings different ideas."""
    if not terms:
        return []
    start = _read(ROTATION_PATH, {"next": 0})["next"] % len(terms)
    picked = [terms[(start + i) % len(terms)] for i in range(min(count, len(terms)))]
    _write(ROTATION_PATH, {"next": start + len(picked)})
    return picked


def create_app(cfg, api_factory, finder=find_pins):
    """api_factory() returns a Pinterest client; finder is swappable for tests."""
    app = Flask(__name__)
    app.secret_key = secrets.token_hex(16)
    disc = cfg["discover"]
    state = {"api": None}

    def api():
        if state["api"] is None:
            state["api"] = api_factory()
        return state["api"]

    @app.get("/")
    def index():
        categorizer = Categorizer(cfg)
        batch = _read(BATCH_PATH, [])
        names = sorted(set(categorizer.categories) | {p["category"] for p in batch}
                       | {cfg["sorting"]["unsorted_name"]})
        return render_template_string(PAGE, batch=batch, categories=names,
                                      queries=categorizer.search_terms())

    @app.post("/refresh")
    def refresh():
        categorizer = Categorizer(cfg)
        old = _read(BATCH_PATH, [])
        mark_seen(p["id"] for p in old)  # shown and passed over: don't show again
        custom = request.form.get("query", "").strip()
        queries = [custom] if custom else _next_queries(
            categorizer.search_terms() + list(disc["extra_queries"]), int(disc["queries_per_refresh"]))
        if not queries:
            flash("Add some categories to config.yaml (or type a search above) so I know what to look for.")
            return redirect(url_for("index"))
        try:
            found = finder(queries, int(disc["pins_per_query"]), show=bool(disc["show_browser"]))
        except Exception as exc:
            flash(f"Couldn't load ideas from Pinterest: {exc}")
            return redirect(url_for("index"))

        # Pins that match nothing: maybe they share a word that should be a new category.
        unmatched = [p for p in found if not categorizer.classify(p["title"])[0]]
        created = categorizer.discover_categories([p["title"] for p in unmatched])
        known = {n.lower(): n for n in categorizer.categories}
        for pin in found:
            category, _ = categorizer.classify(pin["title"])
            pin["category"] = (category or known.get(pin["query"].lower())
                               or categorizer.classify(pin["query"])[0] or pin["query"].title())
        _write(BATCH_PATH, found)
        msg = f"Found {len(found)} new pins for: {', '.join(queries)}."
        if created:
            msg += f" New categories: {', '.join(created)}."
        flash(msg)
        return redirect(url_for("index"))

    @app.post("/save")
    def save():
        chosen = set(request.form.getlist("pin"))
        if not chosen:
            flash("Tick at least one pin first.")
            return redirect(url_for("index"))
        batch = _read(BATCH_PATH, [])
        dest = Destinations(api(), cfg)
        saved, failed, keep, saved_ids = {}, [], [], []
        for pin in batch:
            if pin["id"] not in chosen:
                keep.append(pin)
                continue
            category = request.form.get(f"cat_{pin['id']}") or pin["category"]
            try:
                board_id, section_id, label = dest.resolve(category)
                api().save_pin(pin["id"], board_id, section_id,
                               image_url=pin["image"], title=pin["title"])
                saved[label] = saved.get(label, 0) + 1
                saved_ids.append(pin["id"])
            except Exception as exc:
                failed.append(f"{pin['title'][:40] or pin['id']}: {exc}")
                keep.append(pin)
        mark_seen(saved_ids)
        _write(BATCH_PATH, keep)
        if saved:
            flash("Saved " + ", ".join(f"{n} to {label}" for label, n in saved.items()) + ".")
        for line in failed[:5]:
            flash("Couldn't save " + line)
        return redirect(url_for("index"))

    return app


def run(cfg, api_factory):
    app = create_app(cfg, api_factory)
    port = int(cfg["discover"]["port"])
    threading.Timer(1.0, lambda: webbrowser.open(f"http://localhost:{port}/")).start()
    print(f"Pin picker running at http://localhost:{port}/  (close this window to stop)")
    # One request at a time: the browser profile can only be opened once.
    app.run(host="127.0.0.1", port=port, threaded=False)


PAGE = """<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Pin Picker</title>
<style>
  :root { --bg:#faf8f6; --card:#fff; --ink:#222; --muted:#6b6b6b; --accent:#c8102e; --line:#e6e1dc; }
  @media (prefers-color-scheme: dark) {
    :root { --bg:#161514; --card:#232120; --ink:#eee; --muted:#a8a29e; --accent:#ff4d5e; --line:#3a3633; }
  }
  * { box-sizing:border-box; }
  body { margin:0; background:var(--bg); color:var(--ink); font:15px/1.4 system-ui, sans-serif; }
  header { position:sticky; top:0; z-index:5; background:var(--bg); border-bottom:1px solid var(--line);
           padding:12px 16px; display:flex; flex-wrap:wrap; gap:10px; align-items:center; }
  h1 { font-size:18px; margin:0 12px 0 0; }
  button, .btn { border:0; border-radius:20px; padding:9px 16px; font:inherit; font-weight:600; cursor:pointer;
                 background:var(--line); color:var(--ink); }
  button.primary { background:var(--accent); color:#fff; }
  input[type=text] { border:1px solid var(--line); background:var(--card); color:var(--ink);
                     border-radius:20px; padding:8px 14px; font:inherit; min-width:200px; }
  .count { color:var(--muted); }
  .flash { margin:12px 16px 0; padding:10px 14px; border-radius:10px; background:var(--card);
           border:1px solid var(--line); }
  .hint { color:var(--muted); margin:12px 16px 0; }
  .grid { columns:5 220px; column-gap:14px; padding:16px; }
  .card { break-inside:avoid; margin-bottom:14px; background:var(--card); border-radius:14px; overflow:hidden;
          border:2px solid transparent; position:relative; }
  .card:has(input:checked) { border-color:var(--accent); }
  .card label { display:block; cursor:pointer; }
  .card img { width:100%; display:block; background:var(--line); min-height:120px; }
  .card input[type=checkbox] { position:absolute; top:10px; left:10px; width:24px; height:24px;
                               accent-color:var(--accent); cursor:pointer; }
  .meta { padding:8px 10px 10px; font-size:13px; }
  .meta .title { display:-webkit-box; -webkit-line-clamp:2; -webkit-box-orient:vertical; overflow:hidden; }
  .meta select { margin-top:6px; width:100%; border:1px solid var(--line); background:var(--bg);
                 color:var(--ink); border-radius:8px; padding:4px; font:inherit; }
  #busy { display:none; position:fixed; inset:0; background:rgba(0,0,0,.55); color:#fff; z-index:10;
          align-items:center; justify-content:center; font-size:18px; text-align:center; padding:16px; }
  @media (max-width:600px) { input[type=text] { min-width:0; flex:1; } }
</style></head>
<body>
<header>
  <h1>Pin Picker</h1>
  <form method="post" action="{{ url_for('refresh') }}" onsubmit="busy('Finding pins you might like...')"
        style="display:flex;gap:8px;flex-wrap:wrap;flex:1">
    <input type="text" name="query" placeholder="Search for something specific (optional)">
    <button type="submit">Refresh: show me more</button>
  </form>
  {% if batch %}
  <button type="button" onclick="toggleAll()">Select all</button>
  <button class="primary" type="submit" form="pick" onclick="busy('Saving your picks...')">
    Pin selected (<span id="n">0</span>)</button>
  {% endif %}
</header>
{% for msg in get_flashed_messages() %}<div class="flash">{{ msg }}</div>{% endfor %}
{% if not batch %}
  <p class="hint">No ideas loaded yet. Press <b>Refresh</b> and I'll look for pins about:
     {{ queries|join(', ') or 'your categories' }}.</p>
{% else %}
  <p class="hint">Tick the pins you want. Each one is filed into the category shown under it; change it if you like.
     Refresh skips anything you've already seen.</p>
{% endif %}
<form id="pick" method="post" action="{{ url_for('save') }}">
<div class="grid">
{% for pin in batch %}
  <div class="card">
    <label>
      <input type="checkbox" name="pin" value="{{ pin.id }}" onchange="count()">
      <img src="{{ pin.image }}" alt="{{ pin.title }}" loading="lazy" referrerpolicy="no-referrer">
    </label>
    <div class="meta">
      <div class="title">{{ pin.title or 'Untitled pin' }}</div>
      <select name="cat_{{ pin.id }}" aria-label="Category">
        {% for c in categories %}<option {% if c == pin.category %}selected{% endif %}>{{ c }}</option>{% endfor %}
      </select>
    </div>
  </div>
{% endfor %}
</div>
</form>
<div id="busy"></div>
<script>
  function count() { const n = document.querySelectorAll('input[name=pin]:checked').length;
                     const el = document.getElementById('n'); if (el) el.textContent = n; }
  function toggleAll() { const boxes = [...document.querySelectorAll('input[name=pin]')];
                         const on = boxes.some(b => !b.checked); boxes.forEach(b => b.checked = on); count(); }
  function busy(msg) { const b = document.getElementById('busy'); b.textContent = msg; b.style.display = 'flex'; }
</script>
</body></html>
"""
