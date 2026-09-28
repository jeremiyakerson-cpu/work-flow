"""
Fixture data so the app runs end to end with no API key and no credits spent.

Set DEMO_MODE=1.

MLB: prices are the real ones read off the Hard Rock and public boards on
Sep 15 2026, with three extra books added so the consensus logic has
something to work with. It is frozen, not live.

NFL, NCAAF, NBA, NHL and every props fixture: SYNTHETIC. Plausible
matchups and prices, written by hand to exercise the code paths (off-line
books, whole-number lines, one-sided props). They are not a record of any
real board and every source label says so.
"""
ODDS_FIXTURE = [
 {"id":"evt_ath_tb","sport_key":"baseball_mlb","commence_time":"2026-09-15T22:40:00Z",
  "home_team":"Tampa Bay Rays","away_team":"Athletics","bookmakers":[
   {"key":"fanduel","markets":[
    {"key":"h2h","outcomes":[{"name":"Tampa Bay Rays","price":-230},{"name":"Athletics","price":190}]},
    {"key":"spreads","outcomes":[{"name":"Tampa Bay Rays","price":-105,"point":-1.5},
                                 {"name":"Athletics","price":-115,"point":1.5}]},
    {"key":"totals","outcomes":[{"name":"Over","price":105,"point":8.5},
                                {"name":"Under","price":-125,"point":8.5}]}]},
   {"key":"draftkings","markets":[
    {"key":"h2h","outcomes":[{"name":"Tampa Bay Rays","price":-215},{"name":"Athletics","price":180}]},
    {"key":"spreads","outcomes":[{"name":"Tampa Bay Rays","price":-110,"point":-1.5},
                                 {"name":"Athletics","price":-110,"point":1.5}]},
    {"key":"totals","outcomes":[{"name":"Over","price":100,"point":8.5},
                                {"name":"Under","price":-120,"point":8.5}]}]},
   {"key":"betmgm","markets":[
    {"key":"h2h","outcomes":[{"name":"Tampa Bay Rays","price":-225},{"name":"Athletics","price":185}]}]},
   {"key":"caesars","markets":[
    {"key":"h2h","outcomes":[{"name":"Tampa Bay Rays","price":-240},{"name":"Athletics","price":198}]}]}]},
 {"id":"evt_lad_cin","sport_key":"baseball_mlb","commence_time":"2026-09-15T22:40:00Z",
  "home_team":"Cincinnati Reds","away_team":"Los Angeles Dodgers","bookmakers":[
   {"key":"fanduel","markets":[
    {"key":"h2h","outcomes":[{"name":"Cincinnati Reds","price":200},{"name":"Los Angeles Dodgers","price":-240}]},
    {"key":"totals","outcomes":[{"name":"Over","price":-110,"point":8.5},
                                {"name":"Under","price":-110,"point":8.5}]}]},
   {"key":"draftkings","markets":[
    {"key":"h2h","outcomes":[{"name":"Cincinnati Reds","price":215},{"name":"Los Angeles Dodgers","price":-250}]},
    {"key":"totals","outcomes":[{"name":"Over","price":-105,"point":8.5},
                                {"name":"Under","price":-115,"point":8.5}]}]},
   {"key":"betmgm","markets":[
    {"key":"h2h","outcomes":[{"name":"Cincinnati Reds","price":205},{"name":"Los Angeles Dodgers","price":-245}]}]}]},
 {"id":"evt_mil_pit","sport_key":"baseball_mlb","commence_time":"2026-09-15T22:40:00Z",
  "home_team":"Pittsburgh Pirates","away_team":"Milwaukee Brewers","bookmakers":[
   {"key":"fanduel","markets":[
    {"key":"h2h","outcomes":[{"name":"Pittsburgh Pirates","price":200},{"name":"Milwaukee Brewers","price":-240}]},
    {"key":"spreads","outcomes":[{"name":"Pittsburgh Pirates","price":110,"point":1.5},
                                 {"name":"Milwaukee Brewers","price":-135,"point":-1.5}]},
    {"key":"totals","outcomes":[{"name":"Over","price":-110,"point":7.5},
                                {"name":"Under","price":-110,"point":7.5}]}]},
   {"key":"draftkings","markets":[
    {"key":"h2h","outcomes":[{"name":"Pittsburgh Pirates","price":225},{"name":"Milwaukee Brewers","price":-235}]}]},
   {"key":"betmgm","markets":[
    {"key":"h2h","outcomes":[{"name":"Pittsburgh Pirates","price":195},{"name":"Milwaukee Brewers","price":-250}]}]},
   {"key":"caesars","markets":[
    {"key":"h2h","outcomes":[{"name":"Pittsburgh Pirates","price":198},{"name":"Milwaukee Brewers","price":-242}]}]}]}]

