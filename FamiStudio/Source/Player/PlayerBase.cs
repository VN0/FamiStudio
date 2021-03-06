﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

#if FAMISTUDIO_WINDOWS
using AudioStream = FamiStudio.XAudio2Stream;
#else
using AudioStream = FamiStudio.PortAudioStream;
#endif

namespace FamiStudio
{
    public class PlayerBase
    {
        protected const int SampleRate = 44100;
        protected const int BufferSize = 734 * sizeof(short); // 734 = ceil(SampleRate / FrameRate) = ceil(44100 / 60.0988)
        protected const int NumAudioBuffers = 3;

        protected int apuIndex;
        protected NesApu.DmcReadDelegate dmcCallback;

        protected AudioStream audioStream;
        protected Thread playerThread;
        protected AutoResetEvent frameEvent = new AutoResetEvent(true);
        protected ManualResetEvent stopEvent = new ManualResetEvent(false);
        protected ConcurrentQueue<short[]> sampleQueue = new ConcurrentQueue<short[]>();

        protected PlayerBase(int apuIndex)
        {
            this.apuIndex = apuIndex;
        }

        protected short[] AudioBufferFillCallback()
        {
            short[] samples = null;
            if (sampleQueue.TryDequeue(out samples))
            {
                frameEvent.Set(); // Wake up player thread.
            }
            //else
            //{
            //    Trace.WriteLine("Audio is starving!");
            //}

            return samples;
        }

        public virtual void Initialize()
        {
            dmcCallback = new NesApu.DmcReadDelegate(NesApu.DmcReadCallback);
            audioStream = new AudioStream(SampleRate, 1, BufferSize, NumAudioBuffers, AudioBufferFillCallback);
        }
        
        public virtual void Shutdown()
        {
            stopEvent.Set();
            if (playerThread != null)
                playerThread.Join();

            audioStream.Dispose();
        }

        public static bool AdvanceTempo(Song song, int speed, LoopMode loopMode, ref int tempoCounter, ref int playPattern, ref int playNote, ref int jumpPattern, ref int jumpNote, ref bool advance)
        {
            // Tempo/speed logic.
            tempoCounter += song.Tempo * 256 / 150; // NTSC

            if ((tempoCounter >> 8) >= speed)
            {
                tempoCounter -= (speed << 8);

                if (jumpNote >= 0 || jumpPattern >= 0)
                {
                    if (loopMode == LoopMode.Pattern)
                    {
                        playNote = 0;
                    }
                    else
                    {
                        playNote = Math.Min(song.PatternLength - 1, jumpNote);
                        playPattern = jumpPattern;
                    }

                    jumpPattern = -1;
                    jumpNote = -1;
                }
                else if (++playNote >= song.PatternLength)
                {
                    playNote = 0;
                    if (loopMode != LoopMode.Pattern)
                        playPattern++;
                }

                if (playPattern >= song.Length)
                {
                    if (loopMode == LoopMode.None)
                    {
                        return false;
                    }
                    else if (loopMode == LoopMode.Song)
                    {
                        playPattern = 0;
                        playNote = 0;
                    }
                }

                advance = true;
            }

            return true;
        }

        private static ChannelState CreateChannelState(int apuIdx, int channelType)
        {
            switch (channelType)
            {
                case Channel.Square1:
                case Channel.Square2:
                    return new ChannelStateSquare(apuIdx, channelType);
                case Channel.Triangle:
                    return new ChannelStateTriangle(apuIdx, channelType);
                case Channel.Noise:
                    return new ChannelStateNoise(apuIdx, channelType);
                case Channel.Dpcm:
                    return new ChannelStateDpcm(apuIdx, channelType);
                case Channel.Vrc6Square1:
                case Channel.Vrc6Square2:
                    return new ChannelStateVrc6Square(apuIdx, channelType);
                case Channel.Vrc6Saw:
                    return new ChannelStateVrc6Saw(apuIdx, channelType);
                case Channel.Vrc7Fm1:
                case Channel.Vrc7Fm2:
                case Channel.Vrc7Fm3:
                case Channel.Vrc7Fm4:
                case Channel.Vrc7Fm5:
                case Channel.Vrc7Fm6:
                    return new ChannelStateVrc7(apuIdx, channelType);
                case Channel.FdsWave:
                    return new ChannelStateFds(apuIdx, channelType);
                case Channel.Mmc5Square1:
                case Channel.Mmc5Square2:
                    return new ChannelStateMmc5Square(apuIdx, channelType);
                case Channel.NamcoWave1:
                case Channel.NamcoWave2:
                case Channel.NamcoWave3:
                case Channel.NamcoWave4:
                case Channel.NamcoWave5:
                case Channel.NamcoWave6:
                case Channel.NamcoWave7:
                case Channel.NamcoWave8:
                    return new ChannelStateNamco(apuIdx, channelType);
                case Channel.SunsoftSquare1:
                case Channel.SunsoftSquare2:
                case Channel.SunsoftSquare3:
                    return new ChannelStateSunsoftSquare(apuIdx, channelType);
            }

            Debug.Assert(false);
            return null;
        }

        public static ChannelState[] CreateChannelStates(Project project, int apuIdx)
        {
            var channelCount = project.GetActiveChannelCount();
            var states = new ChannelState[channelCount];

            int idx = 0;
            for (int i = 0; i < Channel.Count; i++)
            {
                if (project.IsChannelActive(i))
                    states[idx++] = CreateChannelState(apuIdx, i);
            }

            return states;
        }

        public static int GetNesApuExpansionAudio(Project project)
        {
            switch (project.ExpansionAudio)
            {
                case Project.ExpansionNone:
                    return NesApu.APU_EXPANSION_NONE;
                case Project.ExpansionVrc6:
                    return NesApu.APU_EXPANSION_VRC6;
#if DEV
                case Project.ExpansionVrc7:
                    return NesApu.APU_EXPANSION_VRC7;
                case Project.ExpansionFds:
                    return NesApu.APU_EXPANSION_FDS;
                case Project.ExpansionMmc5:
                    return NesApu.APU_EXPANSION_MMC5;
                case Project.ExpansionNamco:
                    return NesApu.APU_EXPANSION_NAMCO;
                case Project.ExpansionSunsoft:
                    return NesApu.APU_EXPANSION_SUNSOFT;
#endif
            }

            Debug.Assert(false);
            return 0;
        }

        protected unsafe void EndFrameAndQueueSamples()
        {
            NesApu.EndFrame(apuIndex);

            int numTotalSamples = NesApu.SamplesAvailable(apuIndex);
            short[] samples = new short[numTotalSamples];

            fixed (short* ptr = &samples[0])
            {
                NesApu.ReadSamples(apuIndex, new IntPtr(ptr), numTotalSamples);
            }

            sampleQueue.Enqueue(samples);

            // Wait until we have queued as many frames as XAudio buffers to start
            // the audio thread, otherwise, we risk starving on the first frame.
            if (!audioStream.IsStarted)
            {
                if (sampleQueue.Count == NumAudioBuffers)
                {
                    audioStream.Start();
                }
                else
                {
                    frameEvent.Set();
                }
            }
        }
    };
}
