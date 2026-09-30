// Shared in-page audit: horizontal overflow and small tap targets.
function audit() {
  const vw = document.documentElement.clientWidth;
  const out = { scrollWidth: document.documentElement.scrollWidth, vw, overflow: [], small: [] };
  for (const el of document.querySelectorAll("body *")) {
    const r = el.getBoundingClientRect();
    if (!r.width || !r.height) continue;
    const cs = getComputedStyle(el);
    if (cs.visibility === "hidden") continue;
    if (r.right > vw + 1 && !el.closest("svg")) {
      // ignore children of containers that scroll horizontally themselves
      let p = el.parentElement, clipped = false;
      while (p) {
        const o = getComputedStyle(p).overflowX;
        if (o === "auto" || o === "scroll" || o === "hidden") { clipped = true; break; }
        p = p.parentElement;
      }
      if (!clipped) out.overflow.push(`${el.tagName.toLowerCase()}#${el.id}.${el.className} right=${Math.round(r.right)}`);
    }
    if (el.matches("button, a[href], select, input, summary, [role=button]")) {
      if (r.height < 44 || r.width < 44) out.small.push(`${el.tagName.toLowerCase()}#${el.id}.${String(el.className).split(" ")[0]} ${Math.round(r.width)}x${Math.round(r.height)} "${el.textContent.trim().slice(0, 20)}"`);
    }
  }
  return out;
}
module.exports = { audit };
