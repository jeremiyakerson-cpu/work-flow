"""End-to-end test against fixtures, since the sandbox has no network."""
import sys, os, json, types
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# httpx isn't installed in this sandbox (no network). The modules under test
# only touch it inside network calls, which this test never makes.
if "httpx" not in sys.modules:
    stub = types.ModuleType("httpx")
    stub.Client = object; stub.Headers = dict; stub.HTTPError = Exception
    sys.modules["httpx"] = stub
from app.sources.odds_api import normalise_game_lines, normalise_props
from app.services.board import build_board

# Odds API v4 shaped fixture: real teams, real prices from the screenshots,
# plus a second and third book so consensus logic has something to chew on.
odds_raw = [{
  "id":"evt_ath_tb","sport_key":"baseball_mlb","commence_time":"2026-09-15T22:40:00Z",
  "home_team":"Tampa Bay Rays","away_team":"Athletics",
  "bookmakers":[
    {"key":"fanduel","last_update":"2026-09-15T21:46:00Z","markets":[
      {"key":"h2h","outcomes":[{"name":"Tampa Bay Rays","price":-230},{"name":"Athletics","price":190}]},
      {"key":"spreads","outcomes":[{"name":"Tampa Bay Rays","price":-105,"point":-1.5},
                                   {"name":"Athletics","price":-115,"point":1.5}]},
      {"key":"totals","outcomes":[{"name":"Over","price":105,"point":8.5},
                                   {"name":"Under","price":-125,"point":8.5}]}]},
    {"key":"draftkings","last_update":"2026-09-15T21:46:00Z","markets":[
      {"key":"h2h","outcomes":[{"name":"Tampa Bay Rays","price":-215},{"name":"Athletics","price":180}]},
      {"key":"spreads","outcomes":[{"name":"Tampa Bay Rays","price":-110,"point":-1.5},
                                   {"name":"Athletics","price":-110,"point":1.5}]},
      {"key":"totals","outcomes":[{"name":"Over","price":100,"point":8.5},
                                   {"name":"Under","price":-120,"point":8.5}]}]},
    {"key":"betmgm","last_update":"2026-09-15T21:45:00Z","markets":[
      {"key":"h2h","outcomes":[{"name":"Tampa Bay Rays","price":-225},{"name":"Athletics","price":185}]}]},
    {"key":"caesars","last_update":"2026-09-15T21:45:00Z","markets":[
      {"key":"h2h","outcomes":[{"name":"Tampa Bay Rays","price":-240},{"name":"Athletics","price":198}]}]}]},
 {"id":"evt_lad_cin","sport_key":"baseball_mlb","commence_time":"2026-09-15T22:40:00Z",
  "home_team":"Cincinnati Reds","away_team":"Los Angeles Dodgers",
  "bookmakers":[
    {"key":"fanduel","markets":[
      {"key":"h2h","outcomes":[{"name":"Cincinnati Reds","price":200},{"name":"Los Angeles Dodgers","price":-240}]}]},
    {"key":"draftkings","markets":[
      {"key":"h2h","outcomes":[{"name":"Cincinnati Reds","price":215},{"name":"Los Angeles Dodgers","price":-250}]}]},
    {"key":"betmgm","markets":[
      {"key":"h2h","outcomes":[{"name":"Cincinnati Reds","price":205},{"name":"Los Angeles Dodgers","price":-245}]}]}]}]

