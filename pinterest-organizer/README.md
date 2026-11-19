# Pinterest Organizer (Windows)

A small tool that runs on your computer and:

1. **Opens Pinterest** in its own browser window.
2. **Sorts your pins** into categories, and makes new categories when it sees
   several pins about the same thing (for example, a "Sourdough" section once
   enough of your pins mention sourdough).
3. **Finds pins you might like** and shows them as a page of photos, each with a
   tick box. You tick the ones you want and press **Pin selected**. They're saved
   into the right category, and **Refresh** brings a new batch you haven't seen.

It never likes, saves or follows anything on its own. Nothing is pinned unless
you tick it.

---

## One-time setup (about 15 minutes)

### 1. Install Python
Download Python 3.11 or newer from <https://www.python.org/downloads/>.
On the first install screen, **tick "Add python.exe to PATH"**.

### 2. Make a Pinterest developer app (free)
This is what lets the tool talk to your account through Pinterest's official API.

1. Go to <https://developers.pinterest.com/apps/> and sign in with your Pinterest account.
2. Click **Connect app** / **Create app**. Any name works (e.g. "My Pin Organizer").
3. In the app's settings, add this **Redirect URI** exactly:
   `http://localhost:8085/callback`
4. Copy the **App ID** and **App secret key**.

> New apps start with **Trial access**. If saving or moving pins fails with an
> access error, click **Request Standard access** on the app page and say it's a
> personal tool for organizing your own pins. While you wait, you can try
> `api_base: "https://api-sandbox.pinterest.com/v5"` in `config.yaml`.

### 3. Install the tool
Double-click **`setup.bat`**. It installs everything and creates `config.yaml`.

### 4. Fill in config.yaml
Open `config.yaml` in Notepad, paste the App ID and secret into `client_id` and
`client_secret`, and edit the **categories** list to match what you pin. Each
category is a name followed by words that mean a pin belongs there:

```yaml
categories:
  Recipes: [recipe, dinner, dessert, baking, cake]
  Home Decor: [decor, living room, bedroom, cozy]
```

You don't have to think of everything. The tool adds categories on its own.

---

## Using it

Double-click **`Pinterest Organizer.bat`**. You'll get a menu:

| Option | What it does |
|---|---|
| **1. Open Pinterest** | Opens Pinterest in the tool's browser. **Sign in here once.** The pin finder uses this window. |
| **2. Connect to my Pinterest account** | First time only. Approve access on Pinterest's own page. |
| **3. Sort my pins: PREVIEW** | Shows where every pin *would* go and which new categories it would make. Changes nothing. |
| **4. Sort my pins: DO IT** | Moves the pins. |
| **5. Find pins I might like** | Opens the pin picker in your normal browser. |

### The pin picker
- Press **Refresh: show me more**. A browser window opens, searches Pinterest for
  a few of your categories (it cycles through them each time) and fills the page
  with photos. It goes slowly on purpose, so give it a minute.
- Tick the pins you like (click the photo or the box). The dropdown under each
  photo shows the category it will go into. Change it if you want.
- Press **Pin selected**. The pins are saved into that category's section
  (or board), and new sections are created as needed.
- Want something specific? Type in the search box (e.g. "cozy reading nook")
  before pressing Refresh.
- Pins you've seen and passed on don't come back.

### Sections or boards?
In `config.yaml`, `organize_as: sections` (the default) puts every category as a
section inside one board called "Sorted Pins". Use `organize_as: boards` to give
each category its own board instead. To protect boards you've already arranged,
list them under `skip_boards`.

---

## Good to know
- **Always run the preview (option 3) first.** Sorting moves pins out of their
  current boards into the sorted board or sections.
- Your login, settings and history stay in `config.yaml` and the `state` folder on
  your computer. Delete `state\token.json` to disconnect the tool.
- **How the pin finder works:** Pinterest's API has no search for apps like this, so the finder reads
  Pinterest's search results in a browser signed in as you. It only reads, at a human pace, and only
  when you press Refresh, but it is still automated browsing, which Pinterest's terms don't explicitly
  allow. Keep refreshes reasonable. If Pinterest changes its page layout, the finder may stop finding
  pins; the fix goes in `pinboard/discover.py`.
- Sorting and saving use the official API, which is fully allowed.

## For tinkering
```
python main.py sort            # preview
python main.py sort --apply    # move pins
python main.py discover        # pin picker
python -m unittest discover tests
```