ESPN_FIXTURE = [
 {"espn_id":"401","league":"mlb","name":"Athletics at Tampa Bay Rays","short_name":"ATH @ TB",
  "start_utc":"2026-09-15T22:40:00Z","state":"pre","status_detail":"6:40 PM ET","completed":False,
  "venue":"George M. Steinbrenner Field","broadcast":None,
  "home":{"abbr":"TB","name":"Tampa Bay Rays","score":None,"record":"78-72"},
  "away":{"abbr":"ATH","name":"Athletics","score":None,"record":"66-84"}},
 {"espn_id":"402","league":"mlb","name":"Los Angeles Dodgers at Cincinnati Reds","short_name":"LAD @ CIN",
  "start_utc":"2026-09-15T22:40:00Z","state":"pre","status_detail":"6:40 PM ET","completed":False,
  "venue":"Great American Ball Park","broadcast":None,
  "home":{"abbr":"CIN","name":"Cincinnati Reds","score":None,"record":"77-73"},
  "away":{"abbr":"LAD","name":"Los Angeles Dodgers","score":None,"record":"90-60"}},
 {"espn_id":"403","league":"mlb","name":"Milwaukee Brewers at Pittsburgh Pirates","short_name":"MIL @ PIT",
  "start_utc":"2026-09-15T22:40:00Z","state":"pre","status_detail":"6:40 PM ET","completed":False,
  "venue":"PNC Park","broadcast":None,
  "home":{"abbr":"PIT","name":"Pittsburgh Pirates","score":None,"record":"68-82"},
  "away":{"abbr":"MIL","name":"Milwaukee Brewers","score":None,"record":"92-58"}},
 {"espn_id":"404","league":"mlb","name":"Seattle Mariners at Los Angeles Angels","short_name":"SEA @ LAA",
  "start_utc":"2026-09-16T01:38:00Z","state":"pre","status_detail":"9:38 PM ET","completed":False,
  "venue":"Angel Stadium","broadcast":None,
  "home":{"abbr":"LAA","name":"Los Angeles Angels","score":None,"record":"70-80"},
  "away":{"abbr":"SEA","name":"Seattle Mariners","score":None,"record":"85-65"}}]


# ---------------------------------------------------------------------------
# Synthetic fixtures for the other leagues.
#
# Each book row is (ml_home, ml_away, spread_home_point, spread_home_price,
# spread_away_price, total_point, over_price, under_price); None skips that
# market at that book. Built into the exact Odds API v4 shape below.
# ---------------------------------------------------------------------------

def _event(eid: str, sport_key: str, start: str, home: str, away: str,
           books: dict[str, tuple]) -> dict:
    bms = []
    for key, (mlh, mla, sp, sph, spa, tot, ov, un) in books.items():
        markets = []
        if mlh is not None:
            markets.append({"key": "h2h", "outcomes": [
                {"name": home, "price": mlh}, {"name": away, "price": mla}]})
        if sp is not None:
            markets.append({"key": "spreads", "outcomes": [
                {"name": home, "price": sph, "point": sp},
                {"name": away, "price": spa, "point": -sp}]})
        if tot is not None:
            markets.append({"key": "totals", "outcomes": [
                {"name": "Over", "price": ov, "point": tot},
                {"name": "Under", "price": un, "point": tot}]})
        bms.append({"key": key, "last_update": start, "markets": markets})
    return {"id": eid, "sport_key": sport_key, "commence_time": start,
            "home_team": home, "away_team": away, "bookmakers": bms}


