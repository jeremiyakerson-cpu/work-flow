import React from "react";
import { useCurrentFrame } from "remotion";
import { Bullets, C, mono, Panel, prog, rise, Scene } from "../theme";

const LINES: { t: string; c?: string; at: number }[] = [
  { t: '$ claude "fix the flaky checkout tests"', c: C.text, at: 40 },
  { t: "› reading 4 repos · 1,284 files", at: 78 },
  { t: "› root cause: race in session cache", c: C.accent, at: 104 },
  { t: "› patch written · 3 files changed", at: 130 },
  { t: "› running the test suite…", at: 152 },
  { t: "✓ 212 passed · 0 failed", c: C.teal, at: 184 },
];

const Terminal: React.FC = () => {
  const frame = useCurrentFrame();
  return (
    <Panel style={{ ...rise(frame, 24, 26), width: 820, alignSelf: "flex-start", overflow: "hidden" }}>
      <div style={{ display: "flex", gap: 12, padding: "24px 28px", borderBottom: `2px solid ${C.line}` }}>
        {[0, 1, 2].map((i) => (
          <span key={i} style={{ width: 18, height: 18, borderRadius: 9, background: C.line }} />
        ))}
        <span style={{ marginLeft: "auto", fontFamily: mono, fontSize: 20, color: C.muted }}>
          illustration
        </span>
      </div>
      <div style={{ padding: "34px 36px 42px", fontFamily: mono, fontSize: 30, lineHeight: 1.75, minHeight: 400 }}>
        {LINES.map((l, i) => {
          // First line types out; the rest appear as output.
          const chars = i === 0 ? Math.round(prog(frame, l.at, 30) * l.t.length) : l.t.length;
          return (
            <div key={l.t} style={{ color: l.c ?? C.muted, opacity: i === 0 ? 1 : prog(frame, l.at, 10), whiteSpace: "pre" }}>
              {l.t.slice(0, chars)}
              {i === 0 && frame < 76 && Math.floor(frame / 8) % 2 === 0 ? "▍" : ""}
            </div>
          );
        })}
      </div>
    </Panel>
  );
};

export const Coding: React.FC = () => (
  <Scene
    index="01"
    kicker="CODING"
    title={<>Works across <span style={{ color: C.accent }}>whole codebases</span></>}
    source="69.9% on FrontierSWE v2 (Fable 5.1: 56.3%) · Anthropic, Nov 2026"
  >
    <div style={{ display: "flex", gap: 80, width: "100%" }}>
      <div style={{ flex: 1 }}>
        <Bullets
          items={[
            "Takes on large, loosely defined problems",
            "Tests its own work as it goes",
            "Finds root causes earlier models missed",
          ]}
        />
      </div>
      <Terminal />
    </div>
  </Scene>
);
