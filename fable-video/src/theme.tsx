import React from "react";
import { AbsoluteFill, Easing, interpolate, staticFile, useCurrentFrame } from "remotion";
import { loadFont } from "@remotion/fonts";

// Fonts are bundled in public/fonts (SIL Open Font License) so renders work offline.
export const serif = "Cormorant Garamond";
export const sans = "Jost";
const faces: [string, string, string, "normal" | "italic"][] = [
  [serif, "cormorant-garamond", "300", "normal"],
  [serif, "cormorant-garamond", "300", "italic"],
  [serif, "cormorant-garamond", "400", "normal"],
  [serif, "cormorant-garamond", "400", "italic"],
  [sans, "jost", "300", "normal"],
  [sans, "jost", "400", "normal"],
  [sans, "jost", "500", "normal"],
];
for (const [family, file, weight, style] of faces) {
  loadFont({ family, weight, style, url: staticFile(`fonts/${file}-latin-${weight}-${style}.woff2`) });
}

// House palette: linen, stone and espresso neutrals, olive as the one green, rust as a small accent.
export const C = {
  linen: "#F2EDE4",
  stone: "#D9CFC1",
  taupe: "#A8977F",
  espresso: "#3B2A1E",
  olive: "#737158",
  deepOlive: "#4A4A36",
  sage: "#B9BDA3",
  rust: "#6E2A20",
};

export const ease = Easing.bezier(0.16, 1, 0.3, 1);
const clamp = { extrapolateLeft: "clamp", extrapolateRight: "clamp" } as const;

/** 0 → 1 progress starting at `start`, lasting `dur` frames. */
export const prog = (frame: number, start: number, dur = 24) =>
  interpolate(frame, [start, start + dur], [0, 1], { ...clamp, easing: ease });

/** Fade + rise entrance style. */
export const rise = (frame: number, start: number, dur = 26, dist = 36) => {
  const p = prog(frame, start, dur);
  return { opacity: p, translate: `0px ${(1 - p) * dist}px` };
};

/** Small tracked-caps label. */
export const caps = (size = 24, color: string = C.olive): React.CSSProperties => ({
  fontFamily: sans,
  fontWeight: 500,
  fontSize: size,
  letterSpacing: "0.22em",
  textTransform: "uppercase",
  color,
});

export const nums: React.CSSProperties = { fontVariantNumeric: "lining-nums tabular-nums" };

/** The one italic, rust-coloured phrase in a headline. */
export const Em: React.FC<{ children: React.ReactNode; color?: string }> = ({ children, color = C.rust }) => (
  <span style={{ fontStyle: "italic", color }}>{children}</span>
);

export const Background: React.FC = () => <AbsoluteFill style={{ backgroundColor: C.linen }} />;

/** A hairline that draws in from the left. */
export const Rule: React.FC<{ at: number; color?: string; style?: React.CSSProperties }> = ({
  at,
  color = C.taupe,
  style,
}) => {
  const frame = useCurrentFrame();
  return <div style={{ height: 2, background: color, width: `${prog(frame, at, 40) * 100}%`, ...style }} />;
};

/** Standard scene chrome: numbered eyebrow, serif headline, hairline, body, source line. */
export const Scene: React.FC<{
  index: string;
  kicker: string;
  title: React.ReactNode;
  source?: string;
  children: React.ReactNode;
}> = ({ index, kicker, title, source, children }) => {
  const frame = useCurrentFrame();
  return (
    <AbsoluteFill style={{ padding: "100px 130px 84px", fontFamily: serif, color: C.espresso, backgroundColor: C.linen, fontVariantNumeric: "lining-nums" }}>
      <div style={{ ...rise(frame, 4, 22, 16), ...caps(24), display: "flex", justifyContent: "space-between" }}>
        <span>
          {index} &nbsp;/&nbsp; {kicker}
        </span>
        <span style={{ color: C.taupe }}>Claude Fable 5.2</span>
      </div>
      <div style={{ ...rise(frame, 10, 30), fontSize: 132, fontWeight: 300, lineHeight: 1.02, letterSpacing: -2, marginTop: 30 }}>
        {title}
      </div>
      <Rule at={18} style={{ marginTop: 40 }} />
      <div style={{ flex: 1, display: "flex", alignItems: "center", minHeight: 0, padding: "34px 0" }}>{children}</div>
      {source ? (
        <div style={{ ...caps(21, C.espresso), letterSpacing: "0.14em", fontWeight: 400, opacity: prog(frame, 40, 30) * 0.62 }}>
          {source}
        </div>
      ) : null}
    </AbsoluteFill>
  );
};

/** Numbered 01 / 02 / 03 points separated by hairlines. */
export const Points: React.FC<{ items: string[]; start?: number; gap?: number }> = ({ items, start = 32, gap = 22 }) => {
  const frame = useCurrentFrame();
  return (
    <div style={{ display: "flex", flexDirection: "column", width: "100%" }}>
      {items.map((t, i) => (
        <div
          key={t}
          style={{
            ...rise(frame, start + i * gap),
            display: "flex",
            gap: 40,
            alignItems: "baseline",
            padding: "30px 0",
            borderTop: i === 0 ? "none" : `2px solid ${C.stone}`,
          }}
        >
          <span style={{ ...caps(22, C.taupe), flex: "none", width: 50 }}>0{i + 1}</span>
          <span style={{ fontSize: 54, lineHeight: 1.12, fontWeight: 400 }}>{t}</span>
        </div>
      ))}
    </div>
  );
};
