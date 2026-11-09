import React from "react";
import { useCurrentFrame } from "remotion";
import { C, caps, Em, nums, prog, rise, Rule, Scene } from "../theme";

type Col = { big?: string; label: string; note?: string; count?: number; fmt?: (n: number) => string };

/** Three numbered columns divided by thin vertical hairlines. */
const Columns: React.FC<{ cols: Col[]; start?: number }> = ({ cols, start = 30 }) => {
  const frame = useCurrentFrame();
  return (
    <div style={{ display: "flex", width: "100%" }}>
      {cols.map((c, i) => {
        const s = start + i * 18;
        const p = prog(frame, s + 6, 44);
        return (
          <div key={c.label} style={{ ...rise(frame, s, 28), flex: 1, padding: i === 0 ? "6px 50px 6px 0" : "6px 50px", borderLeft: i === 0 ? "none" : `2px solid ${C.taupe}` }}>
            <div style={caps(22, C.taupe)}>0{i + 1}</div>
            <div style={{ fontSize: c.count !== undefined ? 170 : 124, fontWeight: 300, lineHeight: 1, letterSpacing: -3, margin: "18px 0 20px", ...nums }}>
              {c.count !== undefined && c.fmt ? c.fmt(c.count * p) : c.big}
            </div>
            <div style={{ fontSize: 46, lineHeight: 1.12, fontWeight: 400 }}>{c.label}</div>
            {c.note ? <div style={{ ...caps(21, C.olive), marginTop: 22, lineHeight: 1.5 }}>{c.note}</div> : null}
          </div>
        );
      })}
    </div>
  );
};

const Note: React.FC<{ at: number; children: React.ReactNode }> = ({ at, children }) => {
  const frame = useCurrentFrame();
  return (
    <div style={{ marginTop: 44 }}>
      <Rule at={at} color={C.stone} />
      <div style={{ ...rise(frame, at + 6, 28), fontSize: 46, fontWeight: 400, lineHeight: 1.2, marginTop: 30 }}>{children}</div>
    </div>
  );
};

const pct = (n: number) => `${n.toFixed(1)}%`;

export const Science: React.FC = () => (
  <Scene index="04" kicker="Science & math" title={<>Does <Em>real research</Em></>} source="Capabilities and results as reported by Anthropic · Nov 2026">
    <div style={{ width: "100%" }}>
      <Columns
        cols={[
          { big: "Proofs", label: "Formalizes mathematical proofs" },
          { big: "Physics", label: "Develops new methods for physics computations" },
          { big: "Hardware", label: "Designs working electronic devices" },
        ]}
      />
      <Note at={96}>
        Anthropic reports that 1,000 Fable agents formalized <i>Fermat's Last Theorem in 30 hours.</i>
      </Note>
    </div>
  </Scene>
);

export const Vision: React.FC = () => (
  <Scene index="05" kicker="Vision & action" title={<>Sees, clicks <Em>and builds</Em></>} source="OSWorld 2.1 · Chartography · RoboDojo Sim, run internally · Anthropic, Nov 2026">
    <div style={{ width: "100%" }}>
      <Columns
        cols={[
          { count: 89.3, fmt: pct, label: "Operating a computer", note: "OSWorld 2.1" },
          { count: 73.4, fmt: pct, label: "Reading charts by eye", note: "Fable 5.1: 44.8%" },
          { count: 64.9, fmt: pct, label: "Simulated robotics", note: "Fable 5.1: 27.6%" },
        ]}
      />
      <Note at={100}>Reads detailed images and PDFs. Builds 3D worlds, simulations and animations.</Note>
    </div>
  </Scene>
);

export const Price: React.FC = () => (
  <Scene index="06" kicker="Speed & price" title={<>Faster <Em>and cheaper</Em> than 5.1</>} source="API pricing per million tokens · Anthropic, Nov 2026">
    <Columns
      cols={[
        { count: 30, fmt: (n) => `${Math.round(n)}%`, label: "Faster output, at best", note: "“Up to 30% faster” than 5.1" },
        { big: "$8 / $40", label: "Input and output, per million tokens", note: "Down from $10 / $50" },
        { count: 2.5, fmt: (n) => `${n.toFixed(1)}×`, label: "Top speed in fast mode", note: "$16 / $80 per million tokens" },
      ]}
    />
  </Scene>
);

export const GoodToKnow: React.FC = () => {
  const frame = useCurrentFrame();
  const rows = [
    ["Safeguards", "Cybersecurity and biology requests are restricted. Most cyber tasks are blocked."],
    ["Everyday work", "Anthropic suggests starting with Opus 5.5 for routine coding and writing."],
    ["Where to get it", "Pro, Max, Ultra, Team and Enterprise plans, plus the API and major clouds."],
  ];
  return (
    <Scene index="07" kicker="Good to know" title={<>The <Em>fine print</Em></>}>
      <div style={{ display: "flex", flexDirection: "column", width: "100%" }}>
        {rows.map(([k, v], i) => (
          <div key={k} style={{ ...rise(frame, 30 + i * 22, 28), display: "flex", gap: 60, alignItems: "baseline", padding: "38px 0", borderTop: i === 0 ? "none" : `2px solid ${C.stone}` }}>
            <span style={{ ...caps(24, C.olive), width: 400, flex: "none" }}>{k}</span>
            <span style={{ fontSize: 54, lineHeight: 1.14, fontWeight: 400 }}>{v}</span>
          </div>
        ))}
      </div>
    </Scene>
  );
};