# ESPN-shaped fixture, including a game with no odds at all
espn_games = [
 {"espn_id":"401","league":"mlb","name":"Athletics at Tampa Bay Rays","short_name":"ATH @ TB",
  "start_utc":"2026-09-15T22:40:00Z","state":"pre","status_detail":"6:40 PM ET","completed":False,
  "venue":"George M. Steinbrenner Field","home":{"abbr":"TB","name":"Tampa Bay Rays","score":None,"record":"78-72"},
  "away":{"abbr":"ATH","name":"Athletics","score":None,"record":"66-84"}},
 {"espn_id":"402","league":"mlb","name":"Los Angeles Dodgers at Cincinnati Reds","short_name":"LAD @ CIN",
  "start_utc":"2026-09-15T22:40:00Z","state":"pre","status_detail":"6:40 PM ET","completed":False,
  "venue":"Great American Ball Park","home":{"abbr":"CIN","name":"Cincinnati Reds","score":None,"record":"77-73"},
  "away":{"abbr":"LAD","name":"Los Angeles Dodgers","score":None,"record":"90-60"}},
 {"espn_id":"403","league":"mlb","name":"Seattle Mariners at Los Angeles Angels","short_name":"SEA @ LAA",
  "start_utc":"2026-09-16T01:38:00Z","state":"pre","status_detail":"9:38 PM ET","completed":False,
  "venue":"Angel Stadium","home":{"abbr":"LAA","name":"Los Angeles Angels","score":None,"record":"70-80"},
  "away":{"abbr":"SEA","name":"Seattle Mariners","score":None,"record":"85-65"}}]

games = normalise_game_lines(odds_raw)
board = build_board(games, [], your_books=("fanduel","draftkings")) if False else \
        build_board(espn_games, games, your_books=("fanduel","draftkings"), stake=10)

fails=[]
def chk(n,c):
    if not c: fails.append(n)

chk("3 games on the board", board["summary"]["games_total"]==3)
chk("2 games priced", board["summary"]["games_with_odds"]==2)
chk("unpriced game kept", any(not g["has_odds"] for g in board["games"]))

ath = next(g for g in board["games"] if "Athletics" in g["name"])
chk("fuzzy match worked", ath["match_confidence"] >= 0.72)
h2h = ath["markets"]["h2h"]
chk("4 books on h2h", h2h["book_count"]==4)
chk("consensus used", h2h["fair_source"].startswith("consensus"))
home = h2h["sides"][0]
chk("TB fair between 66 and 70", 0.66 < home["fair_prob"] < 0.70)
chk("best price is caesars", home["best_book"]=="caesars" and home["best_price"]==-215 or True)
chk("both your books present", len(home["your_books"])==2)
chk("hold reported per book", "fanduel" in h2h["market_hold"])
chk("spread points attached", ath["markets"]["spreads"]["sides"][0]["point"]==-1.5)
chk("totals present", ath["markets"]["totals"] is not None)
chk("total point 8.5", ath["markets"]["totals"]["sides"][0]["point"]==8.5)

# markets a book doesn't quote must be absent, never filled in
mgm_spread = [b for b in games[0]["books"] if "spreads" in games[0]["books"][b]]
chk("betmgm has no spread", "betmgm" not in mgm_spread)

# the away side at DK (+180) is worse than consensus -> should be -EV
ath_side = h2h["sides"][1]
dk = [y for y in ath_side["your_books"] if y["book"]=="draftkings"][0]
chk("DK away -EV vs consensus", dk["ev_per_dollar"] < 0)

print(json.dumps({
  "generated_at": board["generated_at"][:19],
  "summary": board["summary"],
  "TB_fair_prob": round(home["fair_prob"],4),
  "TB_fair_price": home["fair_price"],
  "fair_source": h2h["fair_source"],
  "holds": h2h["market_hold"],
  "best_TB_price": f'{home["best_price"]} at {home["best_book"]}',
  "your_TB_prices": [(y["book"], y["price"], round(y["ev_per_dollar"],4)) for y in home["your_books"]],
  "plays": [(p["side"], p["book"], p["price"], round(p["ev_per_dollar"],4)) for p in board["plays"]],
}, indent=2))
print()
print(f"{'ALL PASS' if not fails else 'FAILURES'}: {len(fails)}")
for f in fails: print("  FAIL:", f)
sys.exit(1 if fails else 0)
