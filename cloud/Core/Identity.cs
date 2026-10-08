using System.Text;
using System.Text.Json.Nodes;
namespace Boutique.Core;
public static class Identity
{
    public static bool IsOwner(string? header, string? email)
    {
        if (string.IsNullOrWhiteSpace(header) || header.Length > 8192 || string.IsNullOrWhiteSpace(email)) return false;
        try {
            var p = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(header)))!.AsObject();
            return p["identityProvider"]?.GetValue<string>() == "aad"
                && !string.IsNullOrWhiteSpace(p["userId"]?.GetValue<string>())
                && string.Equals(p["userDetails"]?.GetValue<string>(), email.Trim(), StringComparison.OrdinalIgnoreCase)
                && p["userRoles"]!.AsArray().Any(r => r?.GetValue<string>() == "boutique-owner");
        } catch { return false; }
    }
}