def _espn(eid: str, league: str, start: str, detail: str, venue: str,
          home: tuple[str, str, str], away: tuple[str, str, str]) -> dict:
    return {"espn_id": eid, "league": league,
            "name": f"{away[1]} at {home[1]}", "short_name": f"{away[0]} @ {home[0]}",
            "start_utc": start, "state": "pre", "status_detail": detail,
            "completed": False, "venue": venue, "broadcast": None,
            "home": {"abbr": home[0], "name": home[1], "score": None, "record": home[2]},
            "away": {"abbr": away[0], "name": away[1], "score": None, "record": away[2]}}


NFL_ODDS = [
 # DraftKings deals -3 while everyone else is at -3.5: the board must not
 # average a -3 price into a -3.5 consensus.
 _event("evt_kc_buf", "americanfootball_nfl", "2026-10-04T17:00:00Z",
        "Buffalo Bills", "Kansas City Chiefs", {
    "fanduel":    (-175, 146, -3.5, -105, -115, 47.5, -110, -110),
    "draftkings": (-180, 150, -3.0, -120, 100, 47.5, -108, -112),
    "betmgm":     (-170, 142, -3.5, -110, -110, 48.0, -110, -110),
    "caesars":    (-178, 148, -3.5, -108, -112, 47.5, -105, -115)}),
 _event("evt_dal_phi", "americanfootball_nfl", "2026-10-04T20:25:00Z",
        "Philadelphia Eagles", "Dallas Cowboys", {
    "fanduel":    (-290, 235, -7.0, -110, -110, 44.0, -110, -110),
    "draftkings": (-275, 225, -7.0, -115, -105, 44.0, -105, -115),
    "betmgm":     (-300, 240, -7.0, -110, -110, 44.0, -110, -110),
    "caesars":    (-285, 230, -7.0, -112, -108, 44.0, -112, -108)}),
 _event("evt_sf_sea", "americanfootball_nfl", "2026-10-05T00:20:00Z",
        "Seattle Seahawks", "San Francisco 49ers", {
    "fanduel":    (138, -142, 2.5, -110, -110, 43.5, -110, -110),   # stale home ML
    "draftkings": (124, -148, 2.5, -112, -108, 43.5, -115, -105),
    "betmgm":     (118, -140, 2.5, -110, -110, None, None, None)})]

NFL_ESPN = [
 _espn("501", "nfl", "2026-10-04T17:00:00Z", "Sun 1:00 PM ET", "Highmark Stadium",
       ("BUF", "Buffalo Bills", "3-0"), ("KC", "Kansas City Chiefs", "2-1")),
 _espn("502", "nfl", "2026-10-04T20:25:00Z", "Sun 4:25 PM ET", "Lincoln Financial Field",
       ("PHI", "Philadelphia Eagles", "3-0"), ("DAL", "Dallas Cowboys", "1-2")),
 _espn("503", "nfl", "2026-10-05T00:20:00Z", "Sun 8:20 PM ET", "Lumen Field",
       ("SEA", "Seattle Seahawks", "2-1"), ("SF", "San Francisco 49ers", "2-1")),
 _espn("504", "nfl", "2026-10-06T00:15:00Z", "Mon 8:15 PM ET", "Soldier Field",
       ("CHI", "Chicago Bears", "1-2"), ("GB", "Green Bay Packers", "2-1"))]

NCAAF_ODDS = [
 _event("evt_mich_osu", "americanfootball_ncaaf", "2026-10-03T16:00:00Z",
        "Ohio State Buckeyes", "Michigan Wolverines", {
    "fanduel":    (-400, 310, -10.5, -110, -110, 45.5, -110, -110),
    "draftkings": (-380, 300, -10.5, -105, -115, 45.5, -112, -108),
    "betmgm":     (-410, 320, -10.5, -110, -110, 46.0, -110, -110),
    "caesars":    (-395, 305, -10.0, -115, -105, 45.5, -110, -110)}),
 _event("evt_uga_bama", "americanfootball_ncaaf", "2026-10-03T19:30:00Z",
        "Alabama Crimson Tide", "Georgia Bulldogs", {
    "fanduel":    (-125, 105, -2.5, -110, -110, 51.5, -110, -110),
    "draftkings": (-130, 128, -2.5, -115, -105, 51.5, -110, -110),  # away ML outlier
    "betmgm":     (-120, 100, -2.5, -110, -110, 51.5, -105, -115),
    "caesars":    (-128, 108, -2.5, -112, -108, 51.5, -110, -110)}),
 _event("evt_miami_fsu", "americanfootball_ncaaf", "2026-10-03T23:30:00Z",
        "Florida State Seminoles", "Miami Hurricanes", {
    "fanduel":    (165, -198, 4.5, -110, -110, 55.5, -110, -110),
    "draftkings": (170, -205, 4.5, -108, -112, 55.5, -110, -110),
    "betmgm":     (160, -190, 4.5, -110, -110, 55.5, -110, -110)})]

