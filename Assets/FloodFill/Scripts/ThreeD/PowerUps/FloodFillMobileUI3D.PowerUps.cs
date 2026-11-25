using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.ThreeD
{
    public sealed partial class FloodFillMobileUI3D
    {
        public bool PowerUpGameplayVisible=>hud!=null&&hud.activeInHierarchy;
        public GameObject PowerUpHud=>hud;
        private TMP_Text powerInstruction;
        private string normalPowerInstruction;
        private Outline[] powerPaletteOutlines;
        private int powerPaletteHint=-1;
        private void EnsurePowerUpUI()
        {
            if(powerInstruction==null&&hud!=null)
                foreach(var text in hud.GetComponentsInChildren<TMP_Text>(true))if(text.text.Contains("Choose a color")){powerInstruction=text;normalPowerInstruction=text.text;break;}
            if(powerPaletteOutlines==null&&colorButtons.Count>0)
            {
                powerPaletteOutlines=new Outline[colorButtons.Count];
                for(int i=0;i<colorButtons.Count;i++)
                {
                    var outline=colorButtons[i].GetComponent<Outline>();if(outline==null)outline=colorButtons[i].gameObject.AddComponent<Outline>();
                    outline.effectDistance=new Vector2(4,-4);outline.effectColor=Color.cyan;outline.enabled=false;powerPaletteOutlines[i]=outline;
                }
            }
        }
        public void ReflowPowerUpInstruction(RectTransform bar)
        {EnsurePowerUpUI();if(powerInstruction!=null)powerInstruction.rectTransform.anchoredPosition=new Vector2(20,bar.anchoredPosition.y+bar.rect.height*.5f+50);}
        public void SetPowerUpInstruction(string text){EnsurePowerUpUI();if(powerInstruction!=null)powerInstruction.text=text;}
        public void RestorePowerUpInstruction(){if(powerInstruction!=null)powerInstruction.text=normalPowerInstruction;}
        public void SetPowerUpPaletteHint(int color)
        {
            EnsurePowerUpUI();powerPaletteHint=color;if(powerPaletteOutlines==null)return;
            for(int i=0;i<powerPaletteOutlines.Length;i++)if(powerPaletteOutlines[i]!=null)powerPaletteOutlines[i].enabled=i==color;
        }
        public void PulsePowerUpPalette(float speed)
        {
            if(powerPaletteHint<0||powerPaletteOutlines==null||powerPaletteHint>=powerPaletteOutlines.Length||powerPaletteOutlines[powerPaletteHint]==null)return;
            var tint=new Color(.4f,1,1,.65f+.35f*Mathf.Sin(Time.unscaledTime*speed*Mathf.PI));powerPaletteOutlines[powerPaletteHint].effectColor=tint;
        }
        private void RefreshPowerUpPaletteAvailability()
        {
            if(manager==null||manager.PowerUps==null||!manager.PowerUps.IsTargeting)return;
            for(int i=0;i<colorButtons.Count;i++)colorButtons[i].interactable=manager.CanInteract&&i!=manager.PowerUps.TargetColor;
        }
    }
}
