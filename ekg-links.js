/*
 * Shareable links: open the trainer straight at a rhythm or a mode, so a
 * post or a website button can point at exactly what it talks about.
 *
 *   ekg-test.html?rhythm=afib            study guide, AFib on the strip
 *   ekg-test.html?rhythm=afib&tab=meds   same, with its medications below
 *   ekg-test.html?mode=sprint            any mode button on the menu
 *   ekg-test.html?embed=1                hide the site header (set in <head>)
 *
 * Unknown values are ignored and the menu shows as usual. A quiz is never
 * started over an unfinished one: the resume banner wins.
 */

(function () {
  const MODES = ["study", "sprint", "practice", "simulation", "exam", "scenario", "progress"];

  function handle() {
    const params = new URLSearchParams(window.location.search);
    const rhythm = params.get("rhythm");
    const mode = params.get("mode");
    const tab = params.get("tab");
    if (!rhythm && !mode) return;

    const known = rhythm && EkgEducation.RHYTHM_GUIDE[rhythm];
    if (known && (!mode || mode === "study")) {
      window.EkgStudy.openAt(rhythm, tab === "meds" ? "meds" : "rhythms");
    } else if (MODES.includes(mode)) {
      const resume = document.getElementById("resume-banner");
      const quiz = ["practice", "simulation", "exam"].includes(mode);
      if (!(quiz && resume && !resume.hidden)) {
        const btn = document.getElementById(`mode-${mode}`);
        if (btn) btn.click();
      }
    }

    // Done with them: a reload or "back to menu" shouldn't jump again.
    params.delete("rhythm");
    params.delete("mode");
    params.delete("tab");
    const rest = params.toString();
    try {
      history.replaceState(null, "", window.location.pathname + (rest ? `?${rest}` : "") + window.location.hash);
    } catch {}
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", handle);
  else handle();
})();
