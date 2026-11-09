import React from "react";
import { AbsoluteFill, useCurrentFrame } from "remotion";
import { C, display, mono, prog, rise } from "../theme";

const Rings: React.FC = () => {
  const frame = useCurrentFrame();
  return (
    <AbsoluteFill style={{ alignItems: "center", justifyContent: "center" }}>
      {[520, 760, 1000, 1240].map((d, i) => {
        const p = prog(frame, i * 6, 50);
        const a = (frame * (0.5 - i * 0.08) + i * 70) * (Math.PI / 180);
        return (
          <div
            key={d}
            style={{
              position: "absolute",
              width: d * p,
              height: d * p,
              borderRadius: "50%",
              border: `2px solid ${C.line}`,
              opacity: p * 0.8,
            }}
          >
            <div
              style={{
                position: "absolute",
                left: "50%",
                top: "50%",
                width: 18,
                height: 18,
                marginLeft: -9,
                marginTop: -9,
                borderRadius: 9,
                background: i % 2 ? C.teal : C.accent,
                translate: `${(Math.cos(a) * d * p) / 2}px ${(Math.sin(a) * d * p) / 2}px`,
              }}
            />
          </div>
        );
      })}
    </AbsoluteFill>
  );
};

export const Intro: React.FC<{ outro?: boolean }> = ({ outro }) => {
  const frame = useCurrentFrame();
  const words = ["Claude", "Fable", "5.2"];
  return (
    <AbsoluteFill style={{ fontFamily: display, color: C.text }}>
      <Rings />
      <AbsoluteFill style={{ alignItems: "center", justifyContent: "center", textAlign: "center" }}>
        <div style={{ display: "flex", gap: 44, fontSize: 190, fontWeight: 700, letterSpacing: -6 }}>
          {words.map((w, i) => (
            <span
              key={w}
              style={{ ...rise(frame, 10 + i * 9, 28, 90), color: i === 1 ? C.accent : C.text }}
            >
              {w}
            </span>
          ))}
        </div>
        <div style={{ ...rise(frame, 46, 26), fontSize: 54, fontWeight: 500, marginTop: 20 }}>
          {outro ? "Built for the hardest technical work." : "What it can actually do"}
        </div>
        <div
          style={{
            ...rise(frame, 62, 26, 20),
            fontFamily: mono,
            fontSize: 28,
            letterSpacing: outro ? 1 : 6,
            color: C.muted,
            marginTop: 44,
          }}
        >
          {outro
            ? "Figures from Anthropic's launch materials · November 9, 2026"
            : "CODING · AGENTS · SCIENCE · VISION"}
        </div>
      </AbsoluteFill>
    </AbsoluteFill>
  );
};
