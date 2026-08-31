using System;
using System.Collections.Generic;
using System.IO;
using System.Media;

namespace DeepSeekBalanceWidget.Services;

/// <summary>
/// 程序化合成的循环警报音。提供 11 种无需外部文件的风格：
/// - Beep 短鸣：单次 880Hz 短音；
/// - Ascending 递升：440/554/659/784Hz 逐级升调；
/// - Descending 递降：784/659/554/440Hz 逐级降调；
/// - Chime 清脆：高音三连奏；
/// - Bell 铃声：带谐波和衰减的浑厚单音；
/// - DingDong 叮咚：880/660Hz 两段式提示音；
/// - Rapid 快速：短促高频脉冲；
/// - SlowPulse 慢脉冲：低频长脉冲；
/// - Soft 柔和：单音 440Hz 缓起伏，不刺耳；
/// - Standard 标准：880/660Hz 双音交替（原默认，节奏偏紧）；
/// - Urgent 急促：短促的 1000Hz 脉冲，节奏最快，适合强提醒。
/// 不依赖任何外部音频文件：运行时生成 8kHz 16bit PCM WAV 写入内存流，
/// 用 SoundPlayer.PlayLooping 循环播放；Stop 后即静音。
/// </summary>
public static class AlarmSound
{
    private const int SampleRate = 8000;
    private static MemoryStream? _stream;
    private static readonly SoundPlayer Player = new();
    private static string _currentStyle = "";

    public static void Play(string style)
    {
        if (string.IsNullOrEmpty(style)) style = "Standard";
        try
        {
            if (_currentStyle != style || _stream is null)
            {
                _stream = BuildAlarmWav(style);
                _currentStyle = style;
            }
            _stream.Position = 0;
            Player.Stream = _stream;
            Player.PlayLooping();
        }
        catch
        {
            // 无声环境（无声卡/被策略禁用）时静默降级，只保留弹窗
        }
    }

    public static void Stop()
    {
        try { Player.Stop(); } catch { }
    }

