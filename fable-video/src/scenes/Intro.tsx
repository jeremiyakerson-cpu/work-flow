import React from "react";
import { AbsoluteFill, useCurrentFrame } from "remotion";
import { C, caps, Em, prog, rise, serif } from "../theme";

/** Single-ink line illustration: slow concentric orbits. */
const Orbits: React.FC<{ ink: string }> = ({ ink }) => {
  const frame = useCurrentFrame();
  return (
    <AbsoluteFill style={{ alignItems: "center", justifyContent: "center" }}>
      {[260, 420, 580, 740, 900].map((d, i) => {
        const p = prog(frame, 14 + i * 7, 60);
        const a = (frame * (0.42 - i * 0.06) + i * 74) * (Math.PI / 180);
        return (
          <div key={d} style={{ position: "absolute", width: d * p, height: d * p, borderRadius: "50%", border: `2px solid ${ink}`, opacity: p * 0.75 }}>
            <div
              style={{
                position: "absolute",
                left: "50%",
                top: "50%",
                width: 14,
                height: 14,
                margin: -7,
                borderRadius: 7,
                background: ink,
                translate: `${(Math.cos(a) * d * p) / 2}px ${(Math.sin(a) * d * p) / 2}px`,
              }}
            />
          </div>
        );
      })}
    </AbsoluteFill>
  );
};

export const Intro: React.FC = () => {
  const frame = useCurrentFrame();
  return (
    <AbsoluteFill style={{ backgroundColor: C.linen, fontFamily: serif, color: C.espresso, flexDirection: "row" }}>
      <div style={{ flex: 1, padding: "100px 0 84px 130px", display: "flex", flexDirection: "column", justifyContent: "space-between" }}>
        <div style={{ ...rise(frame, 6, 22, 16), ...caps(24) }}>A short guide &nbsp;/&nbsp; November 2026</div>
        <div style={{ fontSize: 148, fontWeight: 300, lineHeight: 1.0, letterSpacing: -3, whiteSpace: "nowrap", fontVariantNumeric: "lining-nums" }}>
          <div style={rise(frame, 14, 34, 60)}>Claude Fable 5.2,</div>
          <div style={rise(frame, 30, 34, 60)}>
            <Em>what it can do.</Em>
          </div>
        </div>
        <div style={{ ...rise(frame, 56, 26, 16), ...caps(22, C.espresso), fontWeight: 400 }}>
          Coding &nbsp;·&nbsp; Agents &nbsp;·&nbsp; Science &nbsp;·&nbsp; Vision
        </div>
      </div>
      <div style={{ width: 700 * prog(frame, 0, 44), background: C.deepOlive, position: "relative", overflow: "hidden", flex: "none" }}>
        <div style={{ position: "absolute", right: 0, top: 0, width: 700, height: "100%" }}>
          <Orbits ink={C.sage} />
        </div>
      </div>
    </AbsoluteFill>
  );
};

export const Outro: React.FC = () => {
  const frame = useCurrentFrame();
  return (
    <AbsoluteFill style={{ backgroundColor: C.deepOlive, fontFamily: serif, color: C.linen, padding: "100px 130px 84px", justifyContent: "space-between" }}>
      <div style={{ ...rise(frame, 6, 22, 16), ...caps(24, C.sage), letterSpacing: "0.4em" }}>Claude Fable 5.2</div>
      <div style={{ fontSize: 168, fontWeight: 300, lineHeight: 1.0, letterSpacing: -3, fontVariantNumeric: "lining-nums" }}>
        <div style={rise(frame, 12, 34, 60)}>Built for the hardest</div>
        <div style={rise(frame, 26, 34, 60)}>
          <Em color={C.linen}>technical work.</Em>
        </div>
      </div>
      <div style={{ ...rise(frame, 50, 26, 16), ...caps(20, C.sage), fontWeight: 400, letterSpacing: "0.14em" }}>
        Figures from Anthropic's launch materials &nbsp;·&nbsp; November 9, 2026
      </div>
    </AbsoluteFill>
  );
};
