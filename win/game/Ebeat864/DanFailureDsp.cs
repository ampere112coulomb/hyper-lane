using System;
namespace HyperLane;

// Audio-thread-owned, allocation-free processing. Only used after terminal Dan failure.
internal sealed class DanFailureDsp(int sampleRate,int channels,double volume)
{
    private long frame;
    private uint noiseState=0x6d2b79f5;
    private readonly float[] held=new float[channels];
    private readonly float gain=(float)Math.Clamp(volume,0,1);
    public void Process(float[] samples,int offset,int count)
    {
        for(int i=0;i<count;i+=channels,frame++)
        {
            double seconds=(double)frame/sampleRate;
            float p=(float)Math.Clamp(seconds/3,0,1);
            float envelope=(float)(Math.Clamp(seconds/.01,0,1)*Math.Clamp((3-seconds)/.10,0,1));
            int hold=1+(int)(p*p*39);float steps=MathF.Pow(2,15-12*p),drive=1+15*p;
            for(int channel=0;channel<channels&&i+channel<count;channel++)
            {
                int index=offset+i+channel;float dry=float.IsFinite(samples[index])?Math.Clamp(samples[index],-1,1):0;
                if(frame%hold==0)held[channel]=MathF.Round(dry*steps)/steps;
                float distorted=MathF.Tanh(held[channel]*drive);
                noiseState^=noiseState<<13;noiseState^=noiseState>>17;noiseState^=noiseState<<5;
                float noise=(noiseState&0xffffff)/8388607.5f-1;
                float mixed=dry*(1-.8f*p)+distorted*(.35f*p)+noise*(.15f*p*p);
                // Final output is bounded by the user's BGM volume, including generated noise.
                samples[index]=Math.Clamp(mixed,-1,1)*gain*.85f*envelope;
            }
        }
    }
}
