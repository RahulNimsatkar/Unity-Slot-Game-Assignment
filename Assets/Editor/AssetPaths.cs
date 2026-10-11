namespace SlotGame.EditorTools
{
    /// <summary>
    /// Single source of truth for every asset path used by the builder tools.
    /// Keeping them in one place avoids magic strings scattered through the code.
    /// </summary>
    public static class AssetPaths
    {
        // Machine art (pixel art -> point filtering)
        public const string MachineBody   = "Assets/Art/Machine/Machine_Body.png";
        public const string LeverUp       = "Assets/Art/Machine/Lever_Up.png";
        public const string LeverDown     = "Assets/Art/Machine/Lever_Down.png";
        public const string GlassOverlay  = "Assets/Art/Machine/Glass_Overlay.png";
        public const string Marquee       = "Assets/Art/Machine/Machine_Marquee.png";
        public const string Background    = "Assets/Art/Machine/Background.png";

        // Symbols (pixel art -> point filtering)
        public const string SymbolSeven   = "Assets/Art/Symbols/Symbol_Seven.png";
        public const string SymbolCherry  = "Assets/Art/Symbols/Symbol_Cherry.png";
        public const string SymbolBell    = "Assets/Art/Symbols/Symbol_Bell.png";
        public const string SymbolBar     = "Assets/Art/Symbols/Symbol_Bar.png";

        // UI art (smooth gradients -> bilinear filtering)
        public const string ButtonCloseSheet = "Assets/UI/Sprites/Btn_Close_Sheet.png";
        public const string ButtonLeftSheet  = "Assets/UI/Sprites/Btn_Left_Sheet.png";
        public const string ButtonRightSheet = "Assets/UI/Sprites/Btn_Right_Sheet.png";
        public const string ButtonYesNoSheet = "Assets/UI/Sprites/Btn_YesNo_Sheet.png";
        public const string PopupPanel       = "Assets/UI/Sprites/Popup_Panel.png";
        public const string DisplayBox       = "Assets/UI/Sprites/Display_Box.png";

        // Generated assets
        public const string ScenePath      = "Assets/Scenes/SlotMachine.unity";
        public const string ReelPrefabPath = "Assets/Prefabs/Reel.prefab";
        public const string WinPulsePath   = "Assets/Animations/WinPulse.anim";
        public const string SoundsFolder   = "Assets/Sounds";

        // WebGL output (relative to the project root, as required by the assignment)
        public const string WebGLBuildPath = "Build/WebGL";
    }
}
