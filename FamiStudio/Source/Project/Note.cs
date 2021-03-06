﻿using System;
using System.Diagnostics;

namespace FamiStudio
{
    public struct Note
    {
        public static string[] NoteNames = 
        {
            "C",
            "C#",
            "D",
            "D#",
            "E",
            "F",
            "F#",
            "G",
            "G#",
            "A",
            "A#",
            "B"
        };

        // TODO: Get rid of this effect thing. Too much like FamiTracker, that's not what we want.
        public const int EffectVolume       = 0;
        public const int EffectVibratoSpeed = 1; // 4Xy
        public const int EffectVibratoDepth = 2; // 4xY
        public const int EffectJump         = 3; // Bxx
        public const int EffectSkip         = 4; // Dxx
        public const int EffectSpeed        = 5; // Fxx
        public const int EffectCount        = 6;

        public const int SpeedInvalid    = 0xff;
        public const int JumpInvalid     = 0xff;
        public const int SkipInvalid     = 0xff;
        public const int VolumeInvalid   = 0xff;
        public const int VolumeMax       = 0x0f;
        public const int VibratoInvalid  = 0xf0;
        public const int VibratoSpeedMax = 0x0c;
        public const int VibratoDepthMax = 0x0f;

        public const int FlagsNone       = 0x00;
        public const int FlagsNoAttack   = 0x01;

        public const int NoteInvalid     = 0xff;
        public const int NoteStop        = 0x00;
        public const int MusicalNoteMin  = 0x01;
        public const int MusicalNoteMax  = 0x60;
        public const int NoteRelease     = 0xf7;
        public const int DPCMNoteMin     = 0x0c;
        public const int DPCMNoteMax     = 0x4b;

        public byte Value; // (0 = stop, 1 = C0 ... 96 = B7).
        public byte Flags;
        public byte Volume; // 0-15. 0xff = no volume change.
        public byte Vibrato; // Uses same encoding as FamiTracker
        public byte Speed;
        public byte Jump;
        public byte Skip;
        public byte Slide;
        public Instrument Instrument;

        public Note(int value)
        {
            Value = (byte)value;
            Volume = VolumeInvalid;
            Vibrato = VibratoInvalid;
            Jump = JumpInvalid;
            Skip = SkipInvalid;
            Speed = SpeedInvalid;
            Slide = 0;
            Flags = 0;
            Instrument = null;
        }

        public void Clear(bool preserveFx = true)
        {
            Value = NoteInvalid;
            Instrument = null;
            Slide = 0;
            Flags = 0;

            if (!preserveFx)
            {
                Speed = SpeedInvalid;
                Skip = SkipInvalid;
                Speed = SpeedInvalid;
                Volume = VolumeInvalid;
                Vibrato = VibratoInvalid;
            }
        }

        public bool IsValid
        {
            get { return Value != NoteInvalid; }
            set { if (!value) Value = NoteInvalid; }
        }

        public bool IsStop
        {
            get { return Value == NoteStop; }
            set { if (value) Value = NoteStop; }
        }

        public bool IsRelease
        {
            get { return Value == NoteRelease; }
        }

        public bool IsMusical
        {
            get { return IsValid && !IsStop && !IsRelease; }
        }

        public bool IsSlideNote
        {
            get { return Slide != 0; }
            set { if (!value) Slide = 0; }
        }

        public byte SlideNoteTarget
        {
            get { return Slide; }
            set { Slide = value; }
        }

        public byte VibratoSpeed
        {
            get { return (byte)(Vibrato >> 4); }
            set
            {
                Vibrato &= 0x0f;
                Vibrato |= (byte)(value << 4);
            }
        }

        public byte VibratoDepth
        {
            get { return (byte)(Vibrato & 0x0f); }
            set
            {
                Vibrato &= 0xf0;
                Vibrato |= value;

                if (Vibrato != VibratoInvalid) 
                    VibratoSpeed = (byte)Utils.Clamp(VibratoSpeed, 0, VibratoSpeedMax);
            }
        }

        public bool HasVolume
        {
            get { return Volume != VolumeInvalid; }
            set { if (!value) Volume = VolumeInvalid; }
        }

        public bool HasVibrato
        {
            get { return Vibrato != VibratoInvalid; }
            set { if (!value) Vibrato = VibratoInvalid; }
        }

        public bool HasAttack
        {
            get { return (Flags & FlagsNoAttack) == 0; }
            set
            {
                Flags = (byte)(Flags & ~FlagsNoAttack);
                if (!value) Flags = (byte)(Flags | FlagsNoAttack);
            }
        }

        public bool HasJump
        {
            get { return Jump != JumpInvalid; }
            set { if (!value) Jump = JumpInvalid; }
        }

        public bool HasSkip
        {
            get { return Skip != SkipInvalid; }
            set { if (!value) Skip = SkipInvalid; }
        }

