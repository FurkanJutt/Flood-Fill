using FloodFill.ThreeD.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.ThreeD
{
    public sealed partial class FloodFillMobileUI3D
    {
        private Slider masterAudioSlider,musicAudioSlider,sfxAudioSlider;
        private Toggle musicAudioToggle,sfxAudioToggle;
        private TMP_Text masterAudioValue,musicAudioValue,sfxAudioValue;
        private FloodAudioManager audioSettingsOwner;
        private void BuildAudioSettings(Transform parent)
        {
            masterAudioSlider=AudioSlider(parent,"MASTER VOLUME",160,out masterAudioValue);
            musicAudioSlider=AudioSlider(parent,"MUSIC VOLUME",35,out musicAudioValue);
            sfxAudioSlider=AudioSlider(parent,"SOUND EFFECTS",-90,out sfxAudioValue);
            musicAudioToggle=AudioToggle(parent,"MUSIC ENABLED",-220);
            sfxAudioToggle=AudioToggle(parent,"SOUND EFFECTS ENABLED",-310,false);
            musicAudioToggle.name="MusicEnabledToggle";sfxAudioToggle.name="SoundEffectsEnabledToggle";
            masterAudioSlider.onValueChanged.AddListener(value=>FloodAudioManager.Instance?.SetMasterVolume(value));
            musicAudioSlider.onValueChanged.AddListener(value=>FloodAudioManager.Instance?.SetMusicVolume(value));
            sfxAudioSlider.onValueChanged.AddListener(value=>FloodAudioManager.Instance?.SetSfxVolume(value));
            musicAudioToggle.onValueChanged.AddListener(value=>FloodAudioManager.Instance?.SetMusicEnabled(value));
            sfxAudioToggle.onValueChanged.AddListener(value=>FloodAudioManager.Instance?.SetSfxEnabledFromUI(value));
            audioSettingsOwner=FloodAudioManager.Instance;
            if(audioSettingsOwner!=null)audioSettingsOwner.SettingsChanged+=RefreshAudioControls;
            RefreshAudioControls();
        }
        private Slider AudioSlider(Transform parent,string label,float y,out TMP_Text value)
        {
            TMP_Text title=AddText(parent,label,27,FontStyles.Bold,new Vector2(-150,y+26),new Vector2(440,46));title.alignment=TextAlignmentOptions.MidlineLeft;
            value=AddText(parent,"100%",26,FontStyles.Bold,new Vector2(290,y+26),new Vector2(150,46),Muted);
            var root=MakeRect(label+" Slider",parent,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,y-20),new Vector2(700,48));
            var background=MakeImage("Track",root.transform,Vector2.zero,new Vector2(700,12),Navy);
            var fill=MakeImage("Fill",root.transform,Vector2.zero,new Vector2(700,12),Accent);
            fill.transform.SetParent(background.transform, false);
            background.GetComponent<Image>().raycastTarget=false;
            fill.GetComponent<Image>().raycastTarget=false;
            var handle=MakeImage("Handle",root.transform,Vector2.zero,new Vector2(36,36),Color.white);
            Slider slider=root.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.wholeNumbers=false;
            var fillRect=fill.GetComponent<RectTransform>();fillRect.sizeDelta=Vector2.zero;fillRect.anchorMin=new Vector2(0,.5f);fillRect.anchorMax=new Vector2(1,.5f);
            slider.fillRect=fillRect;slider.handleRect=handle.GetComponent<RectTransform>();slider.targetGraphic=handle.GetComponent<Image>();
            slider.navigation=new Navigation{mode=Navigation.Mode.None};
            FloodAudioManager.Instance?.BindControl(slider,label);
            return slider;
        }
        private Toggle AudioToggle(Transform parent,string label,float y,bool automaticSound=true)
        {
            var row=MakeRect(label,parent,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,y),new Vector2(700,64));
            var title=AddText(row.transform,label,27,FontStyles.Bold,new Vector2(-40,0),new Vector2(540,60));title.alignment=TextAlignmentOptions.MidlineLeft;
            var box=MakeImage("Toggle",row.transform,new Vector2(310,0),new Vector2(52,52),Navy);
            var check=MakeImage("Checkmark",box.transform,Vector2.zero,new Vector2(32,32),Accent);
            var toggle=box.AddComponent<Toggle>();toggle.targetGraphic=box.GetComponent<Image>();toggle.graphic=check.GetComponent<Image>();
            FloodAudioManager.Instance?.BindControl(toggle,label,!automaticSound);
            SkinSwitch(toggle);
            toggle.onValueChanged.AddListener(_ => SkinSwitch(toggle));
            return toggle;
        }
        private void RefreshAudioControls()
        {
            var audio=FloodAudioManager.Instance;if(audio==null||masterAudioSlider==null)return;
            masterAudioSlider.SetValueWithoutNotify(audio.masterVolume);musicAudioSlider.SetValueWithoutNotify(audio.musicVolume);sfxAudioSlider.SetValueWithoutNotify(audio.soundEffectsVolume);
            musicAudioToggle.SetIsOnWithoutNotify(audio.enableMusic);sfxAudioToggle.SetIsOnWithoutNotify(audio.enableSoundEffects);
            SkinSwitch(musicAudioToggle); SkinSwitch(sfxAudioToggle);
            masterAudioValue.text=Mathf.RoundToInt(audio.masterVolume*100)+"%";musicAudioValue.text=Mathf.RoundToInt(audio.musicVolume*100)+"%";sfxAudioValue.text=Mathf.RoundToInt(audio.soundEffectsVolume*100)+"%";
        }
        private void UnbindAudioControls()
        {if(audioSettingsOwner!=null){audioSettingsOwner.SettingsChanged-=RefreshAudioControls;audioSettingsOwner.SaveSettings();}}
    }
}
