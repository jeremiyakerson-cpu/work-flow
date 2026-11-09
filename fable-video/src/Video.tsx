import React from "react";
import { AbsoluteFill } from "remotion";
import { linearTiming, TransitionSeries } from "@remotion/transitions";
import { fade } from "@remotion/transitions/fade";
import { Background } from "./theme";
import { Intro } from "./scenes/Intro";
import { Coding } from "./scenes/Coding";
import { Agents } from "./scenes/Agents";
import { Benchmarks } from "./scenes/Benchmarks";
import { GoodToKnow, Price, Science, Vision } from "./scenes/Cards";

export const T = 15; // transition overlap in frames
export const SCENES: { name: string; frames: number; el: React.ReactNode }[] = [
  { name: "Intro", frames: 150, el: <Intro /> },
  { name: "Coding", frames: 240, el: <Coding /> },
  { name: "Agents", frames: 240, el: <Agents /> },
  { name: "Benchmarks", frames: 270, el: <Benchmarks /> },
  { name: "Science", frames: 210, el: <Science /> },
  { name: "Vision", frames: 210, el: <Vision /> },
  { name: "Price", frames: 180, el: <Price /> },
  { name: "GoodToKnow", frames: 210, el: <GoodToKnow /> },
  { name: "Outro", frames: 120, el: <Intro outro /> },
];
export const TOTAL = SCENES.reduce((a, s) => a + s.frames, 0) - T * (SCENES.length - 1);

export const FableVideo: React.FC = () => (
  <AbsoluteFill>
    <Background />
    <TransitionSeries>
      {SCENES.flatMap((s, i) => [
        i > 0 ? (
          <TransitionSeries.Transition key={`t${i}`} presentation={fade()} timing={linearTiming({ durationInFrames: T })} />
        ) : null,
        <TransitionSeries.Sequence key={s.name} name={s.name} durationInFrames={s.frames}>
          {s.el}
        </TransitionSeries.Sequence>,
      ])}
    </TransitionSeries>
  </AbsoluteFill>
);