NCAAF_ESPN = [
 _espn("601", "ncaaf", "2026-10-03T16:00:00Z", "Sat 12:00 PM ET", "Ohio Stadium",
       ("OSU", "Ohio State Buckeyes", "4-0"), ("MICH", "Michigan Wolverines", "3-1")),
 _espn("602", "ncaaf", "2026-10-03T19:30:00Z", "Sat 3:30 PM ET", "Bryant-Denny Stadium",
       ("ALA", "Alabama Crimson Tide", "4-0"), ("UGA", "Georgia Bulldogs", "4-0")),
 _espn("603", "ncaaf", "2026-10-03T23:30:00Z", "Sat 7:30 PM ET", "Doak Campbell Stadium",
       ("FSU", "Florida State Seminoles", "2-2"), ("MIA", "Miami Hurricanes", "4-0")),
 # a different Miami, a week later: the start-time gate keeps it from
 # stealing the Hurricanes' odds
 _espn("604", "ncaaf", "2026-10-10T16:00:00Z", "Sat 12:00 PM ET", "Yager Stadium",
       ("M-OH", "Miami (OH) RedHawks", "2-2"), ("OHIO", "Ohio Bobcats", "3-1"))]

NBA_ODDS = [
 _event("evt_bos_nyk", "basketball_nba", "2026-10-21T23:30:00Z",
        "New York Knicks", "Boston Celtics", {
    "fanduel":    (105, -125, 1.5, -110, -110, 221.5, -110, -110),
    "draftkings": (125, -130, 1.5, -108, -112, 222.0, -110, -110),  # home ML outlier
    "betmgm":     (102, -122, 1.5, -110, -110, 221.5, -112, -108),
    "caesars":    (108, -128, 2.0, -110, -110, 221.5, -108, -112)}),
 _event("evt_den_lal", "basketball_nba", "2026-10-22T02:00:00Z",
        "Los Angeles Lakers", "Denver Nuggets", {
    "fanduel":    (135, -160, 3.5, -110, -110, 229.5, -110, -110),
    "draftkings": (140, -165, 3.5, -110, -110, 229.5, -115, -105),
    "betmgm":     (130, -155, 3.5, -108, -112, 229.5, -110, -110),
    "caesars":    (138, -162, 3.5, -110, -110, 229.5, -110, -110)})]

NBA_ESPN = [
 _espn("701", "nba", "2026-10-21T23:30:00Z", "Wed 7:30 PM ET", "Madison Square Garden",
       ("NY", "New York Knicks", "0-0"), ("BOS", "Boston Celtics", "0-0")),
 _espn("702", "nba", "2026-10-22T02:00:00Z", "Wed 10:00 PM ET", "Crypto.com Arena",
       ("LAL", "Los Angeles Lakers", "0-0"), ("DEN", "Denver Nuggets", "0-0"))]

NHL_ODDS = [
 # hockey spreads are the puck line; hockey totals of 6 can push
 _event("evt_tor_mtl", "icehockey_nhl", "2026-10-08T23:00:00Z",
        "Montreal Canadiens", "Toronto Maple Leafs", {
    "fanduel":    (130, -155, 1.5, -190, 158, 6.0, -105, -115),
    "draftkings": (135, -160, 1.5, -185, 154, 6.0, -110, -110),
    "betmgm":     (128, -152, 1.5, -195, 160, 6.0, -108, -112),
    "caesars":    (132, -158, 1.5, -188, 156, 6.5, 120, -145)}),
 _event("evt_edm_vgk", "icehockey_nhl", "2026-10-09T02:00:00Z",
        "Vegas Golden Knights", "Edmonton Oilers", {
    "fanduel":    (-115, 112, -1.5, 205, -250, 6.5, -105, -115),    # away ML outlier
    "draftkings": (-112, -108, -1.5, 210, -258, 6.5, -102, -118),
    "betmgm":     (-118, -102, -1.5, 200, -245, 6.5, -105, -115)})]

