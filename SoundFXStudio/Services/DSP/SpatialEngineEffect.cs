using System;
using SoundFXStudio.Models;

namespace SoundFXStudio.Services.DSP;

/// <summary>
/// Multichannel spatial-engine effect ported from ArtIsWar's open-source
/// atk_spatial_engine.jsfx (REAPER JSFX).
///
/// Pipeline per sample (faithful to the JSFX @sample section):
///   Step 0  Channel-mode detection (stereo / 3-5ch / 6ch / 7ch/8ch)
///   LFE     Scene-intensity sidechain from channel 8 (LFE)
///   Step 1  Dry buffer capture
///   Step 2  FC self-suppression (subtract filtered center content)
///   Step 3  Self Sound Gate / center duck (M/S selective)
///   Step 4  Adaptive EQ (low/high peaking boost, loudness + gunshot gated)
///   Step 5  LR4 crossover split (lowCut / xover1 / xover2 / highCut)
///   Step 6  Self-suppress (transient-gated surround vs diffuse)
///   Step 7  CMR surround decorrelation
///   Step 8  Transient shaping (Saike) + downward expansion + dir. emphasis
///   Step 9  Sum bands, output gain, dry/wet mix
///
/// Processes interleaved buffers of <see cref="ChannelCount"/> channels
/// (1-8, mirroring the JSFX pin layout).
///
/// Core algorithm: Saike's Transience by Joep Vanlier.
/// Implementation: ArtIsWar LLC. This is an independent port for offline use.
/// </summary>
public sealed class SpatialEngineEffect : IAudioEffect
{
    private const double BwQ = 0.7071067811865476;

    private static readonly double DbgK = 20.0 / Math.Log(10.0);

    // ── Heap state (mirrors JSFX @init layout) ──────────────────────────
    private readonly double[] _biquadCoeff = new double[40];    // 8 sets x 5
    private readonly double[] _xoverState = new double[256];    // 4 xovers x 8ch x 8
    private readonly double[] _subXoverState = new double[16];  // FC subtraction (8 biquads x 2)
    private readonly double[] _envState = new double[24];       // 6 groups x 4 followers
    private readonly double[] _envCoeff = new double[48];       // 6 groups x 8 coeffs
    private readonly double[] _ssState = new double[6];         // 3 bands x 2 (mid_sq, side_sq)
    private readonly double[] _gateState = new double[3];       // transient gate smoothing
    private readonly double[] _boostLowCoeff = new double[5];
    private readonly double[] _boostHighCoeff = new double[5];
    private readonly double[] _boostLowState = new double[16];  // 8ch x 2
    private readonly double[] _boostHighState = new double[16];

    private double _ckEnv;
    private readonly double[] _ckCoeff = new double[2];
    private double _boostEnv;
    private readonly double[] _boostCoeff = new double[2];
    private double _lfeEnv;
    private readonly double[] _lfeCoeff = new double[2];
    private double _outGainLin = 1.0;
    private double _ssAlpha = 1.0;
    private double _gateAlpha = 1.0;

    public SpatialEngineEffect(int sampleRate = 48000)
    {
        SampleRate = sampleRate;
    }

    public string Name => "Spatial Engine";

    public bool IsEnabled { get; set; } = true;

    public int SampleRate { get; set; }

    /// <summary>Interleaved channel count (1-8). Selects the channel-mode branch.</summary>
    public int ChannelCount { get; set; } = 2;

    // ── Crossover ───────────────────────────────────────────────────────
    public double LowCutHz { get; set; } = 80;
    public double HighCutHz { get; set; } = 16000;
    public double Xover1Hz { get; set; } = 800;
    public double Xover2Hz { get; set; } = 3000;

    // ── FC Self-Suppression ─────────────────────────────────────────────
    public double FcSubLow { get; set; }
    public double FcSubMid { get; set; } = 0.70;
    public double FcSubHigh { get; set; } = 0.55;

    // ── Self Sound Gate ─────────────────────────────────────────────────
    public double GateDepthDb { get; set; } = -30;
    public double GateThresholdDb { get; set; } = -40;
    public double GateAttackMs { get; set; } = 1.0;
    public double GateReleaseMs { get; set; } = 80;
    public double GateKneeDb { get; set; } = 6;

    // ── Adaptive EQ ─────────────────────────────────────────────────────
    public double AeqLowGainDb { get; set; } = 3.0;
    public double AeqLowFreqHz { get; set; } = 300;
    public double AeqLowQ { get; set; } = 1.0;
    public double AeqHighGainDb { get; set; } = 6.0;
    public double AeqHighFreqHz { get; set; } = 2500;
    public double AeqHighQ { get; set; } = 1.0;
    public double AeqAttackMs { get; set; } = 1.0;
    public double AeqReleaseMs { get; set; } = 80;
    public double AeqRejectThresholdDb { get; set; } = -20;

    // ── Band 1 (Low) ────────────────────────────────────────────────────
    public double LowAttack { get; set; } = 0.5;
    public double LowSustain { get; set; } = 0.5;
    public double SurroundLowAttackStr { get; set; }
    public double SurroundLowSustainStr { get; set; }
    public double FrontLowAttackStr { get; set; }
    public double FrontLowSustainStr { get; set; }

