using System.Text;

/// <summary>One recorded note: mono 16-bit samples at a rate, the key it was played at, an optional loop.</summary>
sealed record Sample(string Name, short[] Data, int Rate, int RootKey, int LoopStart = -1, int LoopEnd = -1);

/// <summary>A preset made of samples, each covering the keys up to the next one's (the usual multisample split).</summary>
sealed record Preset(string Name, int Program, List<Sample> Samples, double ReleaseSeconds, bool Loop);

/// <summary>Writes a minimal SoundFont 2.01: one instrument per preset, one zone per sample, key ranges split between roots.</summary>
static class Sf2Writer
{
    public static void Write(string path, string bankName, string comment, IReadOnlyList<Preset> presets)
    {
        var samples = presets.SelectMany(p => p.Samples).ToList();

        // sdta: all samples back to back, each followed by 46 zero points (the spec's guard).
        var smpl = new MemoryStream();
        var starts = new List<int>();
        int position = 0;
        foreach (var s in samples)
        {
            starts.Add(position);
            foreach (short v in s.Data)
                smpl.Write(BitConverter.GetBytes(v));
            smpl.Write(new byte[46 * 2]);
            position += s.Data.Length + 46;
        }

        // pdta records.
        var phdr = new MemoryStream(); var pbag = new MemoryStream(); var pmod = new MemoryStream(); var pgen = new MemoryStream();
        var inst = new MemoryStream(); var ibag = new MemoryStream(); var imod = new MemoryStream(); var igen = new MemoryStream();
        var shdr = new MemoryStream();
        int pbagIndex = 0, pgenIndex = 0, ibagIndex = 0, igenIndex = 0, sampleIndex = 0;

        for (int p = 0; p < presets.Count; p++)
        {
            var preset = presets[p];
            // Preset header -> one zone -> generator "instrument p".
            Name(phdr, preset.Name); U16(phdr, preset.Program); U16(phdr, 0); U16(phdr, pbagIndex); U32(phdr, 0); U32(phdr, 0); U32(phdr, 0);
            U16(pbag, pgenIndex); U16(pbag, 0); pbagIndex++;
            Gen(pgen, 41, (ushort)p); pgenIndex++;

            Name(inst, preset.Name); U16(inst, ibagIndex);
            var ordered = preset.Samples.OrderBy(s => s.RootKey).ToList();
            for (int z = 0; z < ordered.Count; z++)
            {
                var s = ordered[z];
                int low = z == 0 ? 0 : (ordered[z - 1].RootKey + s.RootKey) / 2 + 1;
                int high = z == ordered.Count - 1 ? 127 : (s.RootKey + ordered[z + 1].RootKey) / 2;
                U16(ibag, igenIndex); U16(ibag, 0); ibagIndex++;
                Gen(igen, 43, (ushort)(low | high << 8)); igenIndex++;                            // keyRange (first)
                Gen(igen, 38, (ushort)(short)Math.Round(1200 * Math.Log2(Math.Max(0.01, preset.ReleaseSeconds)))); igenIndex++; // releaseVolEnv
                if (preset.Loop && s.LoopStart >= 0)
                {
                    Gen(igen, 54, 1); igenIndex++;                                                   // sampleModes: loop
                }
                Gen(igen, 58, (ushort)s.RootKey); igenIndex++;                                       // overridingRootKey
                int globalIndex = samples.IndexOf(s);
                Gen(igen, 53, (ushort)globalIndex); igenIndex++;                                     // sampleID (last)
            }
        }
        // Terminal records.
        Name(phdr, "EOP"); U16(phdr, 0); U16(phdr, 0); U16(phdr, pbagIndex); U32(phdr, 0); U32(phdr, 0); U32(phdr, 0);
        U16(pbag, pgenIndex); U16(pbag, 0);
        pmod.Write(new byte[10]);
        U16(pgen, 0); U16(pgen, 0);
        Name(inst, "EOI"); U16(inst, ibagIndex);
        U16(ibag, igenIndex); U16(ibag, 0);
        imod.Write(new byte[10]);
        U16(igen, 0); U16(igen, 0);

        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            int start = starts[i];
            Name(shdr, s.Name);
            U32(shdr, (uint)start);
            U32(shdr, (uint)(start + s.Data.Length));
            U32(shdr, (uint)(start + (s.LoopStart >= 0 ? s.LoopStart : 0)));
            U32(shdr, (uint)(start + (s.LoopEnd >= 0 ? s.LoopEnd : s.Data.Length - 1)));
            U32(shdr, (uint)s.Rate);
            shdr.WriteByte((byte)s.RootKey);
            shdr.WriteByte(0);   // pitch correction
            U16(shdr, 0);        // sample link
            U16(shdr, 1);        // mono
            sampleIndex++;
        }
        Name(shdr, "EOS"); shdr.Write(new byte[46 - 20]);

        using var file = File.Create(path);
        var info = List("INFO",
            Chunk("ifil", [2, 0, 1, 0]),
            Chunk("isng", Z("EMU8000")),
            Chunk("INAM", Z(bankName)),
            Chunk("ICMT", Z(comment)));
        var sdta = List("sdta", Chunk("smpl", smpl.ToArray()));
        var pdta = List("pdta",
            Chunk("phdr", phdr.ToArray()), Chunk("pbag", pbag.ToArray()), Chunk("pmod", pmod.ToArray()), Chunk("pgen", pgen.ToArray()),
            Chunk("inst", inst.ToArray()), Chunk("ibag", ibag.ToArray()), Chunk("imod", imod.ToArray()), Chunk("igen", igen.ToArray()),
            Chunk("shdr", shdr.ToArray()));
        var body = new MemoryStream();
        body.Write(Encoding.ASCII.GetBytes("sfbk"));
        body.Write(info); body.Write(sdta); body.Write(pdta);
        file.Write(Encoding.ASCII.GetBytes("RIFF"));
        file.Write(BitConverter.GetBytes((uint)body.Length));
        body.Position = 0;
        body.CopyTo(file);
    }

    static void Name(Stream s, string name)
    {
        var bytes = new byte[20];
        Encoding.ASCII.GetBytes(name.Length > 19 ? name[..19] : name).CopyTo(bytes, 0);
        s.Write(bytes);
    }

    static void U16(Stream s, int v) => s.Write(BitConverter.GetBytes((ushort)v));
    static void U32(Stream s, uint v) => s.Write(BitConverter.GetBytes(v));
    static void Gen(Stream s, ushort op, ushort amount) { U16(s, op); U16(s, amount); }

    static byte[] Z(string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text + "\0");
        return bytes.Length % 2 == 0 ? bytes : [.. bytes, 0];
    }

    static byte[] Chunk(string id, byte[] data)
    {
        var m = new MemoryStream();
        m.Write(Encoding.ASCII.GetBytes(id));
        m.Write(BitConverter.GetBytes((uint)data.Length));
        m.Write(data);
        if (data.Length % 2 == 1)
            m.WriteByte(0);
        return m.ToArray();
    }

    static byte[] List(string type, params byte[][] chunks)
    {
        var inner = new MemoryStream();
        inner.Write(Encoding.ASCII.GetBytes(type));
        foreach (var c in chunks)
            inner.Write(c);
        return Chunk("LIST", inner.ToArray());
    }
}
