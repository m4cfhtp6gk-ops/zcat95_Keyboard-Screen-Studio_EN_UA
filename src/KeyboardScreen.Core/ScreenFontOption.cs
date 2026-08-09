namespace KeyboardScreen.Core;
public sealed record ScreenFontOption(string Id,string DisplayName,string FamilyName,string? FileName,FontFamily FontFamily,bool IsBuiltIn=false){
    public const string DefaultId="builtin:misans";
    public static ScreenFontOption Default{get;}=new(
        DefaultId,
        "默认 MiSans",
        "MiSans",
        "MiSans-Medium.ttf",
        new FontFamily("MiSans",Path.Combine(AppContext.BaseDirectory,"Assets","Fonts","MiSans-Medium.ttf")),
        true);
}