    // ── Band 2 (Mid) ────────────────────────────────────────────────────
    public double MidAttack { get; set; } = 0.5;
    public double MidSustain { get; set; } = 0.5;
    public double SurroundMidAttackStr { get; set; }
    public double SurroundMidSustainStr { get; set; }
    public double FrontMidAttackStr { get; set; }
    public double FrontMidSustainStr { get; set; }

    // ── Band 3 (High) ───────────────────────────────────────────────────
    public double HighAttack { get; set; } = 0.5;
    public double HighSustain { get; set; } = 0.5;
    public double SurroundHighAttackStr { get; set; }
    public double SurroundHighSustainStr { get; set; }
    public double FrontHighAttackStr { get; set; }
    public double FrontHighSustainStr { get; set; }

    // ── Expansion ───────────────────────────────────────────────────────
    public double LowExpansionThresholdDb { get; set; } = -40;
    public double LowExpansionRatio { get; set; } = 1;
    public double LowExpansionFloorDb { get; set; } = -80;
    public double MidExpansionThresholdDb { get; set; } = -40;
    public double MidExpansionRatio { get; set; } = 1;
    public double MidExpansionFloorDb { get; set; } = -80;
    public double HighExpansionThresholdDb { get; set; } = -40;
    public double HighExpansionRatio { get; set; } = 1;
    public double HighExpansionFloorDb { get; set; } = -80;

    // ── CMR ─────────────────────────────────────────────────────────────
    public double CmrLow { get; set; }
    public double CmrMid { get; set; }
    public double CmrHigh { get; set; }

    // ── Master ──────────────────────────────────────────────────────────
    public double DirectionalEmphasis { get; set; }
    public double SelfSuppress { get; set; }
    public double OutputGainDb { get; set; }
    public double GainSmoothing { get; set; }
    public double DryWetMix { get; set; } = 1.0;

    // ── LFE Sidechain ───────────────────────────────────────────────────
    public double LfeModulationDepth { get; set; }
    public double LfeAttackMs { get; set; } = 50;
    public double LfeReleaseMs { get; set; } = 300;

    /// <summary>
    /// Optional lossless ArtTune preset carried by this effect. The 300-float
    /// chunk layout does not map 1:1 to the public slider set and cannot be
    /// decoded without the private ArtTuneKit DLL, so processing uses the
    /// default slider values unless a proven mapping is wired up later.
    /// The raw values are preserved verbatim for that purpose.
    /// </summary>
    public AtkSpatialPreset? Preset { get; set; }

    public void Process(Span<float> buffer)
    {
        if (!IsEnabled)
            return;

        var channels = Math.Clamp(ChannelCount, 1, 8);
        var frames = buffer.Length / channels;
        if (frames <= 0)
            return;

        UpdateBlockCoefficients();

        Span<double> spl = new double[8];
        Span<double> dry = new double[8];
        Span<double> band1 = new double[8];
        Span<double> band2 = new double[8];
        Span<double> band3 = new double[8];
        Span<double> passLow = new double[8];
        Span<double> passHigh = new double[8];

        var mode = channels == 7 ? 3 : channels >= 6 ? 2 : channels >= 3 ? 1 : 0;

        for (int frame = 0; frame < frames; frame++)
        {
            var baseIdx = frame * channels;
            for (int c = 0; c < channels; c++)
                spl[c] = buffer[baseIdx + c];

            ProcessSample(channels, mode, spl, dry, band1, band2, band3, passLow, passHigh);

            for (int c = 0; c < channels; c++)
                buffer[baseIdx + c] = (float)spl[c];
        }
    }

    public void Reset()
    {
        Array.Clear(_biquadCoeff);
        Array.Clear(_xoverState);
        Array.Clear(_subXoverState);
        Array.Clear(_envState);
        Array.Clear(_ssState);
        Array.Clear(_gateState);
        Array.Clear(_boostLowState);
        Array.Clear(_boostHighState);
        _ckEnv = 0;
        _boostEnv = 0;
        _lfeEnv = 0;
    }

