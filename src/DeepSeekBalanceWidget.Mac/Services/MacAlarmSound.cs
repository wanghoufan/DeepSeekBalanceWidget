using System.Diagnostics;
using System.Text;

namespace DeepSeekBalanceWidget.Services;

/// <summary>
/// macOS 循环警报音。音频在运行时合成为 8kHz、16-bit、单声道 WAV，
/// 再交给系统自带的 afplay 播放，因此不需要随应用发布外部音频资源。
/// </summary>
public static class MacAlarmSound
{
    private const int SampleRate = 8000;
    private static readonly object Gate = new();
    private static Process? _process;
    private static string? _tempPath;
    private static string _currentStyle = string.Empty;
    private static bool _playing;

    public static void Play(string? style)
    {
        string normalized = NormalizeStyle(style);
        lock (Gate)
        {
            try
            {
                if (_playing && string.Equals(_currentStyle, normalized, StringComparison.Ordinal)
                    && _process is { HasExited: false })
                    return;

                StopLocked(deleteFile: true);
                _tempPath = Path.Combine(Path.GetTempPath(), $"deepseek-balance-alarm-{Guid.NewGuid():N}.wav");
                File.WriteAllBytes(_tempPath, BuildAlarmWav(normalized));
                _currentStyle = normalized;
                _playing = true;
                StartProcessLocked();
            }
            catch
            {
                // 没有 afplay 或没有可用音频设备时，弹窗仍应正常工作。
                _playing = false;
                StopLocked(deleteFile: true);
            }
        }
    }

    public static void Stop()
    {
        lock (Gate)
        {
            try { StopLocked(deleteFile: true); } catch { }
        }
    }

    private static void StartProcessLocked()
    {
        if (!_playing || string.IsNullOrEmpty(_tempPath)) return;

        var info = new ProcessStartInfo("/usr/bin/afplay")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add(_tempPath);
        var process = Process.Start(info);
        if (process is null)
        {
            _playing = false;
            return;
        }

        process.EnableRaisingEvents = true;
        process.Exited += Process_Exited;
        _process = process;
    }

    private static void Process_Exited(object? sender, EventArgs e)
    {
        lock (Gate)
        {
            if (!ReferenceEquals(sender, _process) || !_playing) return;

            _process?.Dispose();
            _process = null;
            try { StartProcessLocked(); }
            catch { _playing = false; }
        }
    }

    private static void StopLocked(bool deleteFile)
    {
        _playing = false;
        var process = _process;
        _process = null;
        if (process is not null)
        {
            try
            {
                process.Exited -= Process_Exited;
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                process.WaitForExit(500);
            }
            catch { }
            finally { process.Dispose(); }
        }

        if (deleteFile && _tempPath is not null)
        {
            try { File.Delete(_tempPath); } catch { }
            _tempPath = null;
        }
    }

    private static string NormalizeStyle(string? style) => style switch
    {
        "Beep" or "Ascending" or "Descending" or "Chime" or "Bell" or "DingDong"
            or "Rapid" or "SlowPulse" or "Soft" or "Standard" or "Urgent" => style,
        _ => "Standard"
    };

