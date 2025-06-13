using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
    public GGWaveProtocolId GGWaveProtocol = GGWaveProtocolId.AUDIBLE_FASTEST;
    public int VolumePercent = 30;
    
    private readonly WaveOutSdl _outputDevice;
    private readonly WaveFormat _waveFormat;
    private readonly GGWaveInstance _ggWave;
    private CancellationTokenSource _stop_cts = new();
    
    public PlaybackState PlaybackState => _outputDevice.PlaybackState;
    
    
    public AudioOutput(WaveOutSdl outputDevice, WaveOutSdlCapabilities deviceCapabilities, int samplesPerFrame = 1024)
    {
        _outputDevice = outputDevice;
        DeviceCapabilities = deviceCapabilities;
        SamplesPerFrame = samplesPerFrame;
        
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
    
    public void EncodeAndPlay(Stream dataReadStream)
    {
        if(PlaybackState != PlaybackState.Stopped)
            throw new Exception("Audio is already playing.");
        
        _outputDevice.PlaybackStopped += (s, e) =>
        {
            if (e.Exception is not null)
                throw new AggregateException(e.Exception);
            
            // wait until device actually stops playing audio
            // Thread.Sleep(500);
            Console.WriteLine("Playback stopped");
        };

        var wavePipe = new System.IO.Pipelines.Pipe();
        
        var waveWriter = wavePipe.Writer.AsStream();
        var encoderThread = new Thread(() =>
        {
            Console.WriteLine("Encoding thread start");
            _ggWave.EncodeStream(
                dataReadStream,
                waveWriter,
                GGWaveProtocol,
                VolumePercent,
                waitForMoreInput: true, 
                ct: _stop_cts.Token);
            Console.WriteLine("Encoding thread end");
        });
        encoderThread.Start();

        var waveReader = new RawSourceWaveStream(wavePipe.Reader.AsStream(), _waveFormat);
        Console.WriteLine("Init out device");
        _outputDevice.Init(waveReader);
        Console.WriteLine("Playback started");
        _outputDevice.Play();
    }

    public void Stop()
    {
        if(PlaybackState == PlaybackState.Stopped)
            return;
        _stop_cts.Cancel();
        _stop_cts = new CancellationTokenSource();
        _outputDevice.Stop();
    }

    public void Dispose()
    {
        _outputDevice.Dispose();
        _ggWave.Dispose();
    }
}