    private void UpdateBlockCoefficients()
    {
        // Enforce frequency ordering (JSFX @slider).
        double lowCut = LowCutHz;
        double xover1 = Math.Max(Xover1Hz, lowCut + 1);
        double xover2 = Math.Max(Xover2Hz, xover1 + 1);
        double highCut = Math.Max(HighCutHz, xover2 + 1);
        xover2 = Math.Min(xover2, highCut - 1);
        xover1 = Math.Min(xover1, xover2 - 1);
        lowCut = Math.Min(lowCut, xover1 - 1);

        CalcBiquadLp(0, lowCut);
        CalcBiquadHp(5, lowCut);
        CalcBiquadLp(10, xover1);
        CalcBiquadHp(15, xover1);
        CalcBiquadLp(20, xover2);
        CalcBiquadHp(25, xover2);
        CalcBiquadLp(30, highCut);
        CalcBiquadHp(35, highCut);

        _outGainLin = Math.Exp(OutputGainDb / DbgK);

        const double fastAttT = 0.0001;
        const double fastRelT = 0.12;
        const double atkRelT = 0.15;
        const double decAttT = 0.0001;

        var b1AtkT = 0.0001 + (1.0 - LowAttack) * 0.3;
        var b1SusT = 0.0001 + (1.0 - LowSustain) * 0.5;
        var b2AtkT = 0.0001 + (1.0 - MidAttack) * 0.3;
        var b2SusT = 0.0001 + (1.0 - MidSustain) * 0.5;
        var b3AtkT = 0.0001 + (1.0 - HighAttack) * 0.3;
        var b3SusT = 0.0001 + (1.0 - HighSustain) * 0.5;

        var smoothT = Math.Max(0.0001, GainSmoothing * 0.05);

        // Front Band 1 (offset 0), Band 2 (8), Band 3 (16); surround copies at 24/32/40.
        SetEnvCoeffs(0, fastAttT, fastRelT, b1AtkT, atkRelT, decAttT, b1SusT, smoothT);
        SetEnvCoeffs(8, fastAttT, fastRelT, b2AtkT, atkRelT, decAttT, b2SusT, smoothT);
        SetEnvCoeffs(16, fastAttT, fastRelT, b3AtkT, atkRelT, decAttT, b3SusT, smoothT);
        Array.Copy(_envCoeff, 0, _envCoeff, 24, 8);
        Array.Copy(_envCoeff, 8, _envCoeff, 32, 8);
        Array.Copy(_envCoeff, 16, _envCoeff, 40, 8);

        _ckCoeff[0] = Math.Exp(-1.0 / (GateAttackMs * 0.001 * SampleRate));
        _ckCoeff[1] = Math.Exp(-1.0 / (GateReleaseMs * 0.001 * SampleRate));

        _boostCoeff[0] = Math.Exp(-1.0 / (AeqAttackMs * 0.001 * SampleRate));
        _boostCoeff[1] = Math.Exp(-1.0 / (AeqReleaseMs * 0.001 * SampleRate));

        if (AeqLowGainDb > 0)
            CalcPeakingEq(_boostLowCoeff, AeqLowFreqHz, AeqLowGainDb, AeqLowQ);
        if (AeqHighGainDb > 0)
            CalcPeakingEq(_boostHighCoeff, AeqHighFreqHz, AeqHighGainDb, AeqHighQ);

        _lfeCoeff[0] = Math.Exp(-1.0 / (LfeAttackMs * 0.001 * SampleRate));
        _lfeCoeff[1] = Math.Exp(-1.0 / (LfeReleaseMs * 0.001 * SampleRate));

        _ssAlpha = 1.0 - Math.Exp(-1.0 / (0.01 * SampleRate));
        _gateAlpha = 1.0 - Math.Exp(-1.0 / (0.005 * SampleRate));
    }

    private void SetEnvCoeffs(int o,
        double fastAtt, double fastRel, double atkAtt, double atkRel,
        double decAtt, double decRel, double smooth)
    {
        _envCoeff[o + 0] = Math.Exp(-1.0 / (fastAtt * SampleRate));
        _envCoeff[o + 1] = Math.Exp(-1.0 / (fastRel * SampleRate));
        _envCoeff[o + 2] = Math.Exp(-1.0 / (atkAtt * SampleRate));
        _envCoeff[o + 3] = Math.Exp(-1.0 / (atkRel * SampleRate));
        _envCoeff[o + 4] = Math.Exp(-1.0 / (decAtt * SampleRate));
        _envCoeff[o + 5] = Math.Exp(-1.0 / (decRel * SampleRate));
        _envCoeff[o + 6] = Math.Exp(-1.0 / (smooth * SampleRate));
        _envCoeff[o + 7] = Math.Exp(-1.0 / (smooth * SampleRate));
    }

    private void CalcBiquadLp(int o, double freq)
    {
        var w0 = 2.0 * Math.PI * freq / SampleRate;
        var cw = Math.Cos(w0);
        var sw = Math.Sin(w0);
        var alpha = sw / (2.0 * BwQ);
        var a0 = 1.0 + alpha;
        _biquadCoeff[o + 0] = (1.0 - cw) / 2.0 / a0;
        _biquadCoeff[o + 1] = (1.0 - cw) / a0;
        _biquadCoeff[o + 2] = (1.0 - cw) / 2.0 / a0;
        _biquadCoeff[o + 3] = -2.0 * cw / a0;
        _biquadCoeff[o + 4] = (1.0 - alpha) / a0;
    }

    private void CalcBiquadHp(int o, double freq)
    {
        var w0 = 2.0 * Math.PI * freq / SampleRate;
        var cw = Math.Cos(w0);
        var sw = Math.Sin(w0);
        var alpha = sw / (2.0 * BwQ);
        var a0 = 1.0 + alpha;
        _biquadCoeff[o + 0] = (1.0 + cw) / 2.0 / a0;
        _biquadCoeff[o + 1] = -(1.0 + cw) / a0;
        _biquadCoeff[o + 2] = (1.0 + cw) / 2.0 / a0;
        _biquadCoeff[o + 3] = -2.0 * cw / a0;
        _biquadCoeff[o + 4] = (1.0 - alpha) / a0;
    }

    private void CalcPeakingEq(double[] co, double freq, double gainDb, double q)
    {
        var a = Math.Exp(gainDb / (2.0 * DbgK));
        var w0 = 2.0 * Math.PI * freq / SampleRate;
        var cw = Math.Cos(w0);
        var sw = Math.Sin(w0);
        var alpha = sw / (2.0 * q);
        var a0 = 1.0 + alpha / a;
        co[0] = (1.0 + alpha * a) / a0;
        co[1] = -2.0 * cw / a0;
        co[2] = (1.0 - alpha * a) / a0;
        co[3] = -2.0 * cw / a0;
        co[4] = (1.0 - alpha / a) / a0;
    }

