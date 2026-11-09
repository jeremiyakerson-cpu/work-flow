import React from "react";
import { AbsoluteFill, Easing, interpolate, staticFile, useCurrentFrame } from "remotion";
import { loadFont } from "@remotion/fonts";

// Fonts are bundled in public/fonts (SIL Open Font License) so renders work offline.
export const display = "Space Grotesk";
export const mono = "JetBrains Mono";
const faces: [string, string, string][] = [
  [display, "space-grotesk", "400"],
  [display, "space-grotesk", "500"],
  [display, "space-grotesk", "700"],
  [mono, "jetbrains-mono", "400"],
  [mono, "jetbrains-mono", "500"],
];
for (const [family, file, weight] of faces) {
  loadFont({ family, weight, url: staticFile(`fonts/${file}-latin-${weight}-normal.woff2`) });
}

export const C = {
  bg: "#0D0F14",
  panel: "#161A22",
  line: "#2A303C",
  text: "#F3EFE7",
  muted: "#A0A7B4",
  accent: "#F4A657",
  teal: "#5FD4C4",
  barMid: "#6B7382",
  barLow: "#3D4350",
};

export const ease = Easing.bezier(0.16, 1, 0.3, 1);
const clamp = { extrapolateLeft: "clamp", extrapolateRight: "clamp" } as const;

/** 0 → 1 progress starting at `start`, lasting `dur` frames. */
export const prog = (frame: number, start: number, dur = 20) =>
  interpolate(frame, [start, start + dur], [0, 1], { ...clamp, easing: ease });

/** Fade + rise entrance style. */
export const rise = (frame: number, start: number, dur = 20, dist = 44) => {
  const p = prog(frame, start, dur);
  return { opacity: p, translate: `0px ${(1 - p) * dist}px` };
};

export const Background: React.FC = () => {
  const frame = useCurrentFrame();
  const x = 30 + Math.sin(frame / 140) * 12;
  const y = 28 + Math.cos(frame / 170) * 10;
  return (
    <AbsoluteFill style={{ backgroundColor: C.bg }}>
      <AbsoluteFill
        style={{
          background: `radial-gradient(900px 700px at ${x}% ${y}%, rgba(244,166,87,0.16), transparent 70%), radial-gradient(1000px 800px at ${100 - x}% ${100 - y}%, rgba(95,212,196,0.10), transparent 70%)`,
        }}
      />
      <AbsoluteFill
        style={{
          backgroundImage: `linear-gradient(${C.line}55 1px, transparent 1px), linear-gradient(90deg, ${C.line}55 1px, transparent 1px)`,
          backgroundSize: "96px 96px",
          backgroundPosition: `${-frame * 0.25}px ${-frame * 0.15}px`,
          opacity: 0.35,
          maskImage: "radial-gradient(ellipse at center, black 30%, transparent 85%)",
        }}
      />
    </AbsoluteFill>
  );
};

/** Standard scene chrome: numbered kicker, headline, body, optional source line. */
export const Scene: React.FC<{
  index: string;
  kicker: string;
  title: React.ReactNode;
  source?: string;
  children: React.ReactNode;
}> = ({ index, kicker, title, source, children }) => {
  const frame = useCurrentFrame();
  return (
    <AbsoluteFill style={{ padding: "96px 120px", fontFamily: display, color: C.text }}>
      <div
        style={{
          ...rise(frame, 4, 18, 20),
          fontFamily: mono,
          fontSize: 28,
          letterSpacing: 4,
          color: C.accent,
          display: "flex",
          alignItems: "center",
          gap: 20,
        }}
      >
        <span>{index}</span>
        <span style={{ width: 64 * prog(frame, 8, 24), height: 2, background: C.accent }} />
        <span>{kicker}</span>
      </div>
      <div
        style={{
          ...rise(frame, 10, 24),
          fontSize: 96,
          fontWeight: 700,
          lineHeight: 1.05,
          letterSpacing: -2.5,
          marginTop: 28,
        }}
      >
        {title}
      </div>
      <div style={{ flex: 1, display: "flex", alignItems: "center", margin: "40px 0", minHeight: 0 }}>{children}</div>
      {source ? (
        <div
          style={{
            opacity: prog(frame, 40, 30) * 0.9,
            fontFamily: mono,
            fontSize: 22,
            color: C.muted,
          }}
        >
          {source}
        </div>
      ) : null}
    </AbsoluteFill>
  );
};

export const Bullets: React.FC<{ items: string[]; start?: number; gap?: number }> = ({
  items,
  start = 30,
  gap = 22,
}) => {
  const frame = useCurrentFrame();
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 34 }}>
      {items.map((t, i) => (
        <div
          key={t}
          style={{
            ...rise(frame, start + i * gap),
            display: "flex",
            gap: 26,
            alignItems: "flex-start",
            fontSize: 46,
            lineHeight: 1.2,
            fontWeight: 500,
          }}
        >
          <span
            style={{
              flex: "none",
              width: 16,
              height: 16,
              borderRadius: 8,
              background: C.accent,
              marginTop: 20,
            }}
          />
          <span>{t}</span>
        </div>
      ))}
    </div>
  );
};

export const Panel: React.FC<{ style?: React.CSSProperties; children: React.ReactNode }> = ({
  style,
  children,
}) => (
  <div
    style={{
      background: C.panel,
      border: `2px solid ${C.line}`,
      borderRadius: 28,
      boxShadow: "0 40px 100px rgba(0,0,0,0.45)",
      ...style,
    }}
  >
    {children}
  </div>
);
