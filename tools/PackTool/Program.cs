using System.Text.RegularExpressions;

// Builds Keyboard Utils instrument sounds (SoundFont 2) from the VSCO 2 Community Edition samples (CC0) and checks
// every SoundFont - built or FreePats' - by playing each preset's notes through MeltySynth.
//   PackTool build <tree.tsv> <cacheDir> <outDir>
//   PackTool check <file.sf2>
if (args.Length >= 2 && args[0] == "check")
{
    Check(args[1]);
    return;
}
if (args.Length < 4 || args[0] != "build")
{
    Console.WriteLine("usage: build <tree.tsv> <cacheDir> <outDir> | check <file.sf2>");
    return;
}

var tree = File.ReadAllLines(args[1]).Select(l => l.Split('\t')[0]).ToList();
string cache = args[2], outDir = args[3];
Directory.CreateDirectory(cache);
Directory.CreateDirectory(outDir);
using var http = new HttpClient();

// Which samples: folder, then a preferred file per note (one dynamic layer, first round robin).
var instruments = new (string Id, string Title, (string Name, int Program, string Folder, string[] Prefer, double MaxSeconds, bool Loop, double Release)[] Presets)[]
{
    ("violin", "Violin (VSCO 2 CE)", [("Violin arco", 0, "Strings/Solo Violin/Arco Vib", ["_f"], 5, true, 0.25), ("Violin pizzicato", 1, "Strings/Solo Violin/Pizz", ["_f_RR1", "_f"], 1.6, false, 0.2)]),
    ("viola", "Viola (VSCO 2 CE)", [("Viola arco", 0, "Strings/Viola Section/susvib", ["_v2_1", "_v2", "_v1"], 5, true, 0.25), ("Viola pizzicato", 1, "Strings/Viola Section/pizz", ["_v2_rr1", "_v1_rr1"], 1.6, false, 0.2)]),
    ("cello", "Cello (VSCO 2 CE)", [("Cello arco", 0, "Strings/Cello Section/susvib", ["_v2_1", "_v2", "_v3_1", "_v1"], 5, true, 0.3), ("Cello pizzicato", 1, "Strings/Cello Section/pizzT", ["_v2_RR1", "_v1_RR1"], 2, false, 0.25)]),
    ("flute", "Flute (VSCO 2 CE)", [("Flute", 0, "Woodwinds/Flute/susvib", ["_v1_1", "_v1"], 5, true, 0.15)]),
    ("trumpet", "Trumpet (VSCO 2 CE)", [("Trumpet", 0, "Brass/Trumpet/susvib", ["_v2_rr1", "_v1_rr1"], 4, true, 0.15)]),
};

foreach (var (id, title, presetSpecs) in instruments)
{
    var presets = new List<Preset>();
    foreach (var spec in presetSpecs)
    {
        var files = tree.Where(p => p.StartsWith(spec.Folder + "/") && p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)).ToList();
        var byNote = files.GroupBy(NoteOf).Where(g => g.Key >= 0);
        var samples = new List<Sample>();
        foreach (var note in byNote.OrderBy(g => g.Key))
        {
            string file = spec.Prefer.Select(token => note.FirstOrDefault(f => Path.GetFileName(f).Contains(token, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(f => f != null) ?? note.OrderBy(f => f).First();
            string local = Path.Combine(cache, file.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(local))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(local)!);
                string url = "https://raw.githubusercontent.com/sgossner/VSCO-2-CE/master/" + string.Join("/", file.Split('/').Select(Uri.EscapeDataString));
                File.WriteAllBytes(local, await http.GetByteArrayAsync(url));
            }
            var (data, rate) = Wav.ReadMono(local);
            samples.Add(Shape($"{id}{note.Key}{spec.Program}", data, rate, note.Key, spec.MaxSeconds, spec.Loop));
        }
        // The file names' octave numbering differs between VSCO instruments: measure what the samples really sound
        // and move the whole preset by the octave most of them agree on.
        var votes = samples.Select(s => Pitch.MeasuredMidi(s.Data, s.Rate) is { } m ? (int)Math.Round((m - s.RootKey) / 12.0) : (int?)null)
            .Where(v => v != null).GroupBy(v => v!.Value).OrderByDescending(g => g.Count()).ToList();
        int octaves = votes.Count > 0 ? votes[0].Key : 0;
        Console.WriteLine($"  measured octave offset: {octaves} (votes {string.Join(", ", votes.Select(g => $"{g.Key}:{g.Count()}"))})");
        if (octaves != 0)
            samples = samples.Select(s => s with { RootKey = s.RootKey + 12 * octaves }).ToList();
        Normalize(samples);
        presets.Add(new Preset(spec.Name, spec.Program, samples, spec.Release, spec.Loop));
        Console.WriteLine($"{id} / {spec.Name}: {samples.Count} notes, keys {samples.Min(s => s.RootKey)}-{samples.Max(s => s.RootKey)}");
    }
    string path = Path.Combine(outDir, id + ".sf2");
    Sf2Writer.Write(path, title, "Built for Keyboard Utils from the VSCO 2 Community Edition samples by Versilian Studios (CC0 1.0).", presets);
    Console.WriteLine($"  -> {path} {new FileInfo(path).Length / 1024} KB");
    Check(path);
}

