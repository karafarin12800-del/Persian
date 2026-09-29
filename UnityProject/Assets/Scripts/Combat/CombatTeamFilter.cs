using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class CombatTeamFilter : MonoBehaviour
    {
        [SerializeField] private int teamId;
        public int TeamId => teamId;
        public void SetTeam(int value) => teamId = value;
    }
}
