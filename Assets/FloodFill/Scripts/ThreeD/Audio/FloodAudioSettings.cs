using System;
using UnityEngine;

namespace FloodFill.ThreeD.Audio
{
    public enum FloodMusicState { Silent, MainMenu, Normal, Medium, Hard }
    public enum FloodSound
    {
        ButtonClick, ButtonBack, ButtonConfirm, ButtonCancel, ToggleOn, ToggleOff, Slider,
        PopupOpen, PopupClose, ColorSelected, BlockColorChange, FloodProgress, FloodEnd, LargeCapture,
        LevelStarted, LevelCompleted, LevelFailed,
        // Keep serialized IDs stable after removing the former cue at 17.
        Restart=18, Pause, Resume
    }
    [Serializable]
    public sealed class FloodSoundSettings
    {
        public AudioClip clip;
        public AudioClip[] alternateClips = Array.Empty<AudioClip>();
        [Range(0f,1f)] public float volume = .65f;
        [Range(.25f,2f)] public float pitch = 1f;
        [Range(0f,.15f)] public float randomPitch = .025f;
        [Min(0f)] public float minimumInterval = .08f;
        [Range(1,8)] public int maximumInstances = 2;
        [Range(0f,.2f)] public float fadeDuration = .015f;
        public bool HasClip
        {
            get
            {
                if(clip!=null)return true;
                if(alternateClips!=null)for(int i=0;i<alternateClips.Length;i++)if(alternateClips[i]!=null)return true;
                return false;
            }
        }
        public AudioClip ChooseClip()
        {
            int count=alternateClips==null?0:alternateClips.Length;
            if(count==0)return clip;
            int index=FloodAudioRandom.Range(0,count+(clip!=null?1:0));
            if(clip!=null&&index==count)return clip;
            var selected=alternateClips[index];if(selected!=null)return selected;
            if(clip!=null)return clip;
            for(int i=0;i<count;i++)if(alternateClips[i]!=null)return alternateClips[i];
            return null;
        }
    }
    // Audio variation must not consume Unity's gameplay/procedural random stream.
    internal static class FloodAudioRandom
    {
        private static readonly System.Random Random=new System.Random(unchecked(Environment.TickCount^0x3A10D));
        internal static int Range(int min,int max)=>Random.Next(min,max);
        internal static float Range(float min,float max)=>min+(float)Random.NextDouble()*(max-min);
    }
    public static class FloodAudioPolicy
    {
        public static FloodMusicState GameplayMusic(int budget,int movesUsed,bool dynamic,float mediumFraction,int hardMoves)
        {
            if(!dynamic)return FloodMusicState.Normal;
            int remaining=Math.Max(0,budget-movesUsed);
            if(remaining<=Math.Max(0,hardMoves))return FloodMusicState.Hard;
            return remaining<=Math.Max(1,budget)*Mathf.Clamp01(mediumFraction)?FloodMusicState.Medium:FloodMusicState.Normal;
        }
        public static float Decibels(float linear) => linear<=.0001f?-80f:Mathf.Clamp(20f*Mathf.Log10(linear),-80f,0f);
    }
}
