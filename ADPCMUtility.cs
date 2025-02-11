using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Threading.Channels;

namespace RetroBiteVST3
{
    public class ADPCMUtility
    {
        static readonly double[][] f = new double[][]
        {
            [ 0.0,          0.0],
            [-60.0 / 64.0,  0.0 ],
            [-115.0 / 64.0, 52.0 / 64.0 ],
            [-98.0 / 64.0,  55.0 / 64.0 ],
            [-122.0 / 64.0, 60.0 / 64.0 ]
        };

        // Static for find predict
        static double p_s_1 = 0.0, p_s_2 = 0.0;
        static double u_s_1 = 0.0, u_s_2 = 0.0;

        /// <summary>
        /// (Guessing) I think this tries to predict the best encoding to use for this set of samples
        /// </summary>
        public static void FindPredict(in Span<short> samplesIn, in Span<double> samplesOut, out int predict_nr, out int shift_factor)
        { 
            // Stores the 5 different versions of our samples?..
            double[,] buffer = new double[28,5];

            // ??
            Span<double> max = stackalloc double[5];
            double min = 1e10;

            // These are for current sample history during encoding...
            double s_0  = 0.0,  s_1 = 0.0, s_2 = 0.0;

            // C# hates us
            predict_nr   = 0;
            shift_factor = 0;

            for (int i = 0; i < 5; ++i)
            {
                max[i] = 0.0;
                s_1 = p_s_1;
                s_2 = p_s_2;

                for (int j = 0; j < 28; ++j)
                {
                    s_0 = Math.Clamp(samplesIn[j], -30720.0, 30719.0); // s[t-0]

                    double ds = s_0 + s_1 * f[i][0] + s_2 * f[i][1];
                    buffer[j,i] = ds;

                    if (Math.Abs(ds) > max[i])
                        max[i] = Math.Abs(ds);

                    s_2 = s_1;                                  // new s[t-2]
                    s_1 = s_0;                                  // new s[t-1]
                }

                if (max[i] < min)
                {
                    min = max[i];
                    predict_nr = i;
                }
                if (min <= 7)
                {
                    predict_nr = 0;
                    break;
                }
            }

            // store s[t-2] and s[t-1] in a static variable
            // these than used in the next function call
            p_s_1 = s_1;
            p_s_2 = s_2;

            for (int i = 0; i < 28; i++)
                samplesOut[i] = buffer[i, predict_nr];

            int min2 = (int)min;
            int shift_mask = 0x4000;
            shift_factor = 0;

            while (shift_factor < 12)
            {
                if ((shift_mask & (min2 + (shift_mask >> 3))) != 0)
                    break;
                shift_factor++;
                shift_mask = shift_mask >> 1;
            }
        }

        public static void Pack(in Span<double> samplesIn, in Span<short> samplesOut, int predict_nr, int shift_factor)
        {
            double s_0 = 0.0, s_1 = 0.0, s_2 = 0.0;

            for (int i = 0; i < 28; ++i)
            {
                s_0 = samplesIn[i] + s_1 * f[predict_nr][0] + s_2 * f[predict_nr][1];
                double ds = s_0 * (double)(1 << shift_factor);
                int di    = Math.Clamp((int)(((int) ds + 0x800) & 0xfffff000), short.MinValue, short.MaxValue);

                // Convert Samples
                samplesOut[i] = (short)di;

                // History
                di   = di >> shift_factor;
                s_2 = s_1;
                s_1 = di - s_0;
            }
        }

        public static void Unpack(in Span<byte> samplesIn, in Span<short> samplesOut, int predict_nr, int shift_factor)
        {
            double[] samples = new double[28];

            // First pack unpacks samples
            for (int i = 0; i < 28; i += 2)
            {
                int d = samplesIn[i >> 1];

                // Sample 1
                int s1 = (d & 0xF) << 12;
                if ((s1 & 0x8000) != 0)
                    s1 |= unchecked((int)0xffff0000);

                // Sample 2
                int s2 = (d & 0xF0) << 8;
                if ((s2 & 0x8000) != 0)
                    s2 |= unchecked((int)0xffff0000);

                samples[i + 0] = (double)(s1 >> shift_factor);
                samples[i + 1] = (double)(s2 >> shift_factor);
            }

            // Now converting samples to short...
            for (int i = 0; i < 28; ++i)
            {
                samples[i] = samples[i] + u_s_1 * -f[predict_nr][0] + u_s_2 * -f[predict_nr][1];

                u_s_2 = u_s_1;
                u_s_1 = samples[i];

                samplesOut[i] = (short)(int)(samples[i] + 0.5);
            }
        }
    }
}