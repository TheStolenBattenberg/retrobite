using NPlug;
using RetroBiteVST3;
using System.Runtime.ExceptionServices;

namespace RetroBite
{
    public class RetroBiteProcessor : AudioProcessor<RetroBiteModel>
    {
        public static readonly Guid ClassID = new Guid("9300d131-560a-4fe6-ba8c-5058cdaa92b7");
        public override Guid ControllerClassId => RetroBiteController.ClassId;

        public RetroBiteProcessor() : base(AudioSampleSizeSupport.Float32)
        {

        }

        protected override bool Initialize(AudioHostApplication host)
        {
            // Initialize Input-Output channels
            AddAudioInput("AudioInput",   SpeakerArrangement.SpeakerStereo);
            AddAudioOutput("AudioOutput", SpeakerArrangement.SpeakerStereo);
            return true;
        }

        protected override void OnActivate(bool isActive)
        {
            logit.Log("OnActivate Called!");
        }

        protected override void ProcessMain(in AudioProcessData data)
        {
            // We use this buffers for transforming our samples...
            Span<short> pcmBuffer  = stackalloc short[28];
            Span<short> adpBuffer  = stackalloc short[28];
            Span<double> pakBuffer = stackalloc double[28];
            Span<byte> nibbles     = stackalloc byte[14];

            // Process each channel
            for (int channel = 0; channel < 2; ++channel)
            {
                // Get Input - Output Spans
                Span<float> inputChannel  = data.Input[0].GetChannelSpanAsFloat32(ProcessSetupData, data, channel);
                Span<float> outputChannel = data.Output[0].GetChannelSpanAsFloat32(ProcessSetupData, data, channel);

                logit.Log($"Want to filter {data.SampleCount} samples!");

                // We must now process each sample
                int samplesRemaining = data.SampleCount;

                int i = 0;
                while (i < data.SampleCount)
                {
                    pcmBuffer.Clear();

                    int numSamples = Math.Min(28, data.SampleCount - i);

                    logit.Log($"Filtering batch of {numSamples} samples...");

                    for (int j = 0; j < numSamples; ++j)
                        pcmBuffer[j] = (short)(short.MaxValue * inputChannel[i++]);

                    // Our sample buffer has now been filled - lets do our ADPCM roundtrip...
                    ADPCMUtility.FindPredict(pcmBuffer, pakBuffer, out int predictNR, out int shiftFactor);
                    ADPCMUtility.Pack(pakBuffer, adpBuffer, predictNR, shiftFactor);

                    // Convert adp to bytes
                    for (int k = 0; k < 28; k += 2)
                        nibbles[k >> 1] = (byte)(((adpBuffer[k + 1] >> 8) & 0xF0) | ((adpBuffer[k] >> 12) & 0xF));

                    // Unpack the samples...
                    ADPCMUtility.Unpack(nibbles, pcmBuffer, predictNR, shiftFactor);

                    for (int j = 0; j < numSamples; ++j)
                        outputChannel[(i - numSamples) + j] = (pcmBuffer[j] / (float)short.MaxValue);
                }

                logit.Log($"Filter pass complete!");
            }
        }
    }
}
