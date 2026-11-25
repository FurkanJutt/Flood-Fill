using System;
using DG.Tweening;
using UnityEngine;
using TMPro;
namespace FloodFill.ThreeD.PowerUps
{
    public enum FloodPowerUpKind { ColorSwap, SmartHint, ColorBlast }
    public enum FloodPowerUpState { Idle, SwapTargeting, BlastTargeting, Calculating, FocusingHint, HintDisplayed, Executing }
    [Serializable] public sealed class FloodPowerUpDefinition3D
    {
        public bool enabled=true;
        [Min(0)] public int startingQuantity=3;
        [Min(1)] public int maximumQuantity=99;
        public Sprite icon;
        [Min(0)] public int moveCost;
        [Min(.05f)] public float animationDuration=.22f;
        public Ease animationEasing=Ease.OutCubic;
        public bool enableScalePop=true;
        [Range(0,.15f)] public float scalePopIntensity=.06f;
        public Color highlightColor=new Color(.2f,.9f,1,1);
        [Range(0,2)] public float highlightIntensity=1;
        public AudioClip activationSound;
    }
    [CreateAssetMenu(menuName="Flood Fill/Power-Up Settings")]
    public sealed class FloodPowerUpSettings3D : ScriptableObject
    {
        [Header("General")]
        public bool enablePowerUpSystem=true;
        public FloodPowerUpDefinition3D colorSwap=new FloodPowerUpDefinition3D();
        public FloodPowerUpDefinition3D smartHint=new FloodPowerUpDefinition3D();
        public FloodPowerUpDefinition3D colorBlast=new FloodPowerUpDefinition3D {animationDuration=.32f};
        [Tooltip("Only honored in Editor and development builds.")] public bool unlimitedPowerUpsDevelopmentMode;
        public bool saveInventory=true,allowPowerUpsInCustomGames=true,trackPowerUpUsage=true,debugLogging;
        [Header("Color Blast")]
        public Color neighborHighlightColor=new Color(.4f,.7f,1,.6f);
        [Min(.05f)] public float centerCubeTransitionDuration=.2f;
        [Min(0)] public float neighborWaveDelay=.07f;
        [Header("Smart Hint Search")]
        [Range(50,1000)] public int calculationTimeBudgetMs=250;
        [Range(100,3000)] public int maximumSearchBudgetMs=1000;
        public bool enableExtendedSearch;
        [Range(100,100000)] public int maximumSearchStates=30000;
        [Range(4,64)] public int searchMemoryMegabytes=16;
        [Header("Smart Hint Camera")]
        public bool enableAutomaticHintRotation=true;
        [Min(.1f)] public float rotationDuration=.6f;
        public Ease rotationEasing=Ease.InOutSine;
        [Min(30)] public float maximumRotationSpeed=240;
        [Min(0)] public float minimumRotationThreshold=4;
        [Range(.01f,1)] public float visibilityThreshold=.15f;
        public bool allowCameraPanAdjustment=true,allowCameraZoomAdjustment=true;
        public Vector2 targetScreenPosition=new Vector2(.5f,.5f);
        [Header("Smart Hint Presentation")]
        public Color regionHighlightColor=Color.white;
        [Range(.1f,2)] public float regionHighlightIntensity=1;
        [Min(.1f)] public float highlightPulseSpeed=2;
        public bool highlightRecommendedPaletteColor=true,previewConnectedNeighboringRegions=true;
        public bool autoExecuteRecommendedMove;
        [Min(.25f)] public float automaticExecutionPreviewDuration=.75f;
        public AudioClip hintRevealedSound;
        [Header("Gameplay Integration")]
        public bool awardScoreForPowerUpCaptures;
        [Tooltip("Opt-in for Big Capture / Combo Capture, whose ordinary wording requires moves.")] public bool allowCompatibleGoalProgressFromPowerUps;
        [Header("UI")]
        public bool showQuantityIndicators=true,showPowerUpNames=true;
        public Sprite availableButtonSprite,selectedButtonSprite,disabledButtonSprite;
        public Color quantityTextColor=Color.white,selectedGlow=new Color(0,.9f,1,1);
        public TMP_FontAsset quantityFont;
        [Min(10)] public float quantityFontSize=25;
        public FontStyles quantityFontStyle=FontStyles.Bold;
        [Min(0)] public float buttonSpacing=16;
        public Vector2 buttonSize=new Vector2(250,110);
        [Min(.05f)] public float selectionAnimationDuration=.15f;
        [Header("Optional Sound Effects")]
        public AudioClip selectionSound,cancellationSound,hintActivationSound,invalidActionSound,unavailableSound;
        public Material outlineMaterial;
        public FloodPowerUpDefinition3D Definition(FloodPowerUpKind kind)=>kind==FloodPowerUpKind.ColorSwap?colorSwap:kind==FloodPowerUpKind.SmartHint?smartHint:colorBlast;
        public bool Unlimited
        {
            get {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return unlimitedPowerUpsDevelopmentMode;
#else
                return false;
#endif
            }
        }
    }
    [Serializable] public sealed class FloodPowerUpAttempt3D
    {
        public int[] used=new int[3];
        public bool scoreGuardActive;
        public int[] rewardedCubeIndices=Array.Empty<int>();
    }
}