// MIDI note from a VSCO file name: ..._A#3_... / ..._C4_...
static int NoteOf(string path)
{
    var m = Regex.Match(Path.GetFileName(path), @"_([A-G])(#|b)?(-?\d)_", RegexOptions.IgnoreCase);
    if (!m.Success)
        return -1;
    int[] pcs = [9, 11, 0, 2, 4, 5, 7]; // A B C D E F G
    int pc = pcs["ABCDEFG".IndexOf(char.ToUpper(m.Groups[1].Value[0]))];
    if (m.Groups[2].Value == "#") pc++;
    if (m.Groups[2].Value == "b") pc--;
    return (int.Parse(m.Groups[3].Value) + 1) * 12 + pc;
}

// Trim the silence in front, cut to a length, and either fade out or loop a steady stretch with a crossfade.
static Sample Shape(string name, float[] data, int rate, int key, double maxSeconds, bool loop)
{
    float peak = data.Length == 0 ? 0 : data.Max(Math.Abs);
    int start = 0;
    while (start < data.Length && Math.Abs(data[start]) < peak * 0.01f)
        start++;
    start = Math.Max(0, start - rate / 200);
    int length = Math.Min(data.Length - start, (int)(maxSeconds * rate));
    var cut = data.AsSpan(start, length).ToArray();
    int loopStart = -1, loopEnd = -1;
    if (loop && length > rate)
    {
        // Loop the middle: from 45 % to 85 % of the note, the end crossfaded with what's before the loop start.
        loopStart = (int)(length * 0.45);
        loopEnd = (int)(length * 0.85);
        int fade = Math.Min(rate / 8, loopStart);
        for (int i = 0; i < fade; i++)
        {
            float t = (float)i / fade;
            int at = loopEnd - fade + i;
            cut[at] = cut[at] * (1 - t) + cut[loopStart - fade + i] * t;
        }
        length = loopEnd + 1;
        cut = cut[..length];
    }
    else
    {
        int fade = Math.Min(rate / 20, length / 4);
        for (int i = 0; i < fade; i++)
            cut[length - 1 - i] *= (float)i / fade;
    }
    var shorts = cut.Select(v => (short)Math.Clamp(v * 32767, -32768, 32767)).ToArray();
    return new Sample(name, shorts, rate, key, loopStart, loopEnd);
}

// Every note brought to the same loudness (RMS of its first second, peaks kept under -1 dBFS): for practising, a quiet
// low flute note shouldn't be inaudible next to a loud high one.
static void Normalize(List<Sample> samples)
{
    const double targetRms = 0.18 * 32767, maxPeak = 0.89 * 32767;
    foreach (var s in samples)
    {
        var d = s.Data;
        int n = Math.Min(d.Length, s.Rate);
        double rms = Math.Sqrt(d.Take(n).Sum(v => (double)v * v) / Math.Max(1, n));
        int peak = d.Max(v => Math.Abs((int)v));
        if (rms <= 0 || peak == 0)
            continue;
        double gain = Math.Min(targetRms / rms, maxPeak / peak);
        for (int j = 0; j < d.Length; j++)
            d[j] = (short)Math.Clamp(Math.Round(d[j] * gain), -32768, 32767);
    }
}

