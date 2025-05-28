using System.Text;
using ggwave.net;
using ggwave.net.Native;
using NAudio.Sdl2;
using NAudio.Sdl2.Interop;
using NAudio.Sdl2.Structures;
using NAudio.Wave;

namespace TechnaLingua.CLI;

class Program
{
    static (WaveOutSdl, WaveOutSdlCapabilities)  GetOutputDevice()
    {
        var audioOutputDevices = WaveOutSdl.GetCapabilitiesList();
        foreach (WaveOutSdlCapabilities device in audioOutputDevices)
        {
            Console.WriteLine(device.ToString(",\n\t"));
        }
        
        // Console.Write("Select output device number: ");
        // string? deviceIdStr = Console.ReadLine();
        int deviceId = 0;
        // if(!string.IsNullOrWhiteSpace(deviceIdStr))
        //     deviceId = Convert.ToInt32(deviceIdStr);
        var deviceCapabilities = audioOutputDevices[deviceId];
        Console.WriteLine($"Selected output device: {deviceCapabilities.DeviceName}");
        if(!deviceCapabilities.IsAudioCapabilitiesValid)
            throw new Exception($"Audio capabilities are not valid ({deviceCapabilities.ToString(", ")} )");
        var outputDevice = new WaveOutSdl
        {
            DeviceId = deviceId,
            DesiredLatency = 100,
            Volume = 1.0f
        };
        return (outputDevice, deviceCapabilities);
    }

    static string GetTextInput() => "Prived";

    static void Main(string[] args)
    {
        SDL.SDL_GetVersion(out var sdlVersion);
        Console.WriteLine($"SDL Version: {sdlVersion.major}.{sdlVersion.minor}.{sdlVersion.patch}");
        
        var text = GetTextInput();
        var utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier:false, throwOnInvalidBytes: true);
        var dataBytes = utf8WithoutBom.GetBytes(text);
        // using var inputDataStream = new ProducerConsumerStream();
        // inputDataStream.Write(dataBytes, 0, dataBytes.Length);
        
        var (outputDevice, outputDeviceCapabilities) = GetOutputDevice();

        var ggWaveParams = GGWaveStatic.getDefaultParameters();
        ggWaveParams.sampleRate = outputDeviceCapabilities.Frequency;
        (ggWaveParams.sampleFormatOut, WaveFormat waveFormat) = outputDeviceCapabilities.Format switch
        {
            SDL.AUDIO_U16 => (GGWaveSampleFormat.FORMAT_U16, 
                new WaveFormat(outputDeviceCapabilities.Frequency, 16, 1)),
            SDL.AUDIO_S16 => (GGWaveSampleFormat.FORMAT_I16, 
                new WaveFormat(outputDeviceCapabilities.Frequency, 16, 1)),
            SDL.AUDIO_F32 => (GGWaveSampleFormat.FORMAT_F32, 
                WaveFormat.CreateIeeeFloatWaveFormat(outputDeviceCapabilities.Frequency, 1)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(outputDeviceCapabilities.Format), 
                outputDeviceCapabilities.Format.ToString())
        };
        Console.WriteLine($"GGWave params: {ggWaveParams}");
        using var ggWave = new GGWaveInstance(ggWaveParams);

        // using var pcmOutStream = new ProducerConsumerStream();
        // var waveBuffer = new BufferedWaveProvider(waveFormat)
        // {
        //     DiscardOnBufferOverflow = true
        // };

        var playbackStoppedCts = new CancellationTokenSource();
        outputDevice.PlaybackStopped += (s, e) =>
        {
            Console.WriteLine($"Playback stopped ({e.Exception})");
            playbackStoppedCts.Cancel();
        };

        byte[] dataPcm = ggWave.Encode(
            dataBytes,
            GGWaveProtocolId.AUDIBLE_NORMAL,
            50);
        var waveStream = new ProducerConsumerStream();
        using (var waveFileWriter = new WaveFileWriter(waveStream.GetWriteStream(), waveFormat))
        {
            waveFileWriter.Write(dataPcm, 0, dataPcm.Length);
        }
        
        using (var waveFileReader = new WaveFileReader(waveStream.CreateReadStream()))
        {
            outputDevice.Init(waveFileReader);
            outputDevice.Play();

            // Console.WriteLine("WritePcmDataToBufferAsync");
            // WritePcmDataToBufferAsync(pcmOutStream, waveBuffer);
            // Console.WriteLine("Encoding");
            // ggWave.EncodeStream(inputDataStream, pcmOutStream, 
            //     GGWaveProtocolId.AUDIBLE_NORMAL, 50);
            // Console.WriteLine("Done");

            playbackStoppedCts.Token.WaitHandle.WaitOne();
            Thread.Sleep(2000);
        }
    }
    
    static async void WritePcmDataToBufferAsync(Stream pcmOutStream, BufferedWaveProvider waveBuffer)
    {
        try
        {
            byte[] buffer = new byte[4096];
            int bytesRead;

            while ((bytesRead = await pcmOutStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                Console.WriteLine($"Add sample {bytesRead} bytes");
                waveBuffer.AddSamples(buffer, 0, bytesRead);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
}