    /// <summary>x = input, co = biquad coefficients (5), so = 2 state slots.</summary>
    private static double ProcessBiquad(double x, ReadOnlySpan<double> co, Span<double> so)
    {
        var y = co[0] * x + so[0];
        so[0] = co[1] * x - co[3] * y + so[1];
        so[1] = co[2] * x - co[4] * y;
        return y;
    }

    // LR4 low-pass: two chained biquads (co at crossoverIndex*10).
    private double Lr4Lp(double x, int xoverIdx, int ch)
        => ProcessBiquad(
            ProcessBiquad(x, _biquadCoeff.AsSpan(xoverIdx * 10, 5), _xoverState.AsSpan(xoverIdx * 64 + ch * 2, 2)),
            _biquadCoeff.AsSpan(xoverIdx * 10, 5), _xoverState.AsSpan(xoverIdx * 64 + 16 + ch * 2, 2));

    // LR4 high-pass: coeffs at crossoverIndex*10 + 5.
    private double Lr4Hp(double x, int xoverIdx, int ch)
        => ProcessBiquad(
            ProcessBiquad(x, _biquadCoeff.AsSpan(xoverIdx * 10 + 5, 5), _xoverState.AsSpan(xoverIdx * 64 + 32 + ch * 2, 2)),
            _biquadCoeff.AsSpan(xoverIdx * 10 + 5, 5), _xoverState.AsSpan(xoverIdx * 64 + 48 + ch * 2, 2));

    /// <summary>Channel envelope follower; co0 = attack, co1 = release.</summary>
    private static double EvalFollower(double input, ref double env, double atkCoeff, double relCoeff)
    {
        var e = input > env
            ? atkCoeff * (env - input) + input
            : relCoeff * (env - input) + input;
        env = e;
        return e;
    }

    private double CalcBandGain(double level, int bandIdx,
        double atkStr, double susStr, double expThresh, double expRatio, double expFloor)
    {
        var levelDb = DbgK * Math.Log(level + 0.0000000001);

        var eo = bandIdx * 4;
        var co = bandIdx * 8;

        var fastE = EvalFollower(levelDb, ref _envState[eo + 0], _envCoeff[co + 0], _envCoeff[co + 1]);
        var atkE = EvalFollower(levelDb, ref _envState[eo + 1], _envCoeff[co + 2], _envCoeff[co + 3]);
        var decE = EvalFollower(levelDb, ref _envState[eo + 2], _envCoeff[co + 4], _envCoeff[co + 5]);

        var susDelta = decE - fastE;
        var susCutDb = susDelta > 0
            ? susStr * susDelta * (1.0 + 2.0 * Math.Abs(susStr) * susDelta / 15.0)
            : 0.0;

        var gainDb = atkStr * (atkE - fastE) + susCutDb;

        if (expRatio > 1 && levelDb < expThresh)
        {
            var below = expThresh - levelDb;
            var expReduction = below * (expRatio - 1);
            var expGainDb = Math.Min(0.0, -Math.Min(expReduction, levelDb + gainDb - expFloor));
            gainDb += expGainDb;
        }

        if (GainSmoothing > 0)
            gainDb = EvalFollower(gainDb, ref _envState[eo + 3], _envCoeff[co + 6], _envCoeff[co + 7]);

        gainDb = Math.Max(-40, Math.Min(24, gainDb));
        return Math.Exp(gainDb / DbgK);
    }