// Plays every preset's notes through MeltySynth: each must sound.
static void Check(string path)
{
    var font = new MeltySynth.SoundFont(path);
    var synth = new MeltySynth.Synthesizer(font, 44100);
    Console.WriteLine($"  check {Path.GetFileName(path)}: {font.Presets.Count} presets: " + string.Join(", ", font.Presets.Select(p => $"{p.PatchNumber}:{p.Name}")));
    var left = new float[44100 / 2];
    var right = new float[44100 / 2];
    foreach (var preset in font.Presets.OrderBy(p => p.PatchNumber))
    {
        var silent = new List<int>();
        synth.ProcessMidiMessage(0, 0xC0, preset.PatchNumber, 0);
        for (int key = 36; key <= 96; key += 3)
        {
            synth.NoteOffAll(true);
            synth.NoteOn(0, key, 100);
            synth.Render(left, right);
            if (left.Max(Math.Abs) < 0.01f)
                silent.Add(key);
        }
        Console.WriteLine($"    {preset.Name}: silent keys {(silent.Count == 0 ? "none" : string.Join(" ", silent))}");
    }
}

static class Pitch
{
    /// <summary>
    /// The pitch a sample sounds (fractional MIDI), from a normalized autocorrelation over a steady stretch; the
    /// shortest lag that's nearly as good as the best one, so a doubled period doesn't read as an octave lower.
    /// </summary>
    public static double? MeasuredMidi(short[] data, int rate)
    {
        int window = 4096;
        int start = Math.Min(data.Length - window, rate / 5);
        if (start < 0)
            return null;
        var x = data.AsSpan(start, window).ToArray().Select(v => (double)v).ToArray();
        int minLag = rate / 2500, maxLag = Math.Min(window / 2, rate / 30);
        var corr = new double[maxLag + 1];
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            double sum = 0, e1 = 0, e2 = 0;
            for (int i = 0; i + lag < window; i++)
            {
                sum += x[i] * x[i + lag];
                e1 += x[i] * x[i];
                e2 += x[i + lag] * x[i + lag];
            }
            corr[lag] = sum / Math.Sqrt(e1 * e2 + 1e-9);
        }
        double best = corr.Skip(minLag).Max();
        if (best < 0.5)
            return null;
        int chosen = Enumerable.Range(minLag, maxLag - minLag + 1)
            .First(l => corr[l] >= best * 0.9 && (l == minLag || corr[l] >= corr[l - 1]) && (l == maxLag || corr[l] >= corr[l + 1]));
        double hz = (double)rate / chosen;
        return 69 + 12 * Math.Log2(hz / 440);
    }
}

static class Wav
{
    /// <summary>PCM 16 / 24 / 32-bit or float WAV, any channels, as mono floats.</summary>
    public static (float[] Data, int Rate) ReadMono(string path)
    {
        using var r = new BinaryReader(File.OpenRead(path));
        r.ReadBytes(12);
        int channels = 1, rate = 44100, bits = 16, format = 1;
        while (r.BaseStream.Position < r.BaseStream.Length - 8)
        {
            string id = new(r.ReadChars(4));
            int size = r.ReadInt32();
            if (id == "fmt ")
            {
                format = r.ReadInt16();
                channels = r.ReadInt16();
                rate = r.ReadInt32();
                r.ReadInt32();
                r.ReadInt16();
                bits = r.ReadInt16();
                r.ReadBytes(size - 16);
            }
            else if (id == "data")
            {
                var bytes = r.ReadBytes(size);
                int bytesPer = bits / 8, frames = bytes.Length / (bytesPer * channels);
                var data = new float[frames];
                for (int f = 0; f < frames; f++)
                {
                    float sum = 0;
                    for (int c = 0; c < channels; c++)
                    {
                        int o = (f * channels + c) * bytesPer;
                        sum += bits switch
                        {
                            16 => BitConverter.ToInt16(bytes, o) / 32768f,
                            24 => ((bytes[o] | bytes[o + 1] << 8 | (sbyte)bytes[o + 2] << 16)) / 8388608f,
                            32 when format == 3 => BitConverter.ToSingle(bytes, o),
                            32 => BitConverter.ToInt32(bytes, o) / 2147483648f,
                            _ => 0,
                        };
                    }
                    data[f] = sum / channels;
                }
                return (data, rate);
            }
            else
                r.ReadBytes(size + (size & 1));
        }
        return ([], rate);
    }
}
