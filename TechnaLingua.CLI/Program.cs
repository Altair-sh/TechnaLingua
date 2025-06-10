using System.Text;
using NAudio.Sdl2;
using NAudio.Sdl2.Interop;
using NAudio.Sdl2.Structures;
using TechnaLingua.CLI.DataOverSound;

namespace TechnaLingua.CLI;

class Program
{
    static (WaveOutSdl, WaveOutSdlCapabilities) GetOutputDevice()
    {
        var audioOutputDevices = WaveOutSdl.GetCapabilitiesList();
        foreach (WaveOutSdlCapabilities device in audioOutputDevices)
        {
            Console.WriteLine(device.ToString(",\n\t"));
        }
        
        Console.Write("Select output device number: ");
        string? deviceIdStr = Console.ReadLine();
        int deviceId = 0;
        if(!string.IsNullOrWhiteSpace(deviceIdStr))
            deviceId = Convert.ToInt32(deviceIdStr);
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

    static string GetTextInput() => "Превед медвед";

    static async Task Main(string[] args)
    {
        SDL.SDL_GetVersion(out var sdlVersion);
        Console.WriteLine($"SDL Version: {sdlVersion.major}.{sdlVersion.minor}.{sdlVersion.patch}");
        
        var text = GetTextInput();
        var utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier:false, throwOnInvalidBytes: true);
        var dataBytes = utf8WithoutBom.GetBytes(text);
        await using var inputDataStream = new ProducerConsumerStream();
        inputDataStream.GetWriteStream().Write(dataBytes, 0, dataBytes.Length);
        var dataReadStream = inputDataStream.CreateReadStream();

        var (outputDevice, outputDeviceCapabilities) = GetOutputDevice();
        using (AudioOutput audioOutput = new(outputDevice, outputDeviceCapabilities))
        {
            await audioOutput.EncodeAndPlay(dataReadStream);
        }
    }
}