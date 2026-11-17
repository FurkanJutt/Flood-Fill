using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FloodFill.ThreeD.Audio
{
    /// <summary>Uses uGUI's registry, not scene searches. Allocates only when controls are added.</summary>
    internal sealed class FloodAudioUIBinder : IDisposable
    {
        private sealed class Binding
        {
            internal Selectable Control;
            internal UnityAction Click;
            internal UnityAction<bool> Toggle;
            internal UnityAction<float> Slider;
            internal string Label;
            internal bool Silent;
            internal FloodSound Sound;
        }
        private readonly Dictionary<Selectable,Binding> bindings = new Dictionary<Selectable,Binding>();
        private readonly List<Selectable> removed = new List<Selectable>();
        private Selectable[] registry = new Selectable[64];
        private float nextScan;
        private int lastCount=-1;
        internal void Scan(bool force=false)
        {
            int count=Selectable.allSelectableCount;
            if(!force&&count==lastCount&&Time.unscaledTime<nextScan)return;
            lastCount=count;nextScan=Time.unscaledTime+.5f;
            if(registry.Length<count)Array.Resize(ref registry,Mathf.NextPowerOfTwo(count));
            int found=Selectable.AllSelectablesNoAlloc(registry);
            for(int i=0;i<found;i++){Bind(registry[i],null,false);registry[i]=null;}
            removed.Clear();
            foreach(var pair in bindings)if(pair.Key==null)removed.Add(pair.Key);
            for(int i=0;i<removed.Count;i++)bindings.Remove(removed[i]);
        }
        internal void Bind(Selectable control,string label,bool silent)
        {
            if(control==null)return;
            if(bindings.TryGetValue(control,out var previous))
            {if(label!=null){previous.Label=label;previous.Silent=silent;previous.Sound=Classify(label);}return;}
            var binding=new Binding{Control=control,Label=label??control.name,Sound=Classify(label??control.name),Silent=silent};
            if(control is Button button)
            {
                binding.Click=()=>Click(binding);button.onClick.AddListener(binding.Click);
            }
            else if(control is Toggle toggle)
            {
                binding.Toggle=value=>Play(binding,value?FloodSound.ToggleOn:FloodSound.ToggleOff);toggle.onValueChanged.AddListener(binding.Toggle);
            }
            else if(control is Slider slider)
            {
                binding.Slider=value=>Play(binding,FloodSound.Slider);slider.onValueChanged.AddListener(binding.Slider);
            }
            bindings.Add(control,binding);
        }
        private static void Click(Binding binding)
        {
            Play(binding,binding.Sound);
        }
        internal static FloodSound Classify(string label)
        {
            string value=label.ToUpperInvariant();
            if(value.Contains("CANCEL"))return FloodSound.ButtonCancel;
            if(value.Contains("BACK")||value.Contains("MAIN MENU"))return FloodSound.ButtonBack;
            if(value.Contains("CONFIRM")||value.Contains("START")||value=="PLAY"||value.Contains("CONTINUE")||value.Contains("RESUME"))return FloodSound.ButtonConfirm;
            return FloodSound.ButtonClick;
        }
        private static void Play(Binding binding,FloodSound sound)
        {
            if(binding.Control==null)return;
            // Button actions may hide/disable their own screen before our listener runs.
            if(!(binding.Control is Button)&&(!binding.Control.IsInteractable()||!binding.Control.gameObject.activeInHierarchy))return;
            var special=binding.Control.GetComponent<FloodAudioUIOverride>();
            if(special!=null){if(special.silent)return;sound=special.sound;}
            else if(binding.Silent||binding.Control.name.StartsWith("Color_",StringComparison.Ordinal)||binding.Control.GetComponent<ColorButton3D>()!=null)return;
            FloodAudioManager.Instance?.PlaySound(sound);
        }
        public void Dispose()
        {
            foreach(var b in bindings.Values)
            {
                if(b.Control is Button button&&b.Click!=null)button.onClick.RemoveListener(b.Click);
                if(b.Control is Toggle toggle&&b.Toggle!=null)toggle.onValueChanged.RemoveListener(b.Toggle);
                if(b.Control is Slider slider&&b.Slider!=null)slider.onValueChanged.RemoveListener(b.Slider);
            }
            bindings.Clear();
        }
    }
}
