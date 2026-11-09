import React from "react";
import { useCurrentFrame } from "remotion";
import { C, mono, Panel, prog, rise, Scene } from "../theme";

type Card = { big: string; label: string; note?: string; count?: number; fmt?: (n: number) => string };

const CardRow: React.FC<{ cards: Card[]; start?: number }> = ({ cards, start = 30 }) => {
  const frame = useCurrentFrame();
  return (
    <div style={{ display: "flex", gap: 40, width: "100%" }}>
      {cards.map((c, i) => {
        const s = start + i * 18;
        const p = prog(frame, s + 6, 40);
        return (
          <Panel key={c.label} style={{ ...rise(frame, s, 24), flex: 1, padding: "48px 44px", alignSelf: "stretch", minHeight: 330 }}>
            <div style={{ fontSize: c.count !== undefined ? 120 : 84, fontWeight: 700, color: C.accent, letterSpacing: -2, lineHeight: 1.05, fontVariantNumeric: "tabular-nums" }}>
              {c.count !== undefined && c.fmt ? c.fmt(c.count * p) : c.big}
            </div>
            <div style={{ fontSize: 40, fontWeight: 500, lineHeight: 1.2, marginTop: 24 }}>{c.label}</div>
            {c.note ? (
              <div style={{ fontFamily: mono, fontSize: 24, color: C.muted, marginTop: 20, lineHeight: 1.4 }}>{c.note}</div>
            ) : null}
          </Panel>
        );
      })}
    </div>
  );
};

const Callout: React.FC<{ at: number; children: React.ReactNode }> = ({ at, children }) => {
  const frame = useCurrentFrame();
  return (
    <div style={{ ...rise(frame, at, 24), marginTop: 44, fontSize: 40, fontWeight: 500, lineHeight: 1.3, borderLeft: `6px solid ${C.teal}`, paddingLeft: 30 }}>
      {children}
    </div>
  );
};

const pct = (n: number) => `${n.toFixed(1)}%`;

export const Science: React.FC = () => (
  <Scene index="04" kicker="SCIENCE & MATH" title={<>Does <span style={{ color: C.accent }}>real research</span></>} source="Capabilities and results as reported by Anthropic · Nov 2026">
    <div style={{ width: "100%" }}>
      <CardRow
        cards={[
          { big: "Proofs", label: "Formalizes mathematical proofs" },
          { big: "Physics", label: "Develops new methods for physics computations" },
          { big: "Hardware", label: "Designs working electronic devices" },
        ]}
      />
      <Callout at={96}>
        Anthropic reports a team of 1,000 Fable agents formalized{" "}
        <span style={{ color: C.teal }}>Fermat's Last Theorem in 30 hours</span>.
      </Callout>
    </div>
  </Scene>
);

export const Vision: React.FC = () => (
  <Scene index="05" kicker="VISION & ACTION" title={<>Sees, clicks, <span style={{ color: C.accent }}>and builds</span></>} source="OSWorld 2.1 · Chartography · RoboDojo Sim (run internally) · Anthropic, Nov 2026">
    <div style={{ width: "100%" }}>
      <CardRow
        cards={[
          { big: "", count: 89.3, fmt: pct, label: "Operating a computer", note: "OSWorld 2.1" },
          { big: "", count: 73.4, fmt: pct, label: "Reading charts by eye", note: "Fable 5.1: 44.8%" },
          { big: "", count: 64.9, fmt: pct, label: "Simulated robotics", note: "Fable 5.1: 27.6%" },
        ]}
      />
      <Callout at={100}>Reads detailed images and PDFs. Builds 3D worlds, simulations and animations.</Callout>
    </div>
  </Scene>
);

export const Price: React.FC = () => (
  <Scene index="06" kicker="SPEED & PRICE" title={<>Faster <span style={{ color: C.accent }}>and cheaper</span> than 5.1</>} source="API pricing per million tokens · Anthropic, Nov 2026">
    <CardRow
      cards={[
        { big: "", count: 30, fmt: (n) => `${Math.round(n)}%`, label: "Faster output, at best", note: "“up to 30% faster” than 5.1" },
        { big: "$8 / $40", label: "Input / output per million tokens", note: "Down from $10 / $50" },
        { big: "", count: 2.5, fmt: (n) => `${n.toFixed(1)}×`, label: "Top speed in fast mode", note: "$16 / $80 per million tokens" },
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
    <Scene index="07" kicker="GOOD TO KNOW" title={<>The <span style={{ color: C.accent }}>fine print</span></>}>
      <div style={{ display: "flex", flexDirection: "column", gap: 30, width: "100%" }}>
        {rows.map(([k, v], i) => (
          <Panel key={k} style={{ ...rise(frame, 30 + i * 22, 24), padding: "34px 44px", display: "flex", gap: 44, alignItems: "center" }}>
            <span style={{ width: 380, flex: "none", fontSize: 44, fontWeight: 700, color: C.accent }}>{k}</span>
            <span style={{ fontSize: 40, fontWeight: 500, lineHeight: 1.25 }}>{v}</span>
          </Panel>
        ))}
      </div>
    </Scene>
  );
};
