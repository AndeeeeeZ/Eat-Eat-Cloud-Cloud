using PurrNet;
using UnityEngine;

[CreateAssetMenu(menuName = "Eat-Eat Cloud-Cloud/Food Visibility Rule")]
public class MP_FoodVisibilityRule : NetworkVisibilityRule
{
    public override int complexity => 100;

    public override bool CanSee(PlayerID player, NetworkIdentity target)
    {
        return false;
    }
}
