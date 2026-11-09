import React from "react";
import { interpolate, useCurrentFrame } from "remotion";
import { C, caps, Em, nums, Points, prog, Scene } from "../theme";

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
      const pulse = on ? 0.5 + 0.5 * Math.sin(frame / 8 - d * 0.6) : 0;
      dots.push(
        <span key={`${r}-${c}`} style={{ width: 8, height: 8, borderRadius: 4, background: on ? C.olive : C.stone, opacity: on ? 0.55 + pulse * 0.45 : 1 }} />,
      );
    }
  }
  return (
    <div style={{ width: 680, flex: "none", opacity: prog(frame, 20, 24) }}>
      <div style={{ display: "grid", gridTemplateColumns: `repeat(${COLS}, 8px)`, gap: 8 }}>{dots}</div>
      <div style={{ display: "flex", alignItems: "baseline", gap: 28, marginTop: 20 }}>
        <span style={{ fontSize: 100, fontWeight: 300, lineHeight: 1, ...nums }}>
          {lit.toLocaleString("en-US")}
          {lit >= 1000 ? "+" : ""}
        </span>
        <span style={caps(21, C.olive)}>Subagents, one lead</span>
      </div>
    </div>
  );
};

export const Agents: React.FC = () => (
  <Scene index="02" kicker="Agents" title={<>Leads <Em>teams of agents</Em></>} source="Anthropic calls it “our strongest lead agent” · Nov 2026">
    <div style={{ display: "flex", gap: 90, width: "100%", alignItems: "center" }}>
      <Points items={["Coordinates hundreds of subagents, even 1,000+", "Runs open-ended engineering work for days", "Flags results that don't reproduce"]} />
      <Swarm />
    </div>
  </Scene>
);
