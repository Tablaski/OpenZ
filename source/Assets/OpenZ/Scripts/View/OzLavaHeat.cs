using System;

namespace OpenZ
{
    /// <summary>
    /// Plain C#, no Unity, so the counted proof (Editor/OzLavaHeatProof.cs) runs this very class with no scene and no Play.
    ///   heat(field) = exp(MoodGain * mood) * (1 + DriftAmp * tanh(drift of that field)) + SurgeLift * surge strength
    ///   mood   the wandering mood of OzLavaPops (hot minutes also bubble more); the border lakes read their own pop stream's mood
    ///   drift  each field's own slow random walk, so the lakes do not move as one
    ///   surge  every 20-45 s one lake brightens from one end to the other over 6-10 s, eased in and out; never two at once
    ///   wave   a slow travelling wave 20-40 m long; its amplitude, length, heading and speed wander, so it is never a beat
    ///   breath the heat you can see at the game camera: one eased swell and fall of 8-20 s after another, each with its own length and strength, some weak, some strong
    /// Mood and drift pass through a critically damped ease, then the range limit, then a limit on the change per second of each field's
    /// final value, in that order. 1.0 is the picture's moment. CALM SCREEN halves the swing round 1. Holding (dev, gate 1) puts everything
    /// at exactly the picture. A step of 0 changes nothing, so it stops with the game's pause. Its own System.Random: the sim's is never touched.
    /// </summary>
    public sealed class OzLavaHeat
    {
        public const double MoodGain = 0.155, DriftAmp = 0.10, DriftSeconds = 15.0, EaseOmega = 0.8, HeatMin = 0.72, HeatMax = 1.35, RatePerSecond = 0.15;
        public const double CoverSwing = 0.08, CoverSpan = 0.30, SurgeLift = 0.13, SurgeGapMin = 20.0, SurgeGapMax = 45.0, SurgeLenMin = 6.0, SurgeLenMax = 10.0;
        public const double WaveAmpMin = 0.03, WaveAmpMax = 0.08, WaveLenMin = 20.0, WaveLenMax = 40.0, WaveSpeedMin = 0.9, WaveSpeedMax = 2.3, WaveTurn = 0.9;
        const double NoiseStep = 0.1, TwoPi = 2.0 * Math.PI, Shoulder = 0.10;
        /// <summary>One breath lasts this long (s); a weak one swings the heat by BreathWeak, a strong one (BreathStrongShare of them) by BreathStrong.</summary>
        public const double BreathMin = 8.0, BreathMax = 20.0, BreathWeakLo = 0.05, BreathWeakHi = 0.14, BreathStrongLo = 0.20, BreathStrongHi = 0.30, BreathStrongShare = 0.28;

        /// <summary>Per field: the final heat (HeatMin..HeatMax, written to _OZLavaField x), crust cover offset (y), surge strength 0..1 (z).</summary>
        public readonly double[] Heat, Cover, Surge;
        /// <summary>Map-wide eased heat: no field drift, no surge (_OZLavaMood, HeatNow).</summary>
        public double Map = 1.0;
        /// <summary>The wave as the shader wants it: 1 + WaveAmp * sin(WaveKx x + WaveKz z + WavePhase), phase anchored at the lakes' centre.</summary>
        public double WaveAmp, WaveKx = 0.2, WaveKz, WavePhase;
        /// <summary>The running surge: unit direction, front position along it in m, half width in m; SurgeField -1 = none.</summary>
        public double SurgeDirX, SurgeDirZ, SurgeFront, SurgeHalf;
        public int SurgeField = -1, SurgesStarted;
        /// <summary>Seconds this has run (stops with the pause), the start of the latest surge.</summary>
        public double Time, LastSurgeAt = -1.0;
        /// <summary>Slots from this id on read the border mood.</summary>
        public int BorderFrom = int.MaxValue;

        readonly Random rng, breathRng;
        readonly int slots;
        readonly double centreX, centreZ;
        readonly double[] drift, driftEased, driftVel, axisX, axisZ, lo, hi, weight;
        double moodE, moodV, moodBE, moodBV, noiseDue, nextSurge, surgeAge, surgeLen, surgeLo, surgeHi, surgeK, phase, breathAt, breathLen = 12.0, breathAmp = 0.1;
        double waveA, waveAE, waveAV, waveL, waveLE, waveLV, waveD, waveDE, waveDV, waveS, waveSE, waveSV;
        int lastSurge = -1;
        bool settled;