    /// <summary>生成对应风格的循环 WAV（已做淡入淡出，可无缝衔接）。</summary>
    private static MemoryStream BuildAlarmWav(string style)
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
        var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            int dataLen = pcm.Length;
            int byteRate = SampleRate * 2;
            writer.Write("RIFF"u8);
            writer.Write(36 + dataLen);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(byteRate);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(dataLen);
            writer.Write(pcm);
        }
        ms.Position = 0;
        return ms;
    }

    /// <summary>短鸣：一次干净的 880Hz 提示音。</summary>
    private static byte[] BuildBeep()
    {
        int toneSamples = SampleRate * 2 / 10; // 0.2s
        int silenceSamples = SampleRate * 4 / 10; // 0.4s
        var pcm = new byte[(toneSamples + silenceSamples) * 2];
        WriteTone(pcm, 0, toneSamples, SampleRate / 100, 880, 24000);
        return pcm;
    }

    /// <summary>递升：四个逐级升高的短音。</summary>
    private static byte[] BuildAscending()
        => BuildToneSequence(new[] { 440d, 554d, 659d, 784d },
            toneSamples: SampleRate * 18 / 100,
            silenceSamples: SampleRate * 2 / 100,
            amplitude: 22000);

    /// <summary>递降：四个逐级降低的短音。</summary>
    private static byte[] BuildDescending()
        => BuildToneSequence(new[] { 784d, 659d, 554d, 440d },
            toneSamples: SampleRate * 18 / 100,
            silenceSamples: SampleRate * 2 / 100,
            amplitude: 22000);

    /// <summary>清脆：三段高音连奏，适合不刺耳的提醒。</summary>
    private static byte[] BuildChime()
        => BuildToneSequence(new[] { 1047d, 1319d, 1568d },
            toneSamples: SampleRate * 22 / 100,
            silenceSamples: SampleRate * 3 / 100,
            amplitude: 19000);

    /// <summary>铃声：基频叠加谐波，并随时间自然衰减。</summary>
    private static byte[] BuildBell()
    {
        int total = SampleRate * 12 / 10; // 1.2s
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

    /// <summary>叮咚：两个音高和时值不同的连续提示音。</summary>
    private static byte[] BuildDingDong()
        => BuildToneSequence(new[] { 880d, 660d },
            toneSamples: SampleRate * 4 / 10,
            silenceSamples: SampleRate * 8 / 100,
            amplitude: 24000);

    /// <summary>快速：六段短脉冲，节奏明显但比 Urgent 更有间隔。</summary>
    private static byte[] BuildRapid()
        => BuildPulseTrain(frequency: 1000,
            pulseSamples: SampleRate * 12 / 100,
            silenceSamples: SampleRate * 8 / 100,
            count: 6,
            amplitude: 23000);

    /// <summary>慢脉冲：三段较长低频脉冲，段间留出明显空隙。</summary>
    private static byte[] BuildSlowPulse()
        => BuildPulseTrain(frequency: 520,
            pulseSamples: SampleRate * 35 / 100,
            silenceSamples: SampleRate * 4 / 10,
            count: 3,
            amplitude: 22000);

    private static byte[] BuildToneSequence(
        IReadOnlyList<double> frequencies,
        int toneSamples,
        int silenceSamples,
        double amplitude)
    {
        int total = frequencies.Count * toneSamples
            + Math.Max(0, frequencies.Count - 1) * silenceSamples;
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

    private static byte[] BuildPulseTrain(
        double frequency,
        int pulseSamples,
        int silenceSamples,
        int count,
        double amplitude)
    {
        // 保留最后一段静音，让循环边界也有清晰的节奏间隔。
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

    /// <summary>柔和：单音 440Hz，整体缓慢起伏，音量更低。</summary>
    private static byte[] BuildSoft()
    {
        // 1.6s 一个循环
        int total = SampleRate * 8 / 5;
        int fade = SampleRate / 50; // 20ms 淡入淡出
        var pcm = new byte[total * 2];
        for (int i = 0; i < total; i++)
        {
            // 整体包络呈正弦慢呼吸：约 0.4Hz
            double breath = 0.55 + 0.45 * Math.Sin(2 * Math.PI * 0.4 * i / SampleRate);
            double env = 1.0;
            if (i < fade) env = i / (double)fade;
            else if (i >= total - fade) env = (total - i) / (double)fade;
            short value = (short)(Math.Sin(2 * Math.PI * 440 * i / SampleRate) * 18000 * breath * env);
            pcm[i * 2] = (byte)(value & 0xFF);
            pcm[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }
        return pcm;
    }

    /// <summary>标准：880/660Hz 双音交替（原默认）。</summary>
    private static byte[] BuildStandard()
    {
        int toneSamples = SampleRate * 4 / 5; // 0.8s 一段
        int fade = SampleRate / 100; // 10ms
        var pcm = new byte[(toneSamples * 2) * 2];
        WriteTone(pcm, 0, toneSamples, fade, 880, 26000);
        WriteTone(pcm, toneSamples, toneSamples, fade, 660, 26000);
        return pcm;
    }

    /// <summary>急促：4 段短促 1000Hz 脉冲，每段 0.3s 含 30ms 静音间隔，节奏最快。</summary>
    private static byte[] BuildUrgent()
    {
        int pulse = SampleRate * 3 / 10; // 0.3s 一段
        int silence = SampleRate * 3 / 100; // 30ms 静音
        int fade = SampleRate / 100; // 10ms
        int total = pulse * 4 + silence * 3;
        var pcm = new byte[total * 2];
        int pos = 0;
        for (int s = 0; s < 4; s++)
        {
            for (int i = 0; i < pulse; i++)
            {
                double env = 1.0;
                if (i < fade) env = i / (double)fade;
                else if (i >= pulse - fade) env = (pulse - i) / (double)fade;
                short value = (short)(Math.Sin(2 * Math.PI * 1000 * (pos + i) / SampleRate) * 26000 * env);
                int idx = (pos + i) * 2;
                pcm[idx] = (byte)(value & 0xFF);
                pcm[idx + 1] = (byte)((value >> 8) & 0xFF);
            }
            pos += pulse;
            if (s < 3) pos += silence; // 段间静音
        }
        return pcm;
    }

    private static void WriteTone(byte[] pcm, int offsetSamples, int count, int fade, double freq, double amplitude)
    {
        for (int i = 0; i < count; i++)
        {
            double env = 1.0;
            if (i < fade) env = i / (double)fade;
            else if (i >= count - fade) env = (count - i) / (double)fade;
            WriteSample(pcm, offsetSamples + i,
                Math.Sin(2 * Math.PI * freq * i / SampleRate) * amplitude * env);
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
