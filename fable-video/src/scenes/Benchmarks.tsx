import React from "react";
import { useCurrentFrame } from "remotion";
import { C, caps, Em, nums, prog, rise, Scene } from "../theme";

const DATA = [
  { name: "Agentic coding", bench: "FrontierSWE v2", v: [69.9, 62.3, 56.3] },
  { name: "Computer use", bench: "OSWorld 2.1", v: [89.3, 87.2, 83.5] },
  { name: "Expert reasoning", bench: "Humanity's Last Exam", v: [74.2, 67.7, 65.6] },
  { name: "Reading charts", bench: "Chartography", v: [73.4, 64.4, 44.8] },
];
// Rust is reserved for the highlighted series.
const SERIES = [
  { label: "Fable 5.2", color: C.rust },
  { label: "Opus 5.5", color: C.olive },
  { label: "Fable 5.1", color: C.sage },
];
const MAX_W = 560;

export const Benchmarks: React.FC = () => {
  const frame = useCurrentFrame();
  return (
    <Scene index="03" kicker="Benchmarks" title={<>Ahead of <Em>its predecessors</Em></>} source="Scores in %, each model at its highest effort · Anthropic launch benchmarks, Nov 2026">
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", columnGap: 110, rowGap: 34, width: "100%" }}>
        {DATA.map((d, i) => {
          const start = 28 + i * 16;
          return (
            <div key={d.bench} style={rise(frame, start)}>
              <div style={{ display: "flex", alignItems: "baseline", gap: 22, marginBottom: 6 }}>
                <span style={{ fontSize: 52, fontWeight: 400 }}>{d.name}</span>
                <span style={caps(20, C.olive)}>{d.bench}</span>
              </div>
              {d.v.map((v, j) => {
                const p = prog(frame, start + 10 + j * 6, 44);
                return (
                  <div key={j} style={{ display: "flex", alignItems: "center", gap: 20, height: 46 }}>
                    <span style={{ ...caps(20, C.espresso), fontWeight: j === 0 ? 500 : 400, width: 150 }}>{SERIES[j].label}</span>
                    <span style={{ width: (v / 100) * MAX_W * p, height: j === 0 ? 26 : 16, background: SERIES[j].color }} />
                    <span style={{ fontSize: j === 0 ? 50 : 36, lineHeight: 1, color: j === 0 ? C.rust : C.espresso, opacity: p, ...nums }}>
                      {(v * p).toFixed(1)}
                    </span>
                  </div>
                );
              })}
            </div>
          );
        })}
      </div>
    </Scene>
  );
};
