namespace Smartest.Minigames
{
    /// <summary>
    /// Simon, played back last-first. Holding a sequence and reversing it are different
    /// skills, so this plays nothing like Simon even though it looks the same.
    /// </summary>
    public class BackwardsGame : SimonGame
    {
        protected override bool Backwards => true;
    }
}
