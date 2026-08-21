namespace KeyboardScreen.Core;
public sealed record ScreenFontOption(string Id,string DisplayNameOrKey,string FamilyName,string? FileName,FontFamily FontFamily,bool IsBuiltIn=false){
    public const string DefaultId="builtin:misans";
    /// <summary>Name shown in the font picker; built-in entries follow the app language.</summary>
    public string DisplayName=>IsBuiltIn?Loc.T(DisplayNameOrKey):DisplayNameOrKey;
    public static ScreenFontOption Default{get;}=new(
        DefaultId,
        "FontDefaultMiSans",
        "MiSans",
        "MiSans-Medium.ttf",
        new FontFamily("MiSans",Path.Combine(AppContext.BaseDirectory,"Assets","Fonts","MiSans-Medium.ttf")),
        true);
}