        public OzLavaHeat(int slots, int seed, double centreX = 0.0, double centreZ = 0.0)
        {
            this.slots = slots; this.centreX = centreX; this.centreZ = centreZ; rng = new Random(seed); breathRng = new Random(seed ^ 0x3C5A11);
            Heat = new double[slots]; Cover = new double[slots]; Surge = new double[slots];
            drift = new double[slots]; driftEased = new double[slots]; driftVel = new double[slots];
            axisX = new double[slots]; axisZ = new double[slots]; lo = new double[slots]; hi = new double[slots]; weight = new double[slots];
            for (int i = 0; i < slots; i++) { Heat[i] = 1.0; drift[i] = driftEased[i] = Gauss(); }
            waveA = waveAE = Gauss(); waveL = waveLE = Gauss(); waveD = waveDE = Gauss(); waveS = waveSE = Gauss();
            phase = rng.NextDouble() * TwoPi;
            nextSurge = 8.0 + 16.0 * rng.NextDouble();
            WriteWave();
        }

        /// <summary>A field a surge may run along: its long axis (unit), its ends in m along that axis, its chance (0 = never).</summary>
        public void SetField(int id, double dirX, double dirZ, double from, double to, double chance)
        {
            if (id < 0 || id >= slots) return;
            axisX[id] = dirX; axisZ[id] = dirZ; lo[id] = from; hi[id] = to; weight[id] = chance;
        }

        double Gauss() { double u = 1.0 - rng.NextDouble(), v = rng.NextDouble(); return Math.Sqrt(-2.0 * Math.Log(u)) * Math.Cos(TwoPi * v); }

        /// <summary>Critically damped ease: smooth at any frame length (the SmoothDamp sum), no overshoot.</summary>
        static void Ease(ref double x, ref double v, double target, double omega, double h)
        {
            double f = omega * h, e = 1.0 / (1.0 + f + 0.48 * f * f + 0.235 * f * f * f), d = x - target, tmp = (v + omega * d) * h;
            v = (v - omega * tmp) * e; x = target + (d + tmp) * e;
        }

        static double Clamp(double x, double a, double b) => x < a ? a : x > b ? b : x;
        static double Soft(double x) => Math.Tanh(x);
        /// <summary>The range limit with rounded shoulders: unchanged in the middle, never past HeatMin / HeatMax, no kink where it starts to act.</summary>
        static double Range(double v)
        {
            if (v > HeatMax - Shoulder) return HeatMax - Shoulder + Shoulder * Math.Tanh((v - HeatMax + Shoulder) / Shoulder);
            if (v < HeatMin + Shoulder) return HeatMin + Shoulder - Shoulder * Math.Tanh((HeatMin + Shoulder - v) / Shoulder);
            return v;
        }

        /// <summary>Advance dt seconds. mood / moodBorder: the two pop streams' moods now. hold: the picture's moment. calm: CALM SCREEN.</summary>
        public void Step(double dt, double mood, double moodBorder, bool hold, bool calm)
        {
            if (hold) { Hold(); return; }                                                                       // first: gate 1's still may be taken on a paused game
            if (dt <= 0.0) return;
            while (dt > 1e-9)
            {
                double h = Math.Min(dt, 0.1); dt -= h;
                Tick(h, mood, moodBorder, calm);
            }
        }

        void Hold()
        {
            Map = 1.0; WaveAmp = 0.0; SurgeField = -1; surgeK = 0.0; SurgeFront = SurgeHalf = 0.0;
            for (int i = 0; i < slots; i++) { Heat[i] = 1.0; Cover[i] = 0.0; Surge[i] = 0.0; }
        }

        void Tick(double h, double mood, double moodBorder, bool calm)
        {
            Time += h;
            noiseDue += h;
            while (noiseDue >= NoiseStep)
            {   // the targets of the eases: a fixed 10 Hz whatever the frame rate, so the walks are the same at 30 and at 144 frames
                noiseDue -= NoiseStep;
                for (int i = 0; i < slots; i++) Walk(ref drift[i], DriftSeconds);
                Walk(ref waveA, 37.0); Walk(ref waveL, 61.0); Walk(ref waveD, 83.0); Walk(ref waveS, 29.0);
            }
            Ease(ref moodE, ref moodV, mood, EaseOmega, h);
            Ease(ref moodBE, ref moodBV, moodBorder, EaseOmega, h);
            Surges(h);
            Breath(h);
            double limit = RatePerSecond * h;
            for (int i = 0; i < slots; i++)
            {
                Ease(ref driftEased[i], ref driftVel[i], drift[i], EaseOmega, h);
                double m = i >= BorderFrom ? moodBE : moodE;
                double v = Math.Exp(MoodGain * m) * (1.0 + DriftAmp * Soft(driftEased[i])) * (1.0 + BreathNow(i * 0.06));
                double s = i == SurgeField ? surgeK : 0.0;
                v = Range(v + SurgeLift * s);
                if (calm) { v = 1.0 + (v - 1.0) * 0.5; s *= 0.5; }
                Heat[i] = settled ? Heat[i] + Clamp(v - Heat[i], -limit, limit) : v;   // the very first value is where the level starts: nothing to ease from
                Cover[i] = -Clamp((Heat[i] - 1.0) / CoverSpan, -1.0, 1.0) * CoverSwing;
                Surge[i] = s;
            }
            double all = Range(Math.Exp(MoodGain * moodE) * (1.0 + BreathNow(0.0)));
            if (calm) all = 1.0 + (all - 1.0) * 0.5;
            Map = settled ? Map + Clamp(all - Map, -limit, limit) : all;
            settled = true;
            Wave(h, calm);
        }

