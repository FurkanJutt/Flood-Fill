using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.ThreeD.PowerUps
{
    public sealed class FloodPowerUpBar3D : MonoBehaviour
    {
        public Button[] buttons;
        public Image[] backgrounds,icons;
        public FloodFillUIState3D[] backgroundStates,iconStates;
        public TMP_Text[] quantities,names;
        private FloodPowerUps3D owner;
        private bool bound;
        private static readonly string[] Labels={"COLOR SWAP","SMART HINT","COLOR BLAST"};
        private void Awake()
        {
            for(int i=0;i<buttons.Length;i++){int index=i;buttons[i].onClick.AddListener(()=>
            {
                owner?.Select((FloodPowerUpKind)index);
                FloodTouchInput3D.PressFeedback(buttons[index].transform,.04f,owner!=null?owner.settings.selectionAnimationDuration:.15f);
            });}
        }
        private void OnEnable()
        {
            owner=FindAnyObjectByType<FloodPowerUps3D>();if(owner==null)return;
            owner.Changed+=Refresh;bound=true;Refresh();
        }
        private void OnDisable()
        {
            if(owner!=null&&bound){owner.Changed-=Refresh;owner.CancelActivePowerUp();}bound=false;
            foreach(var b in buttons)if(b!=null){b.transform.DOKill();b.transform.localScale=Vector3.one;}
        }
        public void Refresh()
        {
            if(owner==null||owner.settings==null)return;var settings=owner.settings;
            var layout=GetComponent<HorizontalLayoutGroup>();layout.spacing=settings.buttonSpacing;
            var rect=(RectTransform)transform;
            if(Mathf.Abs(rect.sizeDelta.y-settings.buttonSize.y)>.01f)
            {
                var palette=transform.parent.Find("ColorDock") as RectTransform;
                if(palette!=null){rect.sizeDelta=new Vector2(palette.rect.width,settings.buttonSize.y);rect.anchoredPosition=new Vector2(palette.anchoredPosition.x,palette.anchoredPosition.y+palette.rect.height*(1-palette.pivot.y)+18+settings.buttonSize.y*.5f);owner.ui?.ReflowPowerUpInstruction(rect);}
            }
            for(int i=0;i<3;i++)
            {
                var kind=(FloodPowerUpKind)i;var def=settings.Definition(kind);bool selected=owner.SelectedKind==kind;
                var element=buttons[i].GetComponent<LayoutElement>();if(element!=null){element.preferredWidth=settings.buttonSize.x;element.flexibleWidth=1;}
                bool usable=owner.Available(kind);buttons[i].interactable=settings.enablePowerUpSystem&&def.enabled&&owner.CanSelect&&(usable||selected);
                bool disabled=!buttons[i].interactable&&!selected;
                backgroundStates[i].ApplyState(selected,disabled);
                iconStates[i].ApplyState(selected,disabled);
                quantities[i].gameObject.SetActive(settings.showQuantityIndicators);quantities[i].color=settings.quantityTextColor;
                if(settings.quantityFont!=null)quantities[i].font=settings.quantityFont;quantities[i].fontSize=settings.quantityFontSize;quantities[i].fontStyle=settings.quantityFontStyle;
                quantities[i].text=settings.Unlimited?"∞":FloodPowerUpInventory3D.Quantity(kind).ToString();
                names[i].gameObject.SetActive(settings.showPowerUpNames);names[i].text=owner.State==FloodPowerUpState.Calculating&&i==1?"CALCULATING…":Labels[i];
                if(owner.State!=FloodPowerUpState.Calculating)icons[i].rectTransform.localRotation=Quaternion.identity;
            }
        }
        private void Update()
        {
            if(owner==null)return;
            if(owner.State==FloodPowerUpState.Calculating)icons[1].rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(Time.unscaledTime*4)*10);
            if(owner.CurrentHint!=null)owner.ui?.PulsePowerUpPalette(owner.settings.highlightPulseSpeed);
        }
    }
}
