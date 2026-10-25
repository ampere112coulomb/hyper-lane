using AVFoundation;
using Foundation;
using System.Runtime.InteropServices;
namespace HyperLane.iOS;
internal sealed class IosDanFailureAudio : IDisposable
{
    private readonly AVAudioEngine engine=new();
    private readonly AVAudioPlayerNode node=new();
    private readonly AVAudioFile file;
    private AVAudioPcmBuffer? output;
    internal IosDanFailureAudio(string path){file=new AVAudioFile(NSUrl.FromFilename(path),out var error);if(error!=null)throw new IOException(error.LocalizedDescription);engine.AttachNode(node);engine.Connect(node,engine.MainMixerNode,file.ProcessingFormat);engine.Prepare();}
    internal void Start(double seconds,double volume)
    {
        var format=file.ProcessingFormat;int rate=(int)format.SampleRate,channels=(int)format.ChannelCount,frames=rate*3;
        using var input=new AVAudioPcmBuffer(format,(uint)frames);file.FramePosition=Math.Clamp((long)(seconds*rate),0,file.Length);if(!file.ReadIntoBuffer(input,out var error))throw new IOException(error?.LocalizedDescription??"PCM read failed");
        float[] interleaved=new float[frames*channels],channel=new float[frames];for(int c=0;c<channels;c++){Array.Clear(channel);Marshal.Copy(Marshal.ReadIntPtr(input.FloatChannelData,c*IntPtr.Size),channel,0,(int)input.FrameLength);for(int f=0;f<frames;f++)interleaved[f*channels+c]=channel[f];}
        new HyperLane.DanFailureDsp(rate,channels,volume).Process(interleaved,0,interleaved.Length);output?.Dispose();output=new AVAudioPcmBuffer(format,(uint)frames){FrameLength=(uint)frames};for(int c=0;c<channels;c++){for(int f=0;f<frames;f++)channel[f]=interleaved[f*channels+c];Marshal.Copy(channel,0,Marshal.ReadIntPtr(output.FloatChannelData,c*IntPtr.Size),frames);}
        if(!engine.StartAndReturnError(out error))throw new IOException(error?.LocalizedDescription??"Audio engine failed");node.ScheduleBuffer(output,null,AVAudioPlayerNodeBufferOptions.Interrupts,null);node.Play();
    }
    public void Dispose(){node.Stop();engine.Stop();output?.Dispose();file.Dispose();node.Dispose();engine.Dispose();}
}
