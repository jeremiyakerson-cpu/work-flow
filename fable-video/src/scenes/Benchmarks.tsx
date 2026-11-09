import React from "react";
import { useCurrentFrame } from "remotion";
import { C, mono, prog, rise, Scene } from "../theme";

const DATA = [
  { name: "Agentic coding", bench: "FrontierSWE v2", v: [69.9, 62.3, 56.3] },
  { name: "Computer use", bench: "OSWorld 2.1", v: [89.3, 87.2, 83.5] },
  { name: "Expert reasoning", bench: "Humanity's Last Exam", v: [74.2, 67.7, 65.6] },
  { name: "Reading charts", bench: "Chartography", v: [73.4, 64.4, 44.8] },
];
const SERIES = [
  { label: "Fable 5.2", color: C.accent, text: C.text },
  { label: "Opus 5.5", color: C.barMid, text: C.muted },
  { label: "Fable 5.1", color: C.barLow, text: C.muted },
];
const MAX_W = 620;

export const Benchmarks: React.FC = () => {
  const frame = useCurrentFrame();
  return (
    <Scene
      index="03"
      kicker="BENCHMARKS"
      title={<>Ahead of <span style={{ color: C.accent }}>its predecessors</span></>}
      source="Scores in %, each model at its highest effort · Anthropic launch benchmarks, Nov 2026"
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", columnGap: 90, rowGap: 46, width: "100%", alignContent: "start" }}>
        {DATA.map((d, i) => {
          const start = 28 + i * 16;
          return (
            <div key={d.bench} style={rise(frame, start)}>
              <div style={{ display: "flex", alignItems: "baseline", gap: 18, marginBottom: 14 }}>
                <span style={{ fontSize: 44, fontWeight: 700 }}>{d.name}</span>
                <span style={{ fontFamily: mono, fontSize: 24, color: C.muted }}>{d.bench}</span>
              </div>
              {d.v.map((v, j) => {
                const p = prog(frame, start + 10 + j * 6, 40);
                return (
                  <div key={j} style={{ display: "flex", alignItems: "center", gap: 18, height: 52 }}>
                    <span style={{ width: 150, fontFamily: mono, fontSize: 24, color: SERIES[j].text }}>
                      {SERIES[j].label}
                    </span>
                    <span
                      style={{
                        width: (v / 100) * MAX_W * p,
                        height: j === 0 ? 34 : 24,
                        borderRadius: 6,
                        background: SERIES[j].color,
                      }}
                    />
                    <span
                      style={{
                        fontSize: j === 0 ? 40 : 28,
                        fontWeight: j === 0 ? 700 : 500,
                        color: j === 0 ? C.accent : C.muted,
                        fontVariantNumeric: "tabular-nums",
                        opacity: p,
                      }}
                    >
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