NHL_ESPN = [
 _espn("801", "nhl", "2026-10-08T23:00:00Z", "Thu 7:00 PM ET", "Bell Centre",
       ("MTL", "Montreal Canadiens", "0-0-0"), ("TOR", "Toronto Maple Leafs", "0-0-0")),
 _espn("802", "nhl", "2026-10-09T02:00:00Z", "Thu 10:00 PM ET", "T-Mobile Arena",
       ("VGK", "Vegas Golden Knights", "0-0-0"), ("EDM", "Edmonton Oilers", "0-0-0"))]


# ---------------------------------------------------------------------------
# Props. Rows are (market, player, point, {book: (over_or_yes, under_or_no)});
# a None second price means the book only hangs one side (common on
# anytime-scorer markets), which the app must report as un-de-viggable.
# ---------------------------------------------------------------------------

def _props(event: dict, rows: list[tuple]) -> dict:
    by_book: dict[str, dict[str, list[dict]]] = {}
    for market, player, point, books in rows:
        yes_no = point is None
        for book, (a, b) in books.items():
            outs = by_book.setdefault(book, {}).setdefault(market, [])
            first, second = ("Yes", "No") if yes_no else ("Over", "Under")
            outs.append({"name": first, "description": player, "price": a, "point": point})
            if b is not None:
                outs.append({"name": second, "description": player, "price": b, "point": point})
    return {
        "id": event["id"], "sport_key": event["sport_key"],
        "commence_time": event["commence_time"],
        "home_team": event["home_team"], "away_team": event["away_team"],
        "bookmakers": [
            {"key": book, "markets": [{"key": m, "outcomes": o} for m, o in mk.items()]}
            for book, mk in by_book.items()
        ],
    }


def _by_id(events: list[dict], eid: str) -> dict:
    return next(e for e in events if e["id"] == eid)


