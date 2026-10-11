namespace HyperLane.Android;

internal static class MobileSoundPolicy
{
    // Autoplay always supplies its own presses, independently of automatic SE
    // in manual play. Neither switch changes the user's SE volume.
    internal static bool Scheduled(bool autoPlay, bool autoSound) => autoPlay || autoSound;
    internal static bool OnJudgement(bool autoPlay, bool autoSound) => !autoPlay && !autoSound;
}