        public bool HasSpeed
        {
            get { return Speed != SpeedInvalid; }
            set { if (!value) Speed = SpeedInvalid; }
        }

        public string FriendlyName
        {
            get
            {
                return GetFriendlyName(Value);
            }
        }

        public static string GetFriendlyName(int value)
        {
            if (value == NoteStop)
                return "Stop Note";
            if (value == NoteRelease)
                return "Release Note";
            if (value == NoteInvalid)
                return "";

            int octave = (value - 1) / 12;
            int note   = (value - 1) % 12;

            return NoteNames[note] + octave.ToString();
        }
        
        public bool HasValidEffectValue(int fx)
        {
            switch (fx)
            {
                case EffectVolume       : return HasVolume;
                case EffectVibratoDepth : return HasVibrato;
                case EffectVibratoSpeed : return HasVibrato;
                case EffectJump         : return HasJump;
                case EffectSkip         : return HasSkip;
                case EffectSpeed        : return HasSpeed;
            }

            return false;
        }
        
        public int GetEffectValue(int fx)
        {
            switch (fx)
            {
                case EffectVolume       : return Volume;
                case EffectVibratoDepth : return VibratoDepth;
                case EffectVibratoSpeed : return VibratoSpeed;
                case EffectJump         : return Jump;
                case EffectSkip         : return Skip;
                case EffectSpeed        : return Speed;
            }

            return 0;
        }

        public void SetEffectValue(int fx, int val)
        {
            switch (fx)
            {
                case EffectVolume       : Volume       = (byte)val; break;
                case EffectVibratoDepth : VibratoDepth = (byte)val; break;
                case EffectVibratoSpeed : VibratoSpeed = (byte)val; break;
                case EffectJump         : Jump         = (byte)val; break;
                case EffectSkip         : Skip         = (byte)val; break;
                case EffectSpeed        : Speed        = (byte)val; break;
            }
        }
        
        public void ClearEffectValue(int fx)
        {
            switch (fx)
            {
                case EffectVolume       : Volume  = VolumeInvalid;  break;
                case EffectVibratoDepth : Vibrato = VibratoInvalid; break;
                case EffectVibratoSpeed : Vibrato = VibratoInvalid; break;
                case EffectJump         : Jump    = JumpInvalid;    break;
                case EffectSkip         : Skip    = SkipInvalid;    break;
                case EffectSpeed        : Speed   = SpeedInvalid;   break;
            }
        }

        public static bool EffectWantsPreviousValue(int fx)
        {
            switch (fx)
            {
                case EffectVolume :
                case EffectVibratoDepth : 
                case EffectVibratoSpeed :
                case EffectSpeed:
                    return true;
            }

            return false;
        }
        
        public static int GetEffectMinValue(Song song, int fx)
        {
            return 0;
        }

        public static int GetEffectMaxValue(Song song, int fx)
        {
            switch (fx)
            {
                case EffectVolume       : return VolumeMax;
                case EffectVibratoDepth : return VibratoDepthMax;
                case EffectVibratoSpeed : return VibratoSpeedMax;
                case EffectJump         : return Math.Min(254, song.Length);
                case EffectSkip         : return Math.Min(254, song.PatternLength);
                case EffectSpeed        : return 31;
            }

            return 0;
        }
        
        public static int GetEffectDefaultValue(Song song, int fx)
        {
            switch (fx)
            {
                case EffectVolume : return VolumeMax;
                case EffectSpeed  : return song.Speed;
            }

            return 0;
        }

        public static int Clamp(int note)
        {
            Debug.Assert(note != NoteInvalid);
            if (note < MusicalNoteMin) return MusicalNoteMin;
            if (note > MusicalNoteMax) return MusicalNoteMax;
            return note;
        }

        public void SerializeState(ProjectBuffer buffer)
        {
            buffer.Serialize(ref Value);

            // At version 4(FamiStudio 1.4.0), we refactored the notes, added slide notes, vibrato and no-attack notes (flags).
            if (buffer.Version >= 4)
            {
                buffer.Serialize(ref Jump);
                buffer.Serialize(ref Skip);
                buffer.Serialize(ref Speed);
                buffer.Serialize(ref Vibrato); 
                buffer.Serialize(ref Flags);
                buffer.Serialize(ref Slide);
            }
            else
            {
                byte effect = 0;
                byte effectParam = 255;
                buffer.Serialize(ref effect);
                buffer.Serialize(ref effectParam);

                HasVibrato = false;
                HasJump = false;
                HasSkip = false;
                HasSpeed = false;

                switch (effect)
                {
                    case 1: Jump  = effectParam; break;
                    case 2: Skip  = effectParam; break; 
                    case 3: Speed = effectParam; break;
                }
            }

            // At version 3 (FamiStudio 1.2.0), we added a volume track.
            if (buffer.Version >= 3)
                buffer.Serialize(ref Volume);
            else
                Volume = Note.VolumeInvalid;

            buffer.Serialize(ref Instrument);
        }
    }
}