        /// <summary>One swell and fall after another: a new length and strength each time the last one has come back to the middle.</summary>
        void Breath(double h)
        {
            breathAt += h / breathLen;
            if (breathAt < 1.0) return;
            breathAt -= 1.0;
            breathLen = BreathMin + (BreathMax - BreathMin) * breathRng.NextDouble();
            breathAmp = breathRng.NextDouble() < BreathStrongShare ? BreathStrongLo + (BreathStrongHi - BreathStrongLo) * breathRng.NextDouble() : BreathWeakLo + (BreathWeakHi - BreathWeakLo) * breathRng.NextDouble();
        }

        /// <summary>The breath now; a small lag per field, so the lakes do not move in step. Eased: flat at the top, no corner at the middle.</summary>
        double BreathNow(double lag) { double s = Math.Sin(TwoPi * (breathAt + lag)); return breathAmp * s * Math.Abs(s); }

        void Walk(ref double x, double seconds) => x += -x * NoiseStep / seconds + Math.Sqrt(2.0 * NoiseStep / seconds) * Gauss();

        void Wave(double h, bool calm)
        {
            Ease(ref waveAE, ref waveAV, waveA, EaseOmega, h); Ease(ref waveLE, ref waveLV, waveL, EaseOmega, h);
            Ease(ref waveDE, ref waveDV, waveD, EaseOmega, h); Ease(ref waveSE, ref waveSV, waveS, EaseOmega, h);
            double a = WaveAmpMin + (WaveAmpMax - WaveAmpMin) * (0.5 + 0.5 * Soft(waveAE));
            double len = 0.5 * (WaveLenMin + WaveLenMax) + 0.5 * (WaveLenMax - WaveLenMin) * Soft(waveLE);
            double speed = 0.5 * (WaveSpeedMin + WaveSpeedMax) + 0.5 * (WaveSpeedMax - WaveSpeedMin) * Soft(waveSE);
            double k = TwoPi / len, th = WaveTurn * Soft(waveDE) + 0.6;
            WaveAmp = calm ? a * 0.5 : a;
            WaveKx = k * Math.Cos(th); WaveKz = k * Math.Sin(th);
            phase += k * speed * h; if (phase > TwoPi) phase -= TwoPi;
            WriteWave();
        }

        void WriteWave()
        {   // anchored at the lakes' centre: a slow change of length or heading then turns the wave about the lakes, not about the map's corner
            double p = (phase - WaveKx * centreX - WaveKz * centreZ) % TwoPi;
            WavePhase = p < 0.0 ? p + TwoPi : p;
        }

        void Surges(double h)
        {
            if (SurgeField < 0 && Time >= nextSurge) Begin();
            if (SurgeField < 0) { surgeK = 0.0; return; }
            surgeAge += h;
            double u = surgeAge / surgeLen;
            if (u >= 1.0) { SurgeField = -1; surgeK = 0.0; SurgeFront = SurgeHalf = 0.0; return; }
            double sn = Math.Sin(Math.PI * u);
            surgeK = sn * sn;
            SurgeFront = surgeLo + (surgeHi - surgeLo) * (u * u * u * (u * (u * 6.0 - 15.0) + 10.0));
        }

        void Begin()
        {
            nextSurge = Time + SurgeGapMin + (SurgeGapMax - SurgeGapMin) * rng.NextDouble();
            double sum = 0.0;
            for (int i = 0; i < slots; i++) if (i != lastSurge) sum += weight[i];
            if (sum <= 0.0) return;
            double r = rng.NextDouble() * sum; int pick = -1;
            for (int i = 0; i < slots; i++)
            {
                if (i == lastSurge || weight[i] <= 0.0) continue;
                pick = i; r -= weight[i]; if (r <= 0.0) break;
            }
            if (pick < 0) return;
            lastSurge = SurgeField = pick; SurgesStarted++; LastSurgeAt = Time;
            surgeAge = 0.0; surgeLen = SurgeLenMin + (SurgeLenMax - SurgeLenMin) * rng.NextDouble();
            SurgeHalf = 6.0 + 4.0 * rng.NextDouble();
            bool back = rng.NextDouble() < 0.5;   // either end first
            SurgeDirX = back ? -axisX[pick] : axisX[pick]; SurgeDirZ = back ? -axisZ[pick] : axisZ[pick];
            surgeLo = (back ? -hi[pick] : lo[pick]) - SurgeHalf; surgeHi = (back ? -lo[pick] : hi[pick]) + SurgeHalf;
            SurgeFront = surgeLo;
        }
    }
}
