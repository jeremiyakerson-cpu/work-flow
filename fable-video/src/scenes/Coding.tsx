import React from "react";
import { useCurrentFrame } from "remotion";
import { C, caps, Em, Points, prog, rise, sans, Scene } from "../theme";

const STEPS: { t: string; at: number }[] = [
  { t: "Reads 4 repositories, 1,284 files", at: 78 },
  { t: "Finds the root cause: a race in the session cache", at: 104 },
  { t: "Writes the patch, 3 files changed", at: 130 },
  { t: "Runs the full test suite", at: 152 },
];
const REQUEST = "Fix the flaky checkout tests";

/** An illustrative work log, set as an editorial panel rather than a terminal. */
const WorkLog: React.FC = () => {
  const frame = useCurrentFrame();
  const typed = Math.round(prog(frame, 40, 30) * REQUEST.length);
  return (
    <div style={{ ...rise(frame, 24, 30), width: 780, flex: "none", background: C.stone, padding: "36px 50px 38px" }}>
      <div style={{ display: "flex", justifyContent: "space-between", ...caps(19, C.espresso), fontWeight: 400 }}>
        <span>Work log</span>
        <span style={{ opacity: 0.6 }}>Illustration</span>
      </div>
      <div style={{ fontSize: 52, fontStyle: "italic", lineHeight: 1.1, margin: "18px 0 18px", minHeight: 58 }}>
        “{REQUEST.slice(0, typed)}
        {typed === REQUEST.length ? "”" : ""}
      </div>
      {STEPS.map((s, i) => (
        <div
          key={s.t}
          style={{
            opacity: prog(frame, s.at, 14),
            display: "flex",
            gap: 26,
            alignItems: "baseline",
            padding: "13px 0",
            borderTop: `2px solid ${C.taupe}`,
            fontFamily: sans,
            fontWeight: 300,
            fontSize: 30,
          }}
        >
          <span style={{ ...caps(18, C.olive), flex: "none" }}>0{i + 1}</span>
          <span>{s.t}</span>
        </div>
      ))}
      <div style={{ opacity: prog(frame, 184, 16), borderTop: `2px solid ${C.espresso}`, paddingTop: 20, marginTop: 2, ...caps(22, C.espresso) }}>
        212 passed &nbsp;·&nbsp; 0 failed
      </div>
    </div>
  );
};

export const Coding: React.FC = () => (
  <Scene
    index="01"
    kicker="Coding"
    title={<>Works across <Em>whole codebases</Em></>}
    source="69.9% on FrontierSWE v2, up from 56.3% for Fable 5.1 · Anthropic, Nov 2026"
  >
    <div style={{ display: "flex", gap: 90, width: "100%", alignItems: "center" }}>
      <Points items={["Takes on large, loosely defined problems", "Tests its own work as it goes", "Finds root causes earlier models missed"]} />
      <WorkLog />
    </div>
  </Scene>
);
