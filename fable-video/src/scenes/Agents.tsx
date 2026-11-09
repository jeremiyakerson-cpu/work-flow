import React from "react";
import { interpolate, useCurrentFrame } from "remotion";
import { Bullets, C, display, mono, prog, Scene } from "../theme";

const COLS = 40;
const ROWS = 25; // 1,000 dots

const Swarm: React.FC = () => {
  const frame = useCurrentFrame();
  const radius = interpolate(frame, [30, 170], [0, 26], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  let lit = 0;
  const dots = [];
  for (let r = 0; r < ROWS; r++) {
    for (let c = 0; c < COLS; c++) {
      const d = Math.hypot(c - (COLS - 1) / 2, (r - (ROWS - 1) / 2) * 1.05);
      const on = d < radius;
      if (on) lit++;
      const pulse = on ? 0.55 + 0.45 * Math.sin(frame / 7 - d * 0.7) : 0;
      dots.push(
        <span
          key={`${r}-${c}`}
          style={{
            width: 10,
            height: 10,
            borderRadius: 5,
            background: on ? ((r * 7 + c * 3) % 11 === 0 ? C.teal : C.accent) : C.line,
            opacity: on ? 0.45 + pulse * 0.55 : 0.7,
          }}
        />,
      );
    }
  }
  return (
    <div style={{ width: 800, opacity: prog(frame, 20, 20) }}>
      <div style={{ display: "grid", gridTemplateColumns: `repeat(${COLS}, 10px)`, gap: 10, justifyContent: "center" }}>
        {dots}
      </div>
      <div style={{ display: "flex", alignItems: "baseline", gap: 20, marginTop: 36, justifyContent: "center" }}>
        <span style={{ fontFamily: display, fontSize: 84, fontWeight: 700, color: C.accent, fontVariantNumeric: "tabular-nums" }}>
          {lit.toLocaleString("en-US")}
          {lit >= 1000 ? "+" : ""}
        </span>
        <span style={{ fontFamily: mono, fontSize: 30, color: C.muted }}>subagents, one lead</span>
      </div>
    </div>
  );
};

export const Agents: React.FC = () => (
  <Scene
    index="02"
    kicker="AGENTS"
    title={<>Leads <span style={{ color: C.accent }}>teams of agents</span></>}
    source="Anthropic calls it “our strongest lead agent” · Nov 2026"
  >
    <div style={{ display: "flex", gap: 80, width: "100%" }}>
      <div style={{ flex: 1 }}>
        <Bullets
          items={[
            "Coordinates hundreds of subagents, even 1,000+",
            "Runs open-ended engineering work for days",
            "Flags results that don't reproduce",
          ]}
        />
      </div>
      <Swarm />
    </div>
  </Scene>
);