PROPS_FIXTURE: dict[str, dict] = {
 "evt_ath_tb": _props(_by_id(ODDS_FIXTURE, "evt_ath_tb"), [
    ("pitcher_strikeouts", "Shane Baz", 5.5, {
        "fanduel": (-135, 110), "draftkings": (-120, -105),
        "betmgm": (-130, 105), "caesars": (-125, 100)}),
    ("batter_hits", "Junior Caminero", 0.5, {
        "fanduel": (-210, 165), "draftkings": (-190, 150),
        "betmgm": (-200, 160), "caesars": (-205, 162)}),
    ("batter_home_runs", "Brent Rooker", 0.5, {
        "fanduel": (330, None), "draftkings": (310, -450),
        "betmgm": (300, -425), "caesars": (320, -460)})]),
 "evt_kc_buf": _props(_by_id(NFL_ODDS, "evt_kc_buf"), [
    ("player_pass_yds", "Josh Allen", 254.5, {
        "fanduel": (-115, -115), "draftkings": (-110, -120),
        "betmgm": (-112, -118), "caesars": (-120, -110)}),
    ("player_pass_yds", "Patrick Mahomes", 262.5, {
        "fanduel": (-110, -120), "draftkings": (-118, -112),
        "betmgm": (-115, -115)}),
    # a different number at one book is a different bet, not a bad price
    ("player_pass_yds", "Patrick Mahomes", 259.5, {"caesars": (-130, 100)}),
    ("player_rush_yds", "James Cook", 64.5, {
        "fanduel": (112, -142), "draftkings": (-105, -125),
        "betmgm": (-110, -120), "caesars": (-112, -118)}),
    ("player_anytime_td", "James Cook", None, {
        "fanduel": (-105, None), "draftkings": (-110, -120),
        "betmgm": (-108, -122), "caesars": (-102, -128)}),
    ("player_anytime_td", "Travis Kelce", None, {
        "fanduel": (175, None), "draftkings": (160, -210)})]),
 "evt_mich_osu": _props(_by_id(NCAAF_ODDS, "evt_mich_osu"), [
    ("player_pass_yds", "Julian Sayin", 224.5, {
        "fanduel": (-115, -115), "draftkings": (-120, -110),
        "betmgm": (-110, -120), "caesars": (-112, -118)}),
    ("player_rush_yds", "Justice Haynes", 71.5, {
        "fanduel": (-110, -120), "draftkings": (-115, -115),
        "betmgm": (-118, -112)})]),
 "evt_bos_nyk": _props(_by_id(NBA_ODDS, "evt_bos_nyk"), [
    ("player_points", "Jalen Brunson", 26.5, {
        "fanduel": (-120, -110), "draftkings": (118, -150),
        "betmgm": (-115, -115), "caesars": (-105, -125)}),
    ("player_points", "Jaylen Brown", 24.5, {
        "fanduel": (-110, -120), "draftkings": (-115, -115),
        "betmgm": (-108, -122), "caesars": (-112, -118)}),
    ("player_rebounds", "Karl-Anthony Towns", 11.5, {
        "fanduel": (-105, -125), "draftkings": (-125, -105),
        "betmgm": (-118, -112), "caesars": (-120, -110)})]),
 "evt_tor_mtl": _props(_by_id(NHL_ODDS, "evt_tor_mtl"), [
    ("player_shots_on_goal", "Auston Matthews", 3.5, {
        "fanduel": (-130, 105), "draftkings": (-145, 125),
        "betmgm": (-125, 100), "caesars": (-135, 110)}),
    ("player_points", "Nick Suzuki", 0.5, {
        "fanduel": (-150, 120), "draftkings": (-140, 115),
        "betmgm": (-145, 118)}),
    ("player_goal_scorer_anytime", "Auston Matthews", None, {
        "fanduel": (125, None), "draftkings": (120, None), "betmgm": (130, None)})]),
}


INJURIES_FIXTURE: dict[str, list[dict]] = {
 "nfl": [
    {"team": "KC", "player": "Xavier Worthy", "position": "WR", "status": "Questionable",
     "detail": "Ankle", "date": "2026-10-02T00:00Z"},
    {"team": "BUF", "player": "Dalton Kincaid", "position": "TE", "status": "Out",
     "detail": "Knee", "date": "2026-10-02T00:00Z"}],
 "nba": [
    {"team": "BOS", "player": "Jayson Tatum", "position": "SF", "status": "Out",
     "detail": "Achilles", "date": "2026-10-20T00:00Z"}],
}

# league -> (odds events, ESPN slate). The source label travels with them so
# no response can pass a synthetic board off as a real one.
DEMO_LEAGUES: dict[str, dict] = {
    "mlb":   {"odds": ODDS_FIXTURE, "espn": ESPN_FIXTURE,
              "label": "DEMO FIXTURE (frozen Sep 15 2026)"},
    "nfl":   {"odds": NFL_ODDS, "espn": NFL_ESPN,
              "label": "DEMO FIXTURE (synthetic NFL slate)"},
    "ncaaf": {"odds": NCAAF_ODDS, "espn": NCAAF_ESPN,
              "label": "DEMO FIXTURE (synthetic NCAAF slate)"},
    "nba":   {"odds": NBA_ODDS, "espn": NBA_ESPN,
              "label": "DEMO FIXTURE (synthetic NBA slate)"},
    "nhl":   {"odds": NHL_ODDS, "espn": NHL_ESPN,
              "label": "DEMO FIXTURE (synthetic NHL slate)"},
}
PROPS_LABEL = "DEMO FIXTURE (synthetic props)"


def demo_league(league: str) -> dict | None:
    return DEMO_LEAGUES.get(league)


def demo_event_markets(event_id: str, markets: tuple[str, ...]) -> dict | None:
    """The props fixture for one event, filtered to the markets asked for."""
    blob = PROPS_FIXTURE.get(event_id)
    if blob is None:
        return None
    return {**blob, "bookmakers": [
        {**bm, "markets": [m for m in bm["markets"] if m["key"] in markets]}
        for bm in blob["bookmakers"]
    ]}
