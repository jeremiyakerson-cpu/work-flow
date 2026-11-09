import React from "react";
import { Composition } from "remotion";
import { FableVideo, TOTAL } from "./Video";

export const RemotionRoot: React.FC = () => (
  <Composition
    id="FableCapabilities"
    component={FableVideo}
    durationInFrames={TOTAL}
    fps={30}
    width={1920}
    height={1080}
  />
);