    private void ProcessSample(int channels, int mode,
        Span<double> spl, Span<double> dry,
        Span<double> band1, Span<double> band2, Span<double> band3,
        Span<double> passLow, Span<double> passHigh)
    {
        var ckMode = mode;

        // ── LFE sidechain ───────────────────────────────────────────────
        double lfeNorm, lfeFactor;
        if (channels >= 8)
        {
            var lfeAbs = Math.Abs(spl[7]);
            var lfeDb = DbgK * Math.Log(lfeAbs + 1e-10);
            var lfeEnvDb = EvalFollower(lfeDb, ref _lfeEnv, _lfeCoeff[0], _lfeCoeff[1]);
            lfeNorm = Math.Max(0, Math.Min(1, (lfeEnvDb + 110) / 80));
            lfeFactor = (1.0 - LfeModulationDepth) + LfeModulationDepth * lfeNorm;
        }
        else
        {
            lfeNorm = 0;
            lfeFactor = 1.0;
        }

        // ── Step 1: dry capture ─────────────────────────────────────────
        for (int c = 0; c < channels; c++)
            dry[c] = spl[c];

        // ── Step 2: FC self-suppression (subtract filtered center) ──────
        if (ckMode >= 1)
        {
            var fcRaw = ckMode == 2 ? (spl[4] + spl[5]) * 0.5 : spl[2];

            if (FcSubLow > 0 || FcSubMid > 0 || FcSubHigh > 0)
            {
                // Split through xover1 (coeff offset 10 = LP, 15 = HP).
                var fcLow = ProcessBiquad(fcRaw, _biquadCoeff.AsSpan(10, 5), _subXoverState.AsSpan(0, 2));
                fcLow = ProcessBiquad(fcLow, _biquadCoeff.AsSpan(10, 5), _subXoverState.AsSpan(2, 2));
                var fcAbove1 = ProcessBiquad(fcRaw, _biquadCoeff.AsSpan(15, 5), _subXoverState.AsSpan(4, 2));
                fcAbove1 = ProcessBiquad(fcAbove1, _biquadCoeff.AsSpan(15, 5), _subXoverState.AsSpan(6, 2));

                // Split through xover2 (coeff offset 20 = LP, 25 = HP).
                var fcMid = ProcessBiquad(fcAbove1, _biquadCoeff.AsSpan(20, 5), _subXoverState.AsSpan(8, 2));
                fcMid = ProcessBiquad(fcMid, _biquadCoeff.AsSpan(20, 5), _subXoverState.AsSpan(10, 2));
                var fcHigh = ProcessBiquad(fcAbove1, _biquadCoeff.AsSpan(25, 5), _subXoverState.AsSpan(12, 2));
                fcHigh = ProcessBiquad(fcHigh, _biquadCoeff.AsSpan(25, 5), _subXoverState.AsSpan(14, 2));

                var fcSub = FcSubLow * lfeFactor * fcLow
                          + FcSubMid * lfeFactor * fcMid
                          + FcSubHigh * lfeFactor * fcHigh;
                spl[0] -= fcSub;
                spl[1] -= fcSub;
                if (ckMode == 2)
                {
                    spl[2] -= fcSub;
                    spl[3] -= fcSub;
                }
            }
        }

        // ── Step 3: Self Sound Gate / center duck ───────────────────────
        double ckGain;
        if (ckMode >= 1)
        {
            var ckLevel = ckMode == 2 ? Math.Max(Math.Abs(spl[4]), Math.Abs(spl[5])) : Math.Abs(spl[2]);
            var ckLevelDb = DbgK * Math.Log(ckLevel + 1e-10);
            _ckEnv = ckLevelDb > _ckEnv
                ? _ckCoeff[0] * (_ckEnv - ckLevelDb) + ckLevelDb
                : _ckCoeff[1] * (_ckEnv - ckLevelDb) + ckLevelDb;

            if (_ckEnv > GateThresholdDb)
            {
                var ckReductionDb = GateKneeDb > 0
                    ? GateDepthDb * lfeFactor * Math.Min(1.0, (_ckEnv - GateThresholdDb) / GateKneeDb)
                    : GateDepthDb * lfeFactor;
                ckGain = Math.Exp(ckReductionDb / DbgK);
            }
            else
            {
                ckGain = 1.0;
            }

            // M/S selective duck on L/R.
            var midSig = (spl[0] + spl[1]) * 0.5 * ckGain;
            var sideSig = (spl[0] - spl[1]) * 0.5;
            spl[0] = midSig + sideSig;
            spl[1] = midSig - sideSig;

            if (ckMode == 2)
            {
                midSig = (spl[2] + spl[3]) * 0.5 * ckGain;
                sideSig = (spl[2] - spl[3]) * 0.5;
                spl[2] = midSig + sideSig;
                spl[3] = midSig - sideSig;
            }

            // Zero center channel after sidechain use.
            if (ckMode == 2) { spl[4] = 0; spl[5] = 0; }
            else { spl[2] = 0; }
        }
        else
        {
            ckGain = 1.0;
        }

        // ── Step 4: Adaptive EQ ─────────────────────────────────────────
        if (AeqLowGainDb > 0 || AeqHighGainDb > 0)
        {
            var boostTarget = (ckGain > 0.9 && lfeNorm < 0.5) ? 1.0 : 0.0;

            // Gunshot rejection: suppress if any listen channel very loud.
            var peakAll = ckMode switch
            {
                3 => Math.Max(Math.Max(Math.Max(Math.Abs(spl[0]), Math.Abs(spl[1])), Math.Max(Math.Abs(spl[3]), Math.Abs(spl[4]))),
                              Math.Max(Math.Abs(spl[5]), Math.Abs(spl[6]))),
                2 => Math.Max(Math.Max(Math.Abs(spl[0]), Math.Abs(spl[1])), Math.Max(Math.Abs(spl[2]), Math.Abs(spl[3]))),
                _ => Math.Max(Math.Abs(spl[0]), Math.Abs(spl[1]))
            };
            if (DbgK * Math.Log(peakAll + 1e-10) > AeqRejectThresholdDb)
                boostTarget = 0;

            var boostEnvOld = _boostEnv;
            _boostEnv = boostTarget > boostEnvOld
                ? _boostCoeff[0] * (boostEnvOld - boostTarget) + boostTarget
                : _boostCoeff[1] * (boostEnvOld - boostTarget) + boostTarget;
            var boostEnv = _boostEnv;

            if (ckMode == 3)
            {
                // Mode D: boost spl0,1,3,4,5,6 with sequential state slots.
                ApplyBoost(spl, 0, 0, boostEnv);
                ApplyBoost(spl, 1, 1, boostEnv);
                ApplyBoost(spl, 3, 2, boostEnv);
                ApplyBoost(spl, 4, 3, boostEnv);
                ApplyBoost(spl, 5, 4, boostEnv);
                ApplyBoost(spl, 6, 5, boostEnv);
            }
            else if (ckMode == 1)
            {
                ApplyBoost(spl, 0, 0, boostEnv);
                ApplyBoost(spl, 1, 1, boostEnv);
            }
            else if (ckMode == 2)
            {
                ApplyBoost(spl, 0, 0, boostEnv);
                ApplyBoost(spl, 1, 1, boostEnv);
                ApplyBoost(spl, 2, 2, boostEnv);
                ApplyBoost(spl, 3, 3, boostEnv);
            }
            // ckMode == 0 (stereo): envelope computed but no boost applied (JSFX behavior).
        }

        // ── Step 5: crossover split (LR4, per channel) ──────────────────
        // Channel 7 (LFE, 8ch) is never split or written by the JSFX.
        var procCh = Math.Min(channels, 7);
        for (int c = 0; c < procCh; c++)
        {
            var aboveLow = Lr4Hp(spl[c], 0, c);
            passLow[c] = Lr4Lp(spl[c], 0, c);
            var above1 = Lr4Hp(aboveLow, 1, c);
            band1[c] = Lr4Lp(aboveLow, 1, c);
            var above2 = Lr4Hp(above1, 2, c);
            band2[c] = Lr4Lp(above1, 2, c);
            var above3 = Lr4Hp(above2, 3, c);
            band3[c] = Lr4Lp(above2, 3, c);
            passHigh[c] = above3;
        }

        // ── Step 6: self-suppress ───────────────────────────────────────
        double ratioB1 = 1, ratioB2 = 1, ratioB3 = 1;
        if (SelfSuppress > 0)
        {
            if (channels >= 7)
            {
                ratioB1 = SelfSuppressGroup(band1, 3, 7, SelfSuppress, lfeFactor, 0, 0);
                ratioB2 = SelfSuppressGroup(band2, 3, 7, SelfSuppress, lfeFactor, 1, 1);
                ratioB3 = SelfSuppressGroup(band3, 3, 7, SelfSuppress, lfeFactor, 2, 2);
            }
            else if (channels >= 6)
            {
                ratioB1 = SelfSuppressGroup(band1, 2, 6, SelfSuppress, lfeFactor, 0, 0);
                ratioB2 = SelfSuppressGroup(band2, 2, 6, SelfSuppress, lfeFactor, 1, 1);
                ratioB3 = SelfSuppressGroup(band3, 2, 6, SelfSuppress, lfeFactor, 2, 2);
            }
            else if (channels >= 4)
            {
                SelfSuppressFixed4(band1, SelfSuppress);
                SelfSuppressFixed4(band2, SelfSuppress);
                SelfSuppressFixed4(band3, SelfSuppress);
            }
        }

        // ── Step 7: CMR ─────────────────────────────────────────────────
        if (channels >= 7)
        {
            ApplyCmr(band1, 3, 7, CmrLow, lfeFactor);
            ApplyCmr(band2, 3, 7, CmrMid, lfeFactor);
            ApplyCmr(band3, 3, 7, CmrHigh, lfeFactor);
        }
        else if (channels >= 6)
        {
            ApplyCmr(band1, 2, 6, CmrLow, lfeFactor);
            ApplyCmr(band2, 2, 6, CmrMid, lfeFactor);
            ApplyCmr(band3, 2, 6, CmrHigh, lfeFactor);
        }

        // ── Step 8: transient shaping + directional emphasis ────────────
        if (channels >= 7)
        {
            // 7ch: Front = 0,1,2 ; Surround = 3,4,5,6
            var l1f = Math.Max(Math.Abs(band1[0]), Math.Max(Math.Abs(band1[1]), Math.Abs(band1[2])));
            var g1f = CalcBandGain(l1f, 0, FrontLowAttackStr, FrontLowSustainStr * lfeFactor, LowExpansionThresholdDb, LowExpansionRatio, LowExpansionFloorDb);
            band1[0] *= g1f; band1[1] *= g1f; band1[2] *= g1f;

            var l1s = Math.Max(Math.Abs(band1[3]), Math.Max(Math.Abs(band1[4]), Math.Max(Math.Abs(band1[5]), Math.Abs(band1[6]))));
            var g1s = CalcBandGain(l1s, 3, SurroundLowAttackStr, SurroundLowSustainStr, LowExpansionThresholdDb, LowExpansionRatio, LowExpansionFloorDb);
            band1[3] *= g1s; band1[4] *= g1s; band1[5] *= g1s; band1[6] *= g1s;

            var l2f = Math.Max(Math.Abs(band2[0]), Math.Max(Math.Abs(band2[1]), Math.Abs(band2[2])));
            var g2f = CalcBandGain(l2f, 1, FrontMidAttackStr, FrontMidSustainStr * lfeFactor, MidExpansionThresholdDb, MidExpansionRatio, MidExpansionFloorDb);
            band2[0] *= g2f; band2[1] *= g2f; band2[2] *= g2f;

            var l2s = Math.Max(Math.Abs(band2[3]), Math.Max(Math.Abs(band2[4]), Math.Max(Math.Abs(band2[5]), Math.Abs(band2[6]))));
            var g2s = CalcBandGain(l2s, 4, SurroundMidAttackStr, SurroundMidSustainStr, MidExpansionThresholdDb, MidExpansionRatio, MidExpansionFloorDb);
            band2[3] *= g2s; band2[4] *= g2s; band2[5] *= g2s; band2[6] *= g2s;

            var l3f = Math.Max(Math.Abs(band3[0]), Math.Max(Math.Abs(band3[1]), Math.Abs(band3[2])));
            var g3f = CalcBandGain(l3f, 2, FrontHighAttackStr, FrontHighSustainStr * lfeFactor, HighExpansionThresholdDb, HighExpansionRatio, HighExpansionFloorDb);
            band3[0] *= g3f; band3[1] *= g3f; band3[2] *= g3f;

            var l3s = Math.Max(Math.Abs(band3[3]), Math.Max(Math.Abs(band3[4]), Math.Max(Math.Abs(band3[5]), Math.Abs(band3[6]))));
            var g3s = CalcBandGain(l3s, 5, SurroundHighAttackStr, SurroundHighSustainStr, HighExpansionThresholdDb, HighExpansionRatio, HighExpansionFloorDb);
            band3[3] *= g3s; band3[4] *= g3s; band3[5] *= g3s; band3[6] *= g3s;

            if (DirectionalEmphasis > 0)
            {
                ApplyDirectionalEmphasis(band1, 3, 7, DirectionalEmphasis, ratioB1);
                ApplyDirectionalEmphasis(band2, 3, 7, DirectionalEmphasis, ratioB2);
                ApplyDirectionalEmphasis(band3, 3, 7, DirectionalEmphasis, ratioB3);
            }
        }
        else if (ckMode == 2 && channels >= 6)
        {
            // 6ch: Front = 0,1 ; Surround = 2,3,4,5
            var l1f = Math.Max(Math.Abs(band1[0]), Math.Abs(band1[1]));
            var g1f = CalcBandGain(l1f, 0, FrontLowAttackStr, FrontLowSustainStr * lfeFactor, LowExpansionThresholdDb, LowExpansionRatio, LowExpansionFloorDb);
            band1[0] *= g1f; band1[1] *= g1f;

            var l1s = Math.Max(Math.Abs(band1[2]), Math.Max(Math.Abs(band1[3]), Math.Max(Math.Abs(band1[4]), Math.Abs(band1[5]))));
            var g1s = CalcBandGain(l1s, 3, SurroundLowAttackStr, SurroundLowSustainStr, LowExpansionThresholdDb, LowExpansionRatio, LowExpansionFloorDb);
            band1[2] *= g1s; band1[3] *= g1s; band1[4] *= g1s; band1[5] *= g1s;

            var l2f = Math.Max(Math.Abs(band2[0]), Math.Abs(band2[1]));
            var g2f = CalcBandGain(l2f, 1, FrontMidAttackStr, FrontMidSustainStr * lfeFactor, MidExpansionThresholdDb, MidExpansionRatio, MidExpansionFloorDb);
            band2[0] *= g2f; band2[1] *= g2f;

            var l2s = Math.Max(Math.Abs(band2[2]), Math.Max(Math.Abs(band2[3]), Math.Max(Math.Abs(band2[4]), Math.Abs(band2[5]))));
            var g2s = CalcBandGain(l2s, 4, SurroundMidAttackStr, SurroundMidSustainStr, MidExpansionThresholdDb, MidExpansionRatio, MidExpansionFloorDb);
            band2[2] *= g2s; band2[3] *= g2s; band2[4] *= g2s; band2[5] *= g2s;

            var l3f = Math.Max(Math.Abs(band3[0]), Math.Abs(band3[1]));
            var g3f = CalcBandGain(l3f, 2, FrontHighAttackStr, FrontHighSustainStr * lfeFactor, HighExpansionThresholdDb, HighExpansionRatio, HighExpansionFloorDb);
            band3[0] *= g3f; band3[1] *= g3f;

            var l3s = Math.Max(Math.Abs(band3[2]), Math.Max(Math.Abs(band3[3]), Math.Max(Math.Abs(band3[4]), Math.Abs(band3[5]))));
            var g3s = CalcBandGain(l3s, 5, SurroundHighAttackStr, SurroundHighSustainStr, HighExpansionThresholdDb, HighExpansionRatio, HighExpansionFloorDb);
            band3[2] *= g3s; band3[3] *= g3s; band3[4] *= g3s; band3[5] *= g3s;

            if (DirectionalEmphasis > 0)
            {
                ApplyDirectionalEmphasis(band1, 2, 6, DirectionalEmphasis, ratioB1);
                ApplyDirectionalEmphasis(band2, 2, 6, DirectionalEmphasis, ratioB2);
                ApplyDirectionalEmphasis(band3, 2, 6, DirectionalEmphasis, ratioB3);
            }
        }
        else
        {
            // 2ch / 4ch fallback: single detection group.
            var l1 = Math.Max(Math.Abs(band1[0]), Math.Abs(band1[1]));
            if (channels >= 4) l1 = Math.Max(l1, Math.Max(Math.Abs(band1[2]), Math.Abs(band1[3])));
            var g1 = CalcBandGain(l1, 0, SurroundLowAttackStr, SurroundLowSustainStr, LowExpansionThresholdDb, LowExpansionRatio, LowExpansionFloorDb);
            band1[0] *= g1; band1[1] *= g1;
            if (channels >= 4) { band1[2] *= g1; band1[3] *= g1; }

            var l2 = Math.Max(Math.Abs(band2[0]), Math.Abs(band2[1]));
            if (channels >= 4) l2 = Math.Max(l2, Math.Max(Math.Abs(band2[2]), Math.Abs(band2[3])));
            var g2 = CalcBandGain(l2, 1, SurroundMidAttackStr, SurroundMidSustainStr, MidExpansionThresholdDb, MidExpansionRatio, MidExpansionFloorDb);
            band2[0] *= g2; band2[1] *= g2;
            if (channels >= 4) { band2[2] *= g2; band2[3] *= g2; }

            var l3 = Math.Max(Math.Abs(band3[0]), Math.Abs(band3[1]));
            if (channels >= 4) l3 = Math.Max(l3, Math.Max(Math.Abs(band3[2]), Math.Abs(band3[3])));
            var g3 = CalcBandGain(l3, 2, SurroundHighAttackStr, SurroundHighSustainStr, HighExpansionThresholdDb, HighExpansionRatio, HighExpansionFloorDb);
            band3[0] *= g3; band3[1] *= g3;
            if (channels >= 4) { band3[2] *= g3; band3[3] *= g3; }
        }

        // ── Step 9: sum & output ────────────────────────────────────────
        var wet0 = (passLow[0] + band1[0] + band2[0] + band3[0] + passHigh[0]) * _outGainLin;
        var wet1 = (passLow[1] + band1[1] + band2[1] + band3[1] + passHigh[1]) * _outGainLin;
        spl[0] = dry[0] * (1.0 - DryWetMix) + wet0 * DryWetMix;
        spl[1] = dry[1] * (1.0 - DryWetMix) + wet1 * DryWetMix;

        for (int c = 2; c < procCh; c++)
        {
            var wet = (passLow[c] + band1[c] + band2[c] + band3[c] + passHigh[c]) * _outGainLin;
            spl[c] = dry[c] * (1.0 - DryWetMix) + wet * DryWetMix;
        }
    }

