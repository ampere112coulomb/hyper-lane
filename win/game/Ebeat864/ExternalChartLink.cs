using System;
namespace HyperLane;
internal static class ExternalChartLink
{
    internal static string Normalize(string? value)
    {
        string text=value?.Trim()??"";
        if(text.Length==0||text.Length>2048)return "";
        if(!text.StartsWith("https://",StringComparison.OrdinalIgnoreCase)&&!text.StartsWith("http://",StringComparison.OrdinalIgnoreCase))return "";
        foreach(char c in text)if(char.IsControl(c)||char.IsWhiteSpace(c))return "";
        if(!Uri.TryCreate(text,UriKind.Absolute,out var uri)||
            (uri.Scheme!=Uri.UriSchemeHttps&&uri.Scheme!=Uri.UriSchemeHttp)||
            string.IsNullOrEmpty(uri.Host)||!string.IsNullOrEmpty(uri.UserInfo))return "";
        return uri.AbsoluteUri;
    }
}