    private static byte[] BuildAlarmWav(string style)
    {
        var pcm = style switch
        {
            "Beep" => BuildBeep(),
            "Ascending" => BuildAscending(),
            "Descending" => BuildDescending(),
            "Chime" => BuildChime(),
            "Bell" => BuildBell(),
            "DingDong" => BuildDingDong(),
            "Rapid" => BuildRapid(),
            "SlowPulse" => BuildSlowPulse(),
            "Soft" => BuildSoft(),
            "Urgent" => BuildUrgent(),
            _ => BuildStandard()
        };

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            int dataLength = pcm.Length;
            writer.Write("RIFF"u8);
            writer.Write(36 + dataLength);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(dataLength);
            writer.Write(pcm);
        }
        return stream.ToArray();
    }

    private static byte[] BuildBeep()
    {
        int toneSamples = SampleRate * 2 / 10;
        int silenceSamples = SampleRate * 4 / 10;
        var pcm = new byte[(toneSamples + silenceSamples) * 2];
        WriteTone(pcm, 0, toneSamples, SampleRate / 100, 880, 24000);
        return pcm;
    }

    private static byte[] BuildAscending() => BuildToneSequence(
        new[] { 440d, 554d, 659d, 784d }, SampleRate * 18 / 100, SampleRate * 2 / 100, 22000);

    private static byte[] BuildDescending() => BuildToneSequence(
        new[] { 784d, 659d, 554d, 440d }, SampleRate * 18 / 100, SampleRate * 2 / 100, 22000);

    private static byte[] BuildChime() => BuildToneSequence(
        new[] { 1047d, 1319d, 1568d }, SampleRate * 22 / 100, SampleRate * 3 / 100, 19000);

    private static byte[] BuildBell()
    {
        int total = SampleRate * 12 / 10;
        int fade = SampleRate / 100;
        var pcm = new byte[total * 2];
        for (int i = 0; i < total; i++)
        {
            double t = i / (double)SampleRate;
            double decay = Math.Exp(-2.8 * t);
            double sample = Math.Sin(2 * Math.PI * 440 * t)
                + 0.38 * Math.Sin(2 * Math.PI * 880 * t)
                + 0.18 * Math.Sin(2 * Math.PI * 1320 * t)
                + 0.08 * Math.Sin(2 * Math.PI * 1760 * t);
            double env = decay;
            if (i < fade) env *= i / (double)fade;
            else if (i >= total - fade) env *= (total - i) / (double)fade;
            WriteSample(pcm, i, sample * 15000 * env);
        }
        return pcm;
    }

    private static byte[] BuildDingDong() => BuildToneSequence(
        new[] { 880d, 660d }, SampleRate * 4 / 10, SampleRate * 8 / 100, 24000);

    private static byte[] BuildRapid() => BuildPulseTrain(1000, SampleRate * 12 / 100,
        SampleRate * 8 / 100, 6, 23000);

    private static byte[] BuildSlowPulse() => BuildPulseTrain(520, SampleRate * 35 / 100,
        SampleRate * 4 / 10, 3, 22000);

    private static byte[] BuildSoft()
    {
        int total = SampleRate * 8 / 5;
        int fade = SampleRate / 50;
        var pcm = new byte[total * 2];
        for (int i = 0; i < total; i++)
        {
            double breath = 0.55 + 0.45 * Math.Sin(2 * Math.PI * 0.4 * i / SampleRate);
            double env = 1.0;
            if (i < fade) env = i / (double)fade;
            else if (i >= total - fade) env = (total - i) / (double)fade;
            WriteSample(pcm, i, Math.Sin(2 * Math.PI * 440 * i / SampleRate) * 18000 * breath * env);
        }
        return pcm;
    }

    private static byte[] BuildStandard()
    {
        int toneSamples = SampleRate * 4 / 5;
        int fade = SampleRate / 100;
        var pcm = new byte[toneSamples * 4];
        WriteTone(pcm, 0, toneSamples, fade, 880, 26000);
        WriteTone(pcm, toneSamples, toneSamples, fade, 660, 26000);
        return pcm;
    }

    private static byte[] BuildUrgent()
    {
        int pulse = SampleRate * 3 / 10;
        int silence = SampleRate * 3 / 100;
        int fade = SampleRate / 100;
        int total = pulse * 4 + silence * 3;
        var pcm = new byte[total * 2];
        int position = 0;
        for (int segment = 0; segment < 4; segment++)
        {
            for (int i = 0; i < pulse; i++)
            {
                double env = i < fade ? i / (double)fade
                    : i >= pulse - fade ? (pulse - i) / (double)fade : 1.0;
                WriteSample(pcm, position + i,
                    Math.Sin(2 * Math.PI * 1000 * (position + i) / SampleRate) * 26000 * env);
            }
            position += pulse;
            if (segment < 3) position += silence;
        }
        return pcm;
    }

    private static byte[] BuildToneSequence(IReadOnlyList<double> frequencies,
        int toneSamples, int silenceSamples, double amplitude)
    {
        int total = frequencies.Count * toneSamples + Math.Max(0, frequencies.Count - 1) * silenceSamples;
        var pcm = new byte[total * 2];
        int position = 0;
        int fade = Math.Min(SampleRate / 100, toneSamples / 4);
        foreach (double frequency in frequencies)
        {
            WriteTone(pcm, position, toneSamples, fade, frequency, amplitude);
            position += toneSamples + silenceSamples;
        }
        return pcm;
    }

    private static byte[] BuildPulseTrain(double frequency, int pulseSamples,
        int silenceSamples, int count, double amplitude)
    {
        int total = count * (pulseSamples + silenceSamples);
        var pcm = new byte[total * 2];
        int position = 0;
        int fade = Math.Min(SampleRate / 100, pulseSamples / 4);
        for (int i = 0; i < count; i++)
        {
            WriteTone(pcm, position, pulseSamples, fade, frequency, amplitude);
            position += pulseSamples + silenceSamples;
        }
        return pcm;
    }

    private static void WriteTone(byte[] pcm, int offsetSamples, int count, int fade,
        double frequency, double amplitude)
    {
        for (int i = 0; i < count; i++)
        {
            double env = i < fade ? i / (double)fade
                : i >= count - fade ? (count - i) / (double)fade : 1.0;
            WriteSample(pcm, offsetSamples + i,
                Math.Sin(2 * Math.PI * frequency * i / SampleRate) * amplitude * env);
        }
    }

    private static void WriteSample(byte[] pcm, int sampleIndex, double value)
    {
        short sample = (short)Math.Clamp(value, (double)short.MinValue, (double)short.MaxValue);
        int index = sampleIndex * 2;
        pcm[index] = (byte)(sample & 0xFF);
        pcm[index + 1] = (byte)((sample >> 8) & 0xFF);
    }
}