    private void ApplyBoost(Span<double> spl, int ch, int slot, double boostEnv)
    {
        if (AeqLowGainDb > 0)
        {
            var tmp = ProcessBiquad(spl[ch], _boostLowCoeff, _boostLowState.AsSpan(slot * 2, 2));
            spl[ch] += (tmp - spl[ch]) * boostEnv;
        }
        if (AeqHighGainDb > 0)
        {
            var tmp = ProcessBiquad(spl[ch], _boostHighCoeff, _boostHighState.AsSpan(slot * 2, 2));
            spl[ch] += (tmp - spl[ch]) * boostEnv;
        }
    }

    /// <summary>
    /// Self-suppress for 6ch/7ch. Surround channels span [surrStart, surrStart+4).
    /// bandIdx picks SS_STATE pair (bandIdx*2) and surround env group (bandIdx+3).
    /// </summary>
    private double SelfSuppressGroup(Span<double> band, int surrStart, int surrEnd,
        double selfSup, double lfeFactor, int bandIdx, int gateIdx)
    {
        var surrMid = (band[surrStart] + band[surrStart + 1] + band[surrStart + 2] + band[surrStart + 3]) * 0.25;
        var midSq = surrMid * surrMid;
        var sideSq = ((band[surrStart] - surrMid) * (band[surrStart] - surrMid)
                    + (band[surrStart + 1] - surrMid) * (band[surrStart + 1] - surrMid)
                    + (band[surrStart + 2] - surrMid) * (band[surrStart + 2] - surrMid)
                    + (band[surrStart + 3] - surrMid) * (band[surrStart + 3] - surrMid)) * 0.25;

        var ssIdx = bandIdx * 2;
        _ssState[ssIdx] += _ssAlpha * (midSq - _ssState[ssIdx]);
        _ssState[ssIdx + 1] += _ssAlpha * (sideSq - _ssState[ssIdx + 1]);
        var ratio = _ssState[ssIdx] / (_ssState[ssIdx] + _ssState[ssIdx + 1] + 1e-20);

        // Surround envelope group: ENV_STATE + (bandIdx+3)*4; fast > atk => transient.
        var eo = (bandIdx + 3) * 4;
        var isTransient = _envState[eo] > _envState[eo + 1] ? 1.0 : 0.0;
        _gateState[gateIdx] += _gateAlpha * (isTransient - _gateState[gateIdx]);

        var suppressAmount = selfSup * lfeFactor * ratio * _gateState[gateIdx];
        band[0] -= surrMid * suppressAmount;
        band[1] -= surrMid * suppressAmount;
        for (int c = surrStart; c < surrEnd; c++)
            band[c] -= surrMid * suppressAmount;
        return ratio;
    }

    private static void SelfSuppressFixed4(Span<double> band, double selfSup)
    {
        var mid = (band[0] + band[1] + band[2] + band[3]) * 0.25;
        for (int c = 0; c < 4; c++)
            band[c] -= mid * selfSup;
    }

    private static void ApplyCmr(Span<double> band, int start, int end, double amount, double lfeFactor)
    {
        if (amount <= 0)
            return;
        var sum = 0.0;
        for (int c = start; c < end; c++)
            sum += band[c];
        var k = amount * lfeFactor / 3.0;
        var s = 1.0 + k;
        for (int c = start; c < end; c++)
            band[c] = s * band[c] - k * sum;
    }

    private static void ApplyDirectionalEmphasis(Span<double> band, int start, int end, double amount, double ratio)
    {
        var mult = 1.0 + amount * (1.0 - ratio);
        for (int c = start; c < end; c++)
            band[c] *= mult;
    }
}