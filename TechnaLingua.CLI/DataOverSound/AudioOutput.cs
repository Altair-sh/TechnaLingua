using ggwave.net;
using ggwave.net.Native;
using NAudio.Sdl2;
using NAudio.Sdl2.Interop;
using NAudio.Sdl2.Structures;
using NAudio.Wave;

namespace TechnaLingua.CLI.DataOverSound;

public class AudioOutput : IDisposable
{
    public readonly WaveOutSdlCapabilities DeviceCapabilities;
    public readonly int SamplesPerFrame;
    public int VolumePercent;
    
    private readonly WaveOutSdl _outputDevice;
    private readonly WaveFormat _waveFormat;
    private readonly GGWaveInstance _ggWave;

    public AudioOutput(WaveOutSdl outputDevice, WaveOutSdlCapabilities deviceCapabilities, 
        int samplesPerFrame = 256, int volumePercent = 30)
    {
        _outputDevice = outputDevice;
        DeviceCapabilities = deviceCapabilities;
        SamplesPerFrame = samplesPerFrame;
        VolumePercent = volumePercent;

        var ggWaveParams = GGWaveStatic.getDefaultParameters();
        ggWaveParams.sampleRate = deviceCapabilities.Frequency;
        ggWaveParams.sampleRateOut = deviceCapabilities.Frequency;
        (ggWaveParams.sampleFormatOut, _waveFormat) = deviceCapabilities.Format switch
        {
            SDL.AUDIO_U16 => (GGWaveSampleFormat.FORMAT_U16, 
                new WaveFormat(deviceCapabilities.Frequency, 16, 1)),
            SDL.AUDIO_S16 => (GGWaveSampleFormat.FORMAT_I16, 
                new WaveFormat(deviceCapabilities.Frequency, 16, 1)),
            SDL.AUDIO_F32 => (GGWaveSampleFormat.FORMAT_F32, 
                WaveFormat.CreateIeeeFloatWaveFormat(deviceCapabilities.Frequency, 1)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(deviceCapabilities.Format), 
                deviceCapabilities.Format.ToString())
        };
        ggWaveParams.samplesPerFrame = samplesPerFrame;
        Console.WriteLine($"GGWave params: {ggWaveParams}");
        _ggWave = new GGWaveInstance(ggWaveParams);
    }
    
    public async Task EncodeAndPlay(Stream dataReadStream)
    {
        var playbackStoppedCts = new CancellationTokenSource();
        _outputDevice.PlaybackStopped += (s, e) =>
        {
            if (e.Exception is not null)
                throw new AggregateException(e.Exception);
            playbackStoppedCts.Cancel();
        };

        var waveStream = new ProducerConsumerStream();

        var waveWriter = waveStream.GetWriteStream();
        Console.WriteLine("Encoding");
        _ggWave.EncodeStream(dataReadStream, waveWriter, 
            GGWaveProtocolId.AUDIBLE_NORMAL, VolumePercent);
        Console.WriteLine("Done");

        var waveReader = new RawSourceWaveStream(waveStream.CreateReadStream(), _waveFormat);
        Console.WriteLine("Init out device");
        _outputDevice.Init(waveReader);
        Console.WriteLine("Playback started");
        _outputDevice.Play();
        
        Console.WriteLine("Waiting for playback stop");
        while(!playbackStoppedCts.Token.IsCancellationRequested)
        {
            // ReSharper disable once MethodSupportsCancellation
            await Task.Delay(100);
        }
        // wait until device actually stops playing audio
        // ReSharper disable once MethodSupportsCancellation
        await Task.Delay(500);
        Console.WriteLine("Playback stopped");
    }

    public void Dispose()
    {
        _outputDevice.Dispose();
        _ggWave.Dispose();
    }
}