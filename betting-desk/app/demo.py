"""
Fixture data so the app runs end to end with no API key and no credits spent.

Set DEMO_MODE=1. Prices are the real ones read off the Hard Rock and
public boards on Sep 15 2026, with three extra books added so the
consensus logic has something to work with. It is frozen, not live.
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
