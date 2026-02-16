using System.Collections.Generic;

namespace TipsyBaboonTestSite.Configuration
{
    public class FishingConfiguration
    {
        public string ApplicationName { get; set; } = "TipsyBaboonFishing";

        public bool AllowRegistration { get; set; } = true;

        public bool EnableLoginWithMicrosoft { get; set; } = false;

        public bool EnableLoginWithGoogle { get; set; } = false;

        public bool EnableLoginWithFacebook { get; set; } = false;

        public string NOAAStationId { get; set; } = "8721164";

        public double DefaultLatitude { get; set; } = 28.0806;

        public double DefaultLongitude { get; set; } = -80.6081;

        public List<LoginProvider> AdditionalLoginProviders { get; set; } = new List<LoginProvider>();
    }

    public class LoginProvider
    {
        public string Provider { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string ClientId { get; set; } = string.Empty;

        public string ClientSecret { get; set; } = string.Empty;

        public string DiscoveryEndpoint { get; set; } = string.Empty;

        public List<string> Scopes { get; set; } = new List<string>();
    }
